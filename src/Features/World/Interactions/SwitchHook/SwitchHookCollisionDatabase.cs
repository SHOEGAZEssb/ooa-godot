using Godot;
using System;

namespace oracleofages;

internal sealed class SwitchHookCollisionDatabase
{
    internal static SwitchHookCollisionDatabase Shared { get; } = new();
    private readonly int[] _effects = new int[0x7d];
    private readonly bool[] _enemies = new bool[128];
    private SwitchHookCollisionDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/switch_hook_collision_effects.tsv",
            new GeneratedTableSchema("Switch Hook collision effects", GeneratedTableKeySemantics.Unique,
                ["mode", "effect", "source"], ["mode"], headerRequired: true));
        if (table.Rows.Count != 0x7d) throw new InvalidOperationException("Expected 125 vanilla Switch Hook collision modes.");
        foreach (var row in table.Rows) _effects[row.HexByte(0)] = row.HexByte(1);
        table = GeneratedTable.Load("res://assets/oracle/metadata/switch_hook_enemy_collisions.tsv",
            new GeneratedTableSchema("Switch Hook enemy eligibility", GeneratedTableKeySemantics.Unique,
                ["id", "enabled", "source"], ["id"], headerRequired: true));
        if (table.Rows.Count != 128) throw new InvalidOperationException("Expected 128 Switch Hook enemy masks.");
        foreach (var row in table.Rows) _enemies[row.HexByte(0)] = row.Decimal(1, 0, 1) != 0;
    }
    internal bool EnemyEnabled(int collisionType) => collisionType >= 0 && _enemies[collisionType & 0x7f];
    internal int Effect(int mode) => (mode & 0x7f) < _effects.Length
        ? _effects[mode & 0x7f]
        : throw new NotSupportedException($"Vanilla objectCollisionTable has no mode ${(mode & 0x7f):x2}.");
}
