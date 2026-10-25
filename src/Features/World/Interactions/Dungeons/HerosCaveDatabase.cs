using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class HerosCaveDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _records = new();
    private readonly Dictionary<string, int> _constants = new();
    private readonly (int Room, int Position)[] _portals = new (int, int)[8];
    internal Warp ExitWarp { get; }

    internal HerosCaveDatabase()
    {
        var table = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/heros_cave_objects.tsv",
            new GeneratedTableSchema("Hero's Cave objects", GeneratedTableKeySemantics.Unique,
                ["group", "room", "order", "kind", "id", "subid", "y", "x", "condition", "source"],
                ["group", "room", "order"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            var record = new DungeonObjectRecord(row.Decimal(0, 4, 4), row.HexByte(1), row.UnsignedDecimal(2),
                row.RequiredString(3) switch {
                    "lever" => DungeonObjectKind.Lever,
                    "colored-cube" => DungeonObjectKind.ColoredCube,
                    "lever-lava-filler" => DungeonObjectKind.LeverLavaFiller,
                    "cave-portal" => DungeonObjectKind.CavePortal,
                    "cave-puzzle" => DungeonObjectKind.CavePuzzle,
                    "tile-filler" => DungeonObjectKind.TileFiller,
                    "floor-fill-chest" => DungeonObjectKind.FloorFillChest,
                    "trigger-chest" => DungeonObjectKind.TriggerChestScript,
                    "signal-script" => DungeonObjectKind.SignalScript,
                    "tile-script" => DungeonObjectKind.TileScript,
                    "tile-pattern-chest" => DungeonObjectKind.TilePatternChest,
                    _ => throw row.Invalid(3, "a supported Hero's Cave interaction") },
                row.HexByte(4), row.HexByte(5), row.HexByte(6), row.HexByte(7),
                DungeonObjectData.ParseCondition(row, 8), row.RequiredString(9));
            _records.GetOrAdd(record.Room).Add(record);
        }
        if (table.Rows.Count != 25) throw new InvalidOperationException("Hero's Cave requires 25 native placements.");
        table = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/heros_cave_portals.tsv",
            new GeneratedTableSchema("Hero's Cave portal destinations", GeneratedTableKeySemantics.Unique,
                ["index", "room", "position", "source"], ["index"], headerRequired: true));
        if (table.Rows.Count != 8) throw new InvalidOperationException("Hero's Cave requires eight portal destinations.");
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            if (row.UnsignedDecimal(0) != i) throw row.Invalid(0, "source-ordered portal destinations $00..$07");
            _portals[i] = (row.HexByte(1), row.HexByte(2));
        }
        table = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/heros_cave_constants.tsv",
            new GeneratedTableSchema("Hero's Cave constants", GeneratedTableKeySemantics.Unique,
                ["name", "value", "source"], ["name"], headerRequired: true));
        foreach (var row in table.Rows) _constants.Add(row.RequiredString(0), row.Decimal(1, 0, 255));
        if (_constants.Count != 6) throw new InvalidOperationException("Hero's Cave requires six source constants.");
        var exit = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/heros_cave_exit.tsv",
            new GeneratedTableSchema("Hero's Cave final chest exit", GeneratedTableKeySemantics.Ordered,
                ["group", "room", "destination-transition", "position", "source-transition", "source"],
                headerRequired: true)).SingleRow();
        ExitWarp = new Warp(4, 0xc5, -1, 0, exit.HexByte(4), exit.Decimal(0, 0, 7),
            exit.HexByte(1), exit.HexByte(3), 0, exit.HexByte(2), DirectFadeOut: true);
    }

    internal int Constant(string name) => _constants.TryGetValue(name, out int value) ? value
        : throw new InvalidOperationException($"Hero's Cave source constant '{name}' is missing.");
    internal (int Room, int Position) Portal(int index) => index is >= 0 and < 8 ? _portals[index]
        : throw new InvalidOperationException($"INTERAC $7e:$01 has unsupported destination ${index:x2}.");
    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group, int room) => group == 4
        ? _records.ValuesOrEmpty(room) : Array.Empty<DungeonObjectRecord>();
}
