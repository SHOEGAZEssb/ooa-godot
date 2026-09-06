using Godot;
using System.Linq;

namespace oracleofages;

internal sealed class TokayRescueEmberDatabase
{
    internal TokayRescueEmberRecord Record { get; }
    internal Vector2[] Positions { get; }

    internal TokayRescueEmberDatabase()
    {
        var row = GeneratedTable.Load("res://assets/oracle/objects/tokay_rescue_ember.tsv",
            new GeneratedTableSchema("Tokay rescue Ember Seed $8f", GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "seed-animation", "flame-sprite", "flame-tile-base",
                 "flame-palette", "flame-animation", "speed-z", "gravity", "flame-counter", "source"],
                headerRequired: true)).SingleRow();
        Record = new(row.RequiredString(0), row.HexByte(1), row.HexByte(2), row.RequiredString(3),
            row.RequiredString(4), row.HexByte(5), row.HexByte(6), row.RequiredString(7),
            row.Decimal(8), row.UnsignedDecimal(9), row.UnsignedDecimal(10));
        Positions = GeneratedTable.Load("res://assets/oracle/objects/tokay_rescue_ember_placements.tsv",
            new GeneratedTableSchema("Tokay rescue Ember Seed placements", GeneratedTableKeySemantics.Ordered,
                ["order", "y", "x", "source"], headerRequired: true)).Rows
            .Select(placement => new Vector2(placement.HexByte(2), placement.HexByte(1))).ToArray();
    }
}

internal sealed record TokayRescueEmberRecord(string Sprite, int TileBase, int Palette, string SeedAnimation,
    string FlameSprite, int FlameTileBase, int FlamePalette, string FlameAnimation,
    int SpeedZ, int Gravity, int FlameCounter);
