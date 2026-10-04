using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateLinkCollisionGameplayRom()
    {
        int hostCase1 = 0;
        foreach (int collision in new[] { 0x03, 0x0c, 0x0f, 0x11, 0x1a })
        foreach (Vector2 input in new[] { Vector2.Down, new Vector2(1, 1).Normalized(), Vector2.Right })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33);
            _entities.Clear();
            var room = _rooms.CurrentRoom;
            var rom = new LinkCollisionRom();
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                room.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0x2c, 0, 0);
                rom[0xcf00 + y * 16 + x] = 0x2c;
            }
            room.SetPositionTileAndCollision(new(40, 40), 0x2c, (byte)collision, 0);
            rom[0xce22] = (byte)collision;
            _player.WarpTo(new(28, 24));
            rom.Word(0xd00a, 24 * 256);
            rom.Word(0xd00c, 28 * 256);
            rom[0xd004] = 1;
            int update = 0;
            void Compare(bool recoil)
            {
                rom.Call(LinkCollisionRom.Probe);
                if (recoil) rom.Call(LinkCollisionRom.Knockback);
                else rom.Call(LinkCollisionRom.Move, 0x28, input.X == 0 ? 16 : input.Y == 0 ? 8 : 12);
                Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                FailIf(_player.PrecisePosition != expected || (recoil && _player.KnockbackFrames != rom[0xd02d]),
                    $"Link gameplay collision=${collision:x2}, input={input}, recoil={recoil}, update={update}, batch={batched}: ROM={expected}, runtime={_player.PrecisePosition}.");
                update++;
            }
            StepGameplayUpdates(24, input, batched: batched, afterUpdate: () => Compare(false));
            // Enter recoil through the normal damage gate, then compare each
            // update, including the decrement-to-zero movement and release.
            FailIf(!_player.ApplyEnemyContactDamage(_player.EnemyContactPosition + Vector2.Down * 16,
                0, RingDamageSource.Generic, knockbackFrames: 7, allowZeroDamage: true),
                "ROM recoil fixture could not accept contact.");
            rom[0xd02c] = 0;
            rom[0xd02d] = 7;
            StepGameplayUpdates(7, Vector2.Zero, batched: batched, afterUpdate: () => Compare(true));
            Vector2 stopped = _player.PrecisePosition;
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(_player.PrecisePosition != stopped || _player.KnockbackFrames != 0,
                "Link continued recoil after its counter-zero update.");
        }
        GD.Print("Validated ROM Link wall/partial-tile sliding and diagonal movement through individual/batched gameplay updates, contact recoil and recovery.");
    }
}
