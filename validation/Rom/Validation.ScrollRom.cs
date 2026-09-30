using Godot;
using System;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateScrollTimingRom()
    {
        var graphics = new ScreenTransitionGraphicsDatabase();
        foreach (var route in new[] { (0, 0x11, 0x12), (4, 0x07, 0x03),
            (0, 0x6a, 0x6b), (0, 0x11, 0x11), (0, 0x6b, 0x6b) })
        for (int direction = 0; direction < 4; direction++)
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(route.Item1, route.Item2);
            bool large = _currentRoom.Width > 160;
            _entities.Clear();
            int sourceMinimap = _saveData.MinimapRoom;
            int sourceUnique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            Vector2I vector = direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, _ => Vector2I.Left };
            Vector2 start = new(vector.X < 0 ? 6.75f : vector.X > 0 ? _currentRoom.Width - 5.25f : 80.75f,
                vector.Y < 0 ? 6.25f : vector.Y > 0 ? _currentRoom.Height - 6.75f : 64.25f);
            _player.WarpTo(start); _player.Face(Vector2I.Down);
            int target = route.Item3;
            _transitions.BeginScroll(_player, vector, target);
            start = _player.PrecisePosition;
            int unique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var rom = new ScrollRom(large, direction, (int)(start.X * 256), (int)(start.Y * 256), unique, sourceUnique);
            int update = 0;
            void Compare()
            {
                rom.Update(); update++;
                Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                // Link's original high coordinates wrap while outside the room.
                Vector2 actual = new(((int)(_player.PrecisePosition.X * 256) & 0xffff) / 256.0f,
                    ((int)(_player.PrecisePosition.Y * 256) & 0xffff) / 256.0f);
                bool finished = rom[0xcd04] == 2;
                FailIf(actual != expected || _transitions.ScrollActive == finished ||
                    _saveData.MinimapRoom != (finished ? target : sourceMinimap) || _player.FacingVector != Vector2I.Down,
                    $"ROM scroll large={large}, dir={direction}, batch={batched}, update={update}, state=${rom[0xcd04]:x2}/{rom[0xcd05]}/{rom[0xcd06]}: native={expected}, runtime={actual}, active={_transitions.ScrollActive}.");
                if (finished)
                    FailIf(rom[0xcc21] != (int)_player.PrecisePosition.Y || rom[0xcc22] != (int)_player.PrecisePosition.X ||
                        _player.LocalRespawnPosition.Floor() != new Vector2(rom[0xcc22], rom[0xcc21]) ||
                        _player.LocalRespawnFacingVector != Vector2I.Down ||
                        rom[0xcc23] != 2 || rom[0xcc6b] != 8 || rom[0xcd00] != 1,
                        "ROM scroll finisher must publish the destination anchor/direction and release scroll mode with an eight-update instrument lock.");
                else if (rom[0xcd04] == 5 && rom[0xcd05] != 0)
                {
                    int axis = direction % 2 == 0 ? 0xffaa : 0xffac;
                    int origin = direction % 2 == 0 ? Math.Clamp((int)start.Y - 64, 0, large ? 48 : 0)
                        : Math.Clamp((int)start.X - 80, 0, large ? 80 : 0);
                    int nativePixels = Math.Abs(unchecked((short)(rom.Word(axis) - origin)));
                    float frame = (float)typeof(RoomView).GetField("_transitionFrame", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_roomView)!;
                    FailIf(frame * 4 != nativePixels, $"ROM camera displacement differs on scroll update {update}: native={nativePixels}, runtime={frame * 4}.");
                }
            }
            StepGameplayUpdates(_transitions.ScrollTotalFrames, Vector2.Zero, batched: batched, afterUpdate: Compare);
            FailIf(_transitions.ScrollActive || rom[0xcd04] != 2, "ROM/runtime scroll did not complete together.");
        }
        GD.Print("Validated executed-ROM scroll setup, motion, row cleanup and final handoff in all four directions for small/large rooms and individual/batched updates.");
    }
}
