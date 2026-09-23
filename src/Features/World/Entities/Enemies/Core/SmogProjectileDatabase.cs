using Godot;
using System;

namespace oracleofages;

internal sealed class SmogProjectileDatabase
{
    internal string Sprite { get; }
    internal int TileBase { get; }
    internal int Palette { get; }
    internal Vector2I Radius { get; }
    internal int RawDamage { get; }
    internal int Health { get; }
    internal int InitialCollisionMode { get; }
    internal string[] Animations { get; } = new string[4];
    private readonly Vector2I[] _frontOffsets = new Vector2I[8];
    private readonly bool[] _enabled = new bool[32];
    private readonly int[] _smallEffects = new int[32], _largeEffects = new int[32];
    internal bool Enabled(int collision) => collision is >= 0 and < 32 && _enabled[collision];
    internal int Effect(int subid, int collision) => (subid == 0 ? _smallEffects : _largeEffects)[collision];
    internal Vector2I FrontOffset(int angle) => _frontOffsets[((angle + 2) & 0x1c) >> 2];

    internal SmogProjectileDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/effects/smog_projectile.tsv",
            new GeneratedTableSchema("PART_SMOG_PROJECTILE $4a", GeneratedTableKeySemantics.Unique,
                ["animation-index", "sprite", "tile-base", "palette", "radius-y", "radius-x",
                 "raw-damage", "health", "collision-mode", "animation", "source"], ["animation-index"], headerRequired: true));
        if (table.Rows.Count != 4) throw new InvalidOperationException("PART$4a requires four animations.");
        var first = table.Rows[0];
        Sprite = first.RequiredString(1); TileBase = first.UnsignedDecimal(2); Palette = first.UnsignedDecimal(3);
        Radius = new(first.UnsignedDecimal(5), first.UnsignedDecimal(4));
        RawDamage = first.HexByte(6); Health = first.UnsignedDecimal(7); InitialCollisionMode = first.HexByte(8);
        for (int i = 0; i < 4; i++)
        {
            var row = table.Rows[i];
            if (row.UnsignedDecimal(0) != i || row.RequiredString(1) != Sprite || row.UnsignedDecimal(2) != TileBase ||
                row.UnsignedDecimal(3) != Palette || row.UnsignedDecimal(4) != Radius.Y || row.UnsignedDecimal(5) != Radius.X ||
                row.HexByte(6) != RawDamage || row.UnsignedDecimal(7) != Health || row.HexByte(8) != InitialCollisionMode)
                throw row.Invalid(0, "ordered PART$4a animation with identical initial attributes");
            Animations[i] = row.RequiredString(9);
            _ = OracleGraphicsCache.GetAnimationDefinition(Animations[i]);
        }
        table = GeneratedTable.Load("res://assets/oracle/effects/smog_projectile_offsets.tsv",
            new GeneratedTableSchema("partCommon_anglePositionOffsets", GeneratedTableKeySemantics.Unique,
                ["direction", "y", "x", "source"], ["direction"], headerRequired: true));
        if (table.Rows.Count != 8) throw new InvalidOperationException("PART$4a requires eight front offsets.");
        for (int i = 0; i < 8; i++)
        {
            var row = table.Rows[i];
            if (row.UnsignedDecimal(0) != i) throw row.Invalid(0, "ordered front-probe direction");
            _frontOffsets[i] = new(unchecked((sbyte)row.HexByte(2)), unchecked((sbyte)row.HexByte(1)));
        }
        table = GeneratedTable.Load("res://assets/oracle/effects/smog_projectile_collisions.tsv",
            new GeneratedTableSchema("Smog projectile collisions", GeneratedTableKeySemantics.Unique,
                ["collision", "enabled", "small-effect", "large-effect", "source"], ["collision"], headerRequired: true));
        if (table.Rows.Count != 32) throw new InvalidOperationException("PART$4a requires $20 collision entries.");
        for (int i = 0; i < 32; i++)
        {
            var row = table.Rows[i];
            if (row.HexByte(0) != i) throw row.Invalid(0, "ordered item collision type");
            _enabled[i] = row.Decimal(1,0,1) != 0;
            _smallEffects[i] = row.HexByte(2); _largeEffects[i] = row.HexByte(3);
        }
    }
}
