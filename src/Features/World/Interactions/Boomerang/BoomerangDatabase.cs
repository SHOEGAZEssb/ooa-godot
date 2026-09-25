using Godot;
using System;

namespace oracleofages;

internal sealed class BoomerangDatabase
{
    internal static BoomerangDatabase Shared { get; } = new();
    internal int Speed { get; }
    internal int OutwardUpdates { get; }
    internal int CatchUpdates { get; }
    internal int NearRadius { get; }
    internal int CatchRadius { get; }
    internal int Sound { get; }
    internal int Collision { get; }
    internal Vector2I Radius { get; }
    internal int Damage { get; }
    internal int OamFlags { get; }
    internal bool SourceInverted { get; }
    internal string Animation { get; }

    private BoomerangDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/boomerang.tsv",
            new GeneratedTableSchema("ITEM_BOOMERANG $06", GeneratedTableKeySemantics.Ordered,
                ["speed-raw", "outward-updates", "catch-updates", "near-radius", "catch-radius", "sound",
                 "collision", "radius-y", "radius-x", "damage", "oam-flags", "source-inverted", "animation", "source"],
                headerRequired: true));
        GeneratedTableRow row = table.SingleRow();
        Speed = row.Decimal(0, 1, 255);
        OutwardUpdates = row.Decimal(1, 1, 255);
        CatchUpdates = row.Decimal(2, 1, 255);
        NearRadius = row.Decimal(3, 1, 127);
        CatchRadius = row.Decimal(4, 1, 127);
        Sound = row.HexByte(5);
        Collision = row.HexByte(6);
        Radius = new(row.Decimal(8, 0, 15), row.Decimal(7, 0, 15));
        Damage = -unchecked((sbyte)row.HexByte(9));
        OamFlags = row.HexByte(10);
        SourceInverted = row.Decimal(11, 0, 1) != 0;
        Animation = row.RequiredString(12);
        _ = row.RequiredString(13);
    }
}
