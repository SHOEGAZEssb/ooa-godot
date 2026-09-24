using System;

namespace oracleofages;

internal sealed class BoomerangCollisionDatabase
{
    internal static BoomerangCollisionDatabase Shared { get; } = new();
    private readonly int[] _effects = new int[128];
    private readonly bool[] _enemies = new bool[128];
    private readonly bool[] _parts = new bool[0x5a];
    internal int StunCounter { get; }
    internal int StunInvincibility { get; }
    internal int DeflectionInvincibility { get; }

    private BoomerangCollisionDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/boomerang_collision_effects.tsv",
            new GeneratedTableSchema("ITEMCOLLISION_L1_BOOMERANG $17", GeneratedTableKeySemantics.Unique,
                ["mode", "effect", "source"], ["mode"], headerRequired: true));
        if (table.Rows.Count != 128) throw new InvalidOperationException("Expected $80 boomerang collision modes.");
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            if (row.HexByte(0) != i) throw row.Invalid(0, "ordered collision modes");
            _effects[i] = row.HexByte(1);
            _ = row.RequiredString(2);
        }
        LoadMasks("enemy", _enemies);
        LoadMasks("part", _parts);
        table = GeneratedTable.Load("res://assets/oracle/metadata/boomerang_status.tsv",
            new GeneratedTableSchema("Boomerang damage status", GeneratedTableKeySemantics.Unique,
                ["damage-type", "flags", "invincibility", "knockback", "stun", "source"],
                ["damage-type"], headerRequired: true));
        if (table.Rows.Count != 2 || table.Rows[0].HexByte(0) != 0x24 || table.Rows[1].HexByte(0) != 0x28 ||
            table.Rows[0].HexByte(1) != 0x29 || table.Rows[1].HexByte(1) != 0x60 ||
            table.Rows[0].HexByte(3) != 0 || table.Rows[1].HexByte(3) != 0 || table.Rows[1].HexByte(4) != 0)
            throw new InvalidOperationException("ENEMYDMG_$24/$28 boomerang status contracts changed.");
        StunCounter = table.Rows[0].HexByte(4);
        StunInvincibility = unchecked((sbyte)table.Rows[0].HexByte(2));
        DeflectionInvincibility = unchecked((sbyte)table.Rows[1].HexByte(2));
    }

    private static void LoadMasks(string kind, bool[] masks)
    {
        var table = GeneratedTable.Load($"res://assets/oracle/metadata/boomerang_{kind}_collisions.tsv",
            new GeneratedTableSchema($"Boomerang {kind} masks", GeneratedTableKeySemantics.Unique,
                ["id", "enabled", "source"], ["id"], headerRequired: true));
        if (table.Rows.Count != masks.Length) throw new InvalidOperationException($"Incomplete boomerang {kind} masks.");
        for (int i = 0; i < masks.Length; i++)
        {
            var row = table.Rows[i];
            if (row.HexByte(0) != i) throw row.Invalid(0, "ordered collision types");
            masks[i] = row.Decimal(1, 0, 1) != 0;
            _ = row.RequiredString(2);
        }
    }
    internal bool EnemyEnabled(int type) => type >= 0 && _enemies[type & 0x7f];
    internal bool PartEnabled(int type) => type is >= 0 and < 0x5a && _parts[type];
    internal int Effect(int mode) => _effects[mode & 0x7f];
}
