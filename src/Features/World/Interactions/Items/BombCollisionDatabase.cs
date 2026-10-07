using System;

namespace oracleofages;

// ITEM$03/$0d share ITEMCOLLISION_BOMB $18; target IDs and modes are independent.
internal sealed class BombCollisionDatabase
{
    internal static BombCollisionDatabase Shared { get; } = new();
    private readonly int[] _effects = new int[0x7d];
    private readonly bool[] _enemies = new bool[128], _parts = new bool[0x5a];

    private BombCollisionDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/bomb_collision_effects.tsv",
            new GeneratedTableSchema("ITEMCOLLISION_BOMB $18", GeneratedTableKeySemantics.Unique,
                ["mode", "effect", "source"], ["mode"], headerRequired: true));
        if (table.Rows.Count != _effects.Length) throw new InvalidOperationException("ITEMCOLLISION_BOMB requires $7d collision modes.");
        for (int index = 0; index < _effects.Length; index++)
        {
            var row = table.Rows[index];
            if (row.HexByte(0) != index) throw row.Invalid(0, "ordered collision modes");
            _effects[index] = row.HexByte(1); _ = row.RequiredString(2);
        }
        LoadMasks("enemy", _enemies); LoadMasks("part", _parts);
    }

    private static void LoadMasks(string kind, bool[] masks)
    {
        var table = GeneratedTable.Load($"res://assets/oracle/metadata/bomb_{kind}_collisions.tsv",
            new GeneratedTableSchema($"ITEMCOLLISION_BOMB {kind} masks", GeneratedTableKeySemantics.Unique,
                ["id", "enabled", "source"], ["id"], headerRequired: true));
        if (table.Rows.Count != masks.Length) throw new InvalidOperationException($"Incomplete ITEMCOLLISION_BOMB {kind} masks.");
        for (int index = 0; index < masks.Length; index++)
        {
            var row = table.Rows[index];
            if (row.HexByte(0) != index) throw row.Invalid(0, "ordered target collision types");
            masks[index] = row.Decimal(1, 0, 1) != 0; _ = row.RequiredString(2);
        }
    }

    internal bool EnemyEnabled(int type) => type >= 0 && _enemies[type & 0x7f];
    internal bool PartEnabled(int type) => type is >= 0 and < 0x5a && _parts[type];
    internal int Effect(int mode) => (mode & 0x7f) < _effects.Length ? _effects[mode & 0x7f] :
        throw new NotSupportedException($"objectCollisionTable has no ITEMCOLLISION_BOMB mode ${(mode & 0x7f):x2}.");
}
