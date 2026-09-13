using Godot;
using System;

namespace oracleofages;

internal sealed class SwitchHookCollisionDatabase
{
    internal static SwitchHookCollisionDatabase Shared { get; } = new();
    private readonly int[] _effects = new int[128];
    private readonly bool[] _enemies = new bool[128];
    private readonly Vector2I[,] _probes = new Vector2I[2, 8];
    private SwitchHookCollisionDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/switch_hook_collision_effects.tsv",
            new GeneratedTableSchema("Switch Hook collision effects", GeneratedTableKeySemantics.Unique,
                ["mode", "effect", "source"], ["mode"], headerRequired: true));
        if (table.Rows.Count != 128) throw new InvalidOperationException("Expected 128 Switch Hook collision modes.");
        foreach (var row in table.Rows) _effects[row.HexByte(0)] = row.HexByte(1);
        table = GeneratedTable.Load("res://assets/oracle/metadata/switch_hook_enemy_collisions.tsv",
            new GeneratedTableSchema("Switch Hook enemy eligibility", GeneratedTableKeySemantics.Unique,
                ["id", "enabled", "source"], ["id"], headerRequired: true));
        if (table.Rows.Count != 128) throw new InvalidOperationException("Expected 128 Switch Hook enemy masks.");
        foreach (var row in table.Rows) _enemies[row.HexByte(0)] = row.Decimal(1, 0, 1) != 0;
        table = GeneratedTable.Load("res://assets/oracle/metadata/switch_hook_wall_probes.tsv",
            new GeneratedTableSchema("Switch Hook wall probes", GeneratedTableKeySemantics.Unique,
                ["sidescroll", "index", "y", "x", "source"], ["sidescroll", "index"], headerRequired: true));
        if (table.Rows.Count != 16) throw new InvalidOperationException("Expected two sets of eight Link wall probes.");
        foreach (var row in table.Rows)
            _probes[row.Decimal(0, 0, 1), row.Decimal(1, 0, 7)] = new(row.Decimal(3, -128, 127), row.Decimal(2, -128, 127));
    }
    internal bool EnemyEnabled(int collisionType) => collisionType >= 0 && _enemies[collisionType & 0x7f];
    internal int Effect(int mode) => _effects[mode & 0x7f];
    internal bool SurroundedByWalls(Vector2 position, bool sidescroll, Func<Vector2, bool> solid)
    {
        // Link's cumulative eight probes, followed by four pairs of RRA/RL.
        // A direction is blocked if either of its two probes hits a wall.
        var point = (Vector2I)position.Floor();
        int bits = 0;
        for (int i = 0; i < 8; i++)
        {
            point += _probes[sidescroll ? 1 : 0, i];
            point = new(point.X & 0xff, point.Y & 0xff);
            bits = (bits << 1) | (solid(point) ? 1 : 0);
        }
        return (bits & 0xc0) != 0 && (bits & 0x30) != 0 && (bits & 0x0c) != 0 && (bits & 3) != 0;
    }
}
