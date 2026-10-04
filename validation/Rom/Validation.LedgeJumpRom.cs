using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLedgeJumpRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x9a); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2? start = null;
            for (int y = 8; y < _currentRoom.Height - 32 && start is null; y++)
            for (int x = 8; x < _currentRoom.Width - 8 && start is null; x++)
            {
                Vector2 point = new(x, y);
                if (!_collision.Collides(point) &&
                    _currentRoom.GetMetatile(point + new Vector2(-3, 8)) is 0xb0 or 0xc1 &&
                    _currentRoom.GetMetatile(point + new Vector2(2, 8)) is 0xb0 or 0xc1)
                    start = point;
            }
            FailIf(start is null, "ROM ledge fixture requires the original reachable Crown 4:9a cliff.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _player.WarpTo(start!.Value); _player.Face(Vector2I.Right);
                var initialRandom = _random.CaptureState();
                var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 1,
                    (int)start.Value.X, (int)start.Value.Y);
                rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
                var sounds = _sound.AttachPlayRequestAudit();
                int update = 0, previousDirections = 0;
                void Step(int count = 1, int angle = 0xff)
                {
                    Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                    int held = angle == 0x10 ? 0x80 : 0;
                    int edge = held & ~previousDirections; previousDirections = held;
                    StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                    {
                        rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter - 1); edge = 0; update++;
                        string context = $"Crown ledge 4:9a update={update}, repeat={repeat}, batch={batched}";
                        Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                        int z = unchecked((short)rom.Word(0xd00e));
                        FailIf(_player.PrecisePosition != expected || _player.LedgeZ != z >> 8 ||
                            _player.NativeNormalStateForInteraction != (rom[0xd004] == 1) ||
                            _player.NativeInAirForInteraction != (rom[0xcc5c] != 0) ||
                            CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                            _transitions.IsTransitioning,
                            context + $": fixed XY/Z/state differs: runtime={_player.PrecisePosition}, native={expected}, Z={_player.LedgeZ}/{z >> 8}, state=${rom[0xd004]:x2}/${rom[0xd005]:x2}.");
                        if (rom[0xd004] == 0x12)
                            FailIf(_player.LedgeSpeedRaw != rom[0xd010] ||
                                _player.LedgeSpeedZ != unchecked((short)rom.Word(0xd014)),
                                context + ": planar/vertical ledge speed differs.");
                        // The native fixture supplies the text gate, not its renderer.
                        FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": jump/land sound order differs.");
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                    });
                }
                for (int approach = 0; approach < 32 && rom[0xd004] != 0x12; approach++) Step(1, 0x10);
                FailIf(rom[0xd004] != 0x12 || _player.LedgeJumpPhase != LedgeJumpState.Airborne,
                    "Actual Crown cliff approach must start the native state $12 jump.");
                Step(10);
                _dialogue.ShowMessage("Ledge pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6); _dialogue.Close(); rom[0xcba0] = 0;
                Step(30);
                FailIf(rom[0xd004] != 1 || _player.LedgeJumpPhase != LedgeJumpState.None,
                    "Ledge landing must restore normal control after the airborne interval.");
                Step(8, 0x10); Step(8);
            }
        }
        GD.Print("Validated clean-US collision-reachable Crown ledge entry, fixed XY/Z, speed/state, pause, landing, sound/RNG and repeated split/batched gameplay.");
    }
}
