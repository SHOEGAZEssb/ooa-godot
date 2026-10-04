using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDimitriCarryOffsetsRom()
    {
        var weights = BraceletWeightDatabase.Shared;
        var rom = new LinkCollisionRom();
        rom.Word(0xd018, 0xd100); // Link.relatedObj2 is the carried companion.
        for (int frame = 0; frame < 4; frame++)
        for (int direction = 0; direction < 4; direction++)
        foreach (int z in new[] { 0, -3, -14 })
        {
            rom[0xcc5a] = frame < 2 ? (byte)1 : (byte)0x83;
            rom[0xcc5b] = (byte)(0x40 + (frame < 2 ? frame * 4 : 0));
            rom[0xd021] = (byte)(frame * 4);
            rom[0xd008] = (byte)direction;
            rom.Word(0xd00c, 72 * 256 + 0xc0); rom.Word(0xd00a, 64 * 256 + 0x80);
            rom.Word(0xd00e, z * 256);
            rom.Call(0x54df, bank: 6); // updateGrabbedObjectPosition
            Vector2I offset = weights.LiftOffset(4, frame, direction);
            FailIf(rom[0xd10d] != 72 + offset.X || rom[0xd10b] != 64 || (sbyte)rom[0xd10f] != z + offset.Y,
                $"Dimitri weight-$40 carry frame={frame}, direction={direction}, Z={z} differs from native held-position copy.");
        }
    }

    private void ValidateDimitriCarryGatesRom()
    {
        PrepareCompanionFidelityRoom(); _entities.Clear();
        var actor = (DimitriCompanionRoomEntity)SpawnFidelityCompanion(0x0c, new(72, 64));
        var check = typeof(DimitriCompanionRoomEntity).GetMethod("CarryDirectionAllowed", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rom = new LinkCollisionRom(); rom[0xd101] = 0x0c;
        rom.Word(0xd10c, 72 * 256); rom.Word(0xd10a, 64 * 256);
        int cases = 0;
        foreach (byte tile in new byte[] { 0, 0xd4, 0xd5, 0xd6, 0xfe, 0xff })
        for (int collision = 0; collision < 32; collision++)
        for (int direction = 0; direction < 4; direction++)
        {
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), tile, (byte)collision, 0);
                rom[0xcf00 + y * 16 + x] = tile;
                rom[0xce00 + y * 16 + x] = (byte)collision;
            }
            rom.Call(0x781e, objectPage: 0xd1, accumulator: direction);
            bool expected = (rom[0xc201] & 0x80) == 0;
            FailIf((bool)check.Invoke(actor, [direction])! != expected,
                $"Dimitri carry gate direction={direction}, tile=${tile:x2}, collision=${collision:x2} differs from native @checkTile.");
            cases++;
        }
        GD.Print($"Validated {cases} native Dimitri carry-direction terrain gates.");
    }

    private void ValidateDimitriThrowRom()
    {
        int hostCase1 = 0;
        foreach (bool drop in new[] { false, true })
        foreach (Vector2I direction in new[] { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left })
        foreach (int terrain in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom(); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Bracelet, 1);
            var actor = (DimitriCompanionRoomEntity)SpawnFidelityCompanion(0x0c, new(72, 64));
            Vector2 link = new Vector2(72, 64) - (Vector2)direction * 15;
            int facing = CarriedObjectMotion.DirectionIndex(direction);
            _player.WarpTo(link); _player.Face(direction);
            FailIf(!_bracelet.TryUse(_player, primaryButton: false), "Dimitri ROM throw fixture could not lift him.");
            StepGameplayUpdates(40, Vector2.Zero, ["item"], batched: batched);
            int heldZ = facing is 1 or 3 ? -15 : -14;
            FailIf(actor.PrecisePosition != link || actor.ZFixed != heldZ * 256,
                $"Dimitri held pose {actor.PrecisePosition}/{actor.ZFixed:x4} differs from native weight-$40 up-facing offset; Link={_player.Position}.");
            var rom = new CompanionRom(0x0c, link, facing, _currentRoom);
            rom.ThrowDimitri(link, heldZ, link, facing, drop);
            rom[0xc6aa] = (byte)_player.HealthQuarters;
            if (terrain != 0)
            {
                Vector2 obstacle = link + (Vector2)direction * 24;
                SetCompanionRomTile(rom, (int)obstacle.X / 16, (int)obstacle.Y / 16,
                    terrain == 1 ? (byte)0xfe : (byte)0, terrain == 1 ? (byte)0 : (byte)15);
            }
            var rng = _random.CaptureState(); rom[0xff94] = rng.Rng1; rom[0xff95] = rng.Rng2;
            FailIf(!actor.TryUseBracelet(_player, drop ? Vector2I.Zero : direction), "Dimitri throw was rejected.");
            StepCompanionRom(actor, rom, 90, Vector2.Zero, batched);
        }
    }
}
