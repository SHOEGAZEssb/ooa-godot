using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCollisionMasksRom()
    {
        var rom = new ObjectCollisionRom();
        var boomerang = BoomerangCollisionDatabase.Shared;
        var hook = SwitchHookCollisionDatabase.Shared;
        int cases = 0;
        foreach (bool part in new[] { false, true })
        for (int type = 0; type < (part ? 0x5a : 0x80); type++)
        foreach (int item in new[] { 0x0d, 0x17 })
        foreach (int gate in new[] { 0, 1, 2, 3, 4 })
        {
            if (part && item == 0x0d) continue;
            rom.ClearObjects();
            int target = part ? 0xd0c0 : 0xd080;
            rom[target + 0x24] = (byte)(type | (gate == 1 ? 0 : 0x80));
            rom[target + 0x2a] = (byte)(gate == 2 ? 0x80 : 0);
            rom[target + 0x2b] = (byte)(gate == 3 ? 1 : gate == 4 ? 0xff : 0);
            // Mode00 executes the real no-op handler after mask/geometry selection.
            rom[target + 0x25] = 0;
            rom[target + 0x0b] = rom[0xd70b] = 64;
            rom[target + 0x0d] = rom[0xd70d] = 80;
            rom[target + 0x26] = rom[target + 0x27] = rom[0xd726] = rom[0xd727] = 6;
            rom[0xd724] = (byte)(0x80 | item);
            rom.Call(ObjectCollisionRom.Scan);
            bool enabled = item == 0x0d ? hook.EnemyEnabled(type) : part ? boomerang.PartEnabled(type) : boomerang.EnemyEnabled(type);
            FailIf(rom.Dispatches.Count != (gate == 0 && enabled ? 1 : 0) ||
                rom.Dispatches.Count == 1 && rom.Dispatches[0] != (target, item, 0),
                $"ROM collision mask part={part}, type=${type:x2}, item=${item:x2}, gate={gate}.");
            cases++;
        }
        for (int mode = 0; mode < 0x7d; mode++)
            FailIf(boomerang.Effect(mode) != rom.Table(0x6d0a + mode * 32 + 0x17) ||
                hook.Effect(mode) != rom.Table(0x6d0a + mode * 32 + 0x0d) ||
                GaleSeedCollisionDatabase.Shared.Effect(mode) != rom.Table(0x6d0a + mode * 32 + 0x1e),
                $"ROM shared collision effect table mode=${mode:x2}.");
        GD.Print($"Validated {cases} native enemy/part mask and eligibility scans plus all 125 Boomerang, Switch Hook and Gale effect rows.");
    }

    private void ValidateLinkContactGeometryRom()
    {
        var inventory = new InventoryState(_treasures, OracleSaveData.CreateStandardGame());
        inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SHIELD_01"));
        inventory.EquipA(TreasureId.Shield);
        var world = new ValidationRingPlayerWorld();
        var player = new Player();
        player.Initialize(world, inventory, new(80, 64), new OracleRandom());
        var rom = new ObjectCollisionRom();
        int cases = 0;
        for (int direction = 0; direction < 4; direction++)
        foreach (int origin in new[] { 0, 80, 255 })
        for (int y = -16; y <= 16; y++)
        for (int x = -16; x <= 16; x++)
        {
            player.WarpTo(new(origin, origin));
            player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
            player.UpdateShieldForValidation(true, false);
            rom.ClearObjects();
            rom[0xd024] = 0x80;
            rom[0xd008] = (byte)direction;
            rom[0xd00b] = rom[0xd00d] = (byte)origin;
            rom[0xd026] = rom[0xd027] = 6;
            rom[0xd0e4] = 0x99; // PART $19 active mask: Link and L2/L3 shield.
            rom[0xd0e5] = 0; // Real no-op effect allows repeated overlap probes.
            rom[0xd0cb] = unchecked((byte)(origin + y));
            rom[0xd0cd] = unchecked((byte)(origin + x));
            rom[0xd0e6] = rom[0xd0e7] = 2;
            Rect2 target = new(new(rom[0xd0cd] - 2, rom[0xd0cb] - 2), new(4, 4));
            rom[0xcc6f] = 0;
            rom.Call(ObjectCollisionRom.Scan);
            FailIf(player.OverlapsEnemyCollision(target) != (rom.Dispatches.Count != 0),
                $"ROM body edge origin=${origin:x2}, XY={x}/{y}.");
            rom[0xcc6f] = 2;
            rom.Call(ObjectCollisionRom.Scan);
            bool shield = rom.Dispatches.Count != 0 && rom.Dispatches[0].Type == 2;
            FailIf(player.TryBlockWithShield(target, minimumLevel: 2) != shield,
                $"ROM shield edge direction={direction}, origin=${origin:x2}, XY={x}/{y}.");
            world.Sounds.Clear();
            cases++;
        }
        player.Free();
        GD.Print($"Validated {cases} native Link body/shield edge pairs across all directions and coordinate wrapping.");
    }
}
