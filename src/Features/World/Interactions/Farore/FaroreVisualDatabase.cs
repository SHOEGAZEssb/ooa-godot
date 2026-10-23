using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class FaroreVisualDatabase
{
    private readonly Dictionary<(int, int), (NpcRecord Record, int Offset)> _visuals = [];
    internal IReadOnlyList<FaroreSparkleRecord> Sparkles { get; }
    internal InteractionSparkleVisual UpgradeSparkle
    {
        get
        {
            NpcRecord record = _visuals[(InteractionId.Sparkle, 0)].Record;
            return new(record.SpriteName, record.TileBase, record.Palette, record.DownAnimation);
        }
    }

    internal FaroreVisualDatabase()
    {
        const string root = "res://assets/oracle/cutscenes/";
        var sparkles = new List<FaroreSparkleRecord>();
        foreach (var row in GeneratedTable.Load(root + "farore_sparkles.tsv",
            new("Farore sparkle objects", GeneratedTableKeySemantics.Ordered,
                ["subid", "y", "x", "angle", "source"], headerRequired: true)).Rows)
            sparkles.Add(new(row.HexByte(0), new(row.HexByte(2), row.HexByte(1)), row.HexByte(3)));
        if (sparkles.Count != 8) throw new System.InvalidOperationException("faroreMakeChest.s: missing eight sparkle objects.");
        Sparkles = sparkles;
        foreach (var row in GeneratedTable.Load(root + "farore_visuals.tsv",
            new("Farore cutscene graphics", GeneratedTableKeySemantics.Unique,
                ["id", "subid", "sprite", "tile-base", "palette", "animation", "source-offset", "source"],
                ["id", "subid"], headerRequired: true)).Rows)
        {
            int id = row.HexByte(0), subid = row.HexByte(1);
            string animation = row.RequiredString(5);
            _visuals.Add((id, subid), (new(5, 0xcf, id, subid, 0, 0, 0, 0, row.RequiredString(2),
                row.UnsignedDecimal(3), row.UnsignedDecimal(4), 0, false,
                animation, animation, animation, animation, "", NpcImplementationClassification.EventOwned), row.HexWord(6)));
        }
    }

    internal NpcCharacter Actor(int id, int subid, Vector2 position)
    {
        var (record, offset) = _visuals[(id, id == InteractionId.FaroreMakeChest ? 1 : subid)];
        var actor = NpcCharacter.CreateFromRecord(record with { SubId = subid, X = (int)position.X, Y = (int)position.Y });
        actor.SetGraphicsSourceOffset(offset);
        actor.SetScriptAnimation(record.DownAnimation);
        actor.SetAnimationRate(0);
        actor.SetBlocksLink(false);
        return actor;
    }
}

internal readonly record struct FaroreSparkleRecord(int SubId, Vector2 Position, int Angle);
