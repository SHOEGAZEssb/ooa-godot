using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkHazardRecoveryRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (float fraction in new[] { 0.0f, 0.5f })
        foreach ((byte tile, bool flippers) in new (byte, bool)[] { (0xf3, false), (0xfa, false), (0xfc, false), (0xfc, true) })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33);
            _entities.Clear();
            if (flippers) _inventory.GiveTreasure(TreasureId.Flippers, 0);
            var room = _rooms.CurrentRoom;
            var rom = new LinkCollisionRom();
            rom[0xcc33] = (byte)room.ActiveCollisions;
            rom[0xd000] = 1;
            rom[0xd004] = 1;
            rom[0xd01a] = 0x81;
            rom[0xd024] = 0x80;
            rom[0xd029] = 1;
            rom[0xc6aa] = (byte)_player.HealthQuarters;
            rom[0xc69f] = (byte)(flippers ? 0x40 : 0);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c, 0, 0);
                rom[0xcf00 + y * 16 + x] = 0x2c;
            }
            room.SetPositionTileAndCollision(new(88, 72), tile, 0x10, 0);
            rom[0xcf45] = tile;
            rom[0xce45] = 0x10;
            _player.WarpTo(new(88, 55));
            _player.WarpTo(new(88 + fraction, 55 + fraction), recordSafe: false);
            rom.Word(0xd00a, (int)((55 + fraction) * 256));
            rom.Word(0xd00c, (int)((88 + fraction) * 256));
            rom[0xcc21] = 55;
            rom[0xcc22] = 88;
            rom[0xcc23] = 2;
            rom[0xd008] = 2;
            int angle = 16, update = 0, firstRespawn = -1;
            void Compare()
            {
                rom[0xcc2a] = 0;
                if (rom[0xd004] == 2) rom.Call(LinkCollisionRom.Respawn);
                else if (rom[0xcc4f] != 0) rom.Call(LinkCollisionRom.ForceState);
                else
                {
                    rom.Call(LinkCollisionRom.ApplyTile);
                    rom.Call(LinkCollisionRom.Probe);
                    if (rom[0xcc5d] != 0) rom.Call(LinkCollisionRom.Swim);
                    else if ((rom[0xcc61] & 0x10) == 0) rom.Call(LinkCollisionRom.Move, 0x28, angle);
                }
                // updateSpecialObjects clears transient immobilization after Link.
                rom[0xcc61] &= 0x0f;
                if (rom[0xd004] == 2 && firstRespawn < 0) firstRespawn = update;
                Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                FailIf(_player.PrecisePosition != expected || _player.HealthQuarters != rom[0xc6aa] ||
                    _player.Visible != ((rom[0xd01a] & 0x80) != 0) ||
                    _player.PatchCollisionsEnabled != ((rom[0xd024] & 0x80) != 0) ||
                    _player.NativeNormalStateForInteraction != (rom[0xd004] == 1),
                    $"ROM hazard tile=${tile:x2}, flippers={flippers}, update={update}, batch={batched}: ROM XY={expected}, state/sub=${rom[0xd004]:x2}/${rom[0xd005]:x2}, health={rom[0xc6aa]}, visible=${rom[0xd01a]:x2}, collision=${rom[0xd024]:x2}; runtime XY={_player.PrecisePosition}, health={_player.HealthQuarters}, visible={_player.Visible}, collision={_player.PatchCollisionsEnabled}.");
                if (rom[0xd004] == 2 && rom[0xd005] == 1)
                {
                    int frame = (int)typeof(Player).GetMethod("GetFallInHoleFrame",
                        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_player, null)!;
                    FailIf(frame + 8 != rom[0xd031],
                        $"ROM hole animation update={update}: ROM=${rom[0xd031]:x2}, runtime=${frame + 8:x2}.");
                }
                if (rom[0xd004] == 2 && rom[0xd005] == 5)
                    FailIf((_player.DrownAnimationFrame == 0 ? 0xd4 : 0x0b) != rom[0xd031],
                        $"ROM drowning animation update={update}: ROM=${rom[0xd031]:x2}, runtime frame={_player.DrownAnimationFrame}.");
                update++;
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                angle = 16;
                rom[0xcc2b] = 16;
                StepGameplayUpdates(4, Vector2.Down, batched: batched, afterUpdate: Compare);
                angle = 0xff;
                rom[0xcc2b] = 0xff;
                StepGameplayUpdates(120, Vector2.Zero, batched: batched, afterUpdate: Compare);
                FailIf(firstRespawn < 0 || rom[0xd004] != 1 || _player.IsDrowning || _player.IsFallingInHole ||
                    _player.HealthQuarters != 12 - (attempt + 1) * 2,
                    "ROM hazard fixture did not complete falling/drowning and recovery.");
            }
        }
        GD.Print("Validated ROM hole/drowning animation, damage, respawn and control recovery through individual/batched gameplay updates.");
    }
}
