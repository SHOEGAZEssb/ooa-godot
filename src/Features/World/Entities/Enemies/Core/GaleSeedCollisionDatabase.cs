using System;

namespace oracleofages;

internal sealed class GaleSeedCollisionDatabase
{
    private readonly int[] _effects = new int[128];
    private readonly int[] _modes = new int[128];
    internal static GaleSeedCollisionDatabase Shared { get; } = new();
    private GaleSeedCollisionDatabase()
    {
        GeneratedTable table = GeneratedTable.Load("res://assets/oracle/metadata/gale_collision_effects.tsv",
            new GeneratedTableSchema("gale collisions", GeneratedTableKeySemantics.Unique,
                ["mode", "effect", "source"], ["mode"], headerRequired: true));
        if (table.Rows.Count != 128) throw new InvalidOperationException("Expected 128 gale collision modes.");
        foreach (GeneratedTableRow row in table.Rows) _effects[row.HexByte(0)] = row.HexByte(1);
        table = GeneratedTable.Load("res://assets/oracle/metadata/gale_enemy_modes.tsv",
            new GeneratedTableSchema("gale enemy modes", GeneratedTableKeySemantics.Unique,
                ["id", "mode", "source"], ["id"], headerRequired: true));
        if (table.Rows.Count != 128) throw new InvalidOperationException("Expected 128 source enemy modes.");
        foreach (GeneratedTableRow row in table.Rows) _modes[row.HexByte(0)] = row.HexByte(1);
    }
    internal int Effect(int mode) => _effects[mode & 0x7f];
    internal int EnemyMode(int id) => _modes[id];
}
