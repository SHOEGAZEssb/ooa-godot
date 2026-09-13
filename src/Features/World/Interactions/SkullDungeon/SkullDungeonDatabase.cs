using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Native Skull Dungeon objects merged with the shared source stream.</summary>
internal sealed class SkullDungeonDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _records = new();
    private readonly Dictionary<int, IReadOnlyList<byte>[]> _patterns = new();
    internal DungeonEssenceDefinition Essence { get; }

    internal SkullDungeonDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/skull_dungeon_objects.tsv",
            new GeneratedTableSchema("Skull Dungeon native objects", GeneratedTableKeySemantics.Grouped,
                ["group", "room", "order", "kind", "id", "subid", "y", "x", "condition", "source", "var03"],
                ["group", "room"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            var record = new DungeonObjectRecord(row.Decimal(0, 0, 7), row.HexByte(1), row.UnsignedDecimal(2),
                row.RequiredString(3) switch {
                    "toggle-floor" => DungeonObjectKind.ToggleFloor,
                    "floor-color-changer" => DungeonObjectKind.FloorColorChanger,
                    "floor-pattern-trigger" => DungeonObjectKind.FloorPatternTrigger,
                    "floor-pattern-key" => DungeonObjectKind.FloorPatternKey,
                    "colored-cube" => DungeonObjectKind.ColoredCube,
                    "cube-flame" => DungeonObjectKind.CubeFlame,
                    "cube-light-sensor" => DungeonObjectKind.CubeLightSensor,
                    "cube-switch-sensor" => DungeonObjectKind.CubeSwitchSensor,
                    "floor-switch-bit" => DungeonObjectKind.FloorSwitchBit,
                    "minecart-gate" => DungeonObjectKind.MinecartGate,
                    "blue-flame-chest" => DungeonObjectKind.BlueFlameChest,
                    "tile-filler" => DungeonObjectKind.TileFiller,
                    "floor-fill-chest" => DungeonObjectKind.FloorFillChest,
                    "orb-chest" => DungeonObjectKind.OrbChest,
                    "moving-orb" => DungeonObjectKind.MovingOrb,
                    "lever" => DungeonObjectKind.Lever,
                    "lever-lava-filler" => DungeonObjectKind.LeverLavaFiller,
                    "moving-platform" => DungeonObjectKind.MovingPlatform,
                    "switch-tile-toggler" => DungeonObjectKind.SwitchTileToggler,
                    "armos-warrior" => DungeonObjectKind.ArmosWarrior,
                    "eyesoar" => DungeonObjectKind.Eyesoar,
                    "boss-reward" => DungeonObjectKind.BossReward,
                    "essence" => DungeonObjectKind.Essence,
                    "miniboss-reward" => DungeonObjectKind.MinibossReward,
                    _ => throw row.Invalid(3, "a supported Skull Dungeon object kind") },
                row.HexByte(4), row.HexByte(5), row.HexByte(6), row.HexByte(7),
                DungeonObjectData.ParseCondition(row, 8), row.RequiredString(9), row.HexByte(10));
            var records = _records.GetOrAdd((record.Group << 8) | record.Room);
            if (records.Count > 0 && records[^1].Order >= record.Order)
                throw new InvalidOperationException($"Skull object order did not increase at {record.Source}.");
            records.Add(record);
        }
        if (table.Rows.Count != 46 || GetRoomRecords(4, 0x69).Count != 1 || GetRoomRecords(4, 0x6b).Count != 2 || GetRoomRecords(4, 0x80).Count != 2 || GetRoomRecords(4, 0x89).Count != 1 || GetRoomRecords(4, 0x8f).Count != 1 ||
            GetRoomRecords(4, 0x6c).Count != 2 || GetRoomRecords(4, 0x74).Count != 2 || GetRoomRecords(4, 0x92).Count != 2 ||
            GetRoomRecords(4, 0x75).Count != 3 || GetRoomRecords(4, 0x6f).Count != 2 || GetRoomRecords(4, 0x87).Count != 2 ||
            GetRoomRecords(4, 0x7f).Count != 2 || GetRoomRecords(4, 0x83).Count != 2 || GetRoomRecords(4, 0x84).Count != 2 ||
            GetRoomRecords(4, 0x71).Count != 2 ||
            GetRoomRecords(4, 0x72).Count != 3 || GetRoomRecords(4, 0x78).Count != 4 ||
            GetRoomRecords(4, 0x79).Count != 2 || GetRoomRecords(4, 0x7b).Count != 2 || GetRoomRecords(4, 0x90).Count != 7)
            throw new InvalidOperationException("Incomplete Skull Dungeon native object record set.");
        var essence = GeneratedTable.Load("res://assets/oracle/objects/skull_dungeon_essence.tsv",
            new GeneratedTableSchema("Skull Dungeon Essence", GeneratedTableKeySemantics.Ordered,
                ["index", "text-id", "text-position", "message-base64", "destination-group", "destination-room",
                 "destination-position", "destination-transition", "source"], headerRequired: true)).SingleRow();
        string message = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(essence.RequiredString(3)));
        int position = essence.UnsignedDecimal(2);
        if (position != 0) message = $"\\pos({position})" + message;
        Essence = new(essence.UnsignedDecimal(0), message, new Warp(4, 0x69, -1, 0, 0,
            essence.Decimal(4, 0, 7), essence.HexByte(5), essence.HexByte(6), 0, essence.HexByte(7)));
        if (Essence.Index != 3 || essence.HexWord(1) != 0x0011 || Essence.ExitWarp is not
            { DestinationGroup: 0, DestinationRoom: 3, DestinationPosition: 0x35, DestinationTransition: 0x0e })
            throw new InvalidOperationException("Skull Dungeon Essence source mapping is incomplete.");
        var patterns = GeneratedTable.Load("res://assets/oracle/objects/skull_dungeon_patterns.tsv",
            new GeneratedTableSchema("Skull Dungeon tile patterns", GeneratedTableKeySemantics.Unique,
                ["subid", "color", "positions", "source"], ["subid", "color"], headerRequired: true));
        foreach (var row in patterns.Rows)
        {
            int subid = row.HexByte(0);
            if (!_patterns.TryGetValue(subid, out var colors))
                _patterns.Add(subid, colors = [Array.Empty<byte>(), Array.Empty<byte>(), Array.Empty<byte>()]);
            int color = row.Decimal(1, 0, 2);
            colors[color] = Array.ConvertAll(row.RequiredString(2).Split(','), value => Convert.ToByte(value, 16));
        }
        if (patterns.Rows.Count != 5 || !_patterns.ContainsKey(0x0f) || !_patterns.ContainsKey(0x10))
            throw new InvalidOperationException("Incomplete INTERAC_DUNGEON_EVENTS $0f/$10 tile patterns.");
    }

    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group, int room) =>
        _records.ValuesOrEmpty((group << 8) | room);

    internal IReadOnlyList<byte>[] Pattern(int subid) => _patterns[subid];
}
