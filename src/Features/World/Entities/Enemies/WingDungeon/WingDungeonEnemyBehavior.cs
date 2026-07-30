using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class WingDungeonEnemyBehavior
{
    private readonly Dictionary<string, int> _values = new();

    internal static WingDungeonEnemyBehavior Shared { get; } = new();

    private WingDungeonEnemyBehavior()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/wing_dungeon_enemy_constants.tsv",
            new GeneratedTableSchema(
                "Wing Dungeon enemy constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value", "source"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _values.Add(row.RequiredString(0), row.Decimal(1));
        if (_values.Count != 39 ||
            this["spark-speed-raw"] != 0x28 ||
            this["whisp-speed-raw"] != 0x1e ||
            this["thwomp-gravity"] != 0x30 ||
            this["peahat-acceleration-frames"] != 0x7f ||
            this["sword-chase-frames"] != 0x60 ||
            this["color-gel-initial-speed-z"] != -0x180)
        {
            throw new InvalidOperationException(
                "Imported Wing Dungeon enemy constants are incomplete.");
        }
    }

    internal int this[string key] =>
        _values.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Wing Dungeon enemy constant '{key}' was not imported.");
}
