using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

internal sealed class VolcanoDatabase
{
    private readonly Lookup<(int Group, int Room), VolcanoPlacement> _placements = new();
    private readonly Dictionary<string, int> _constants = new();
    internal List<VolcanoStep> Steps { get; } = [];
    internal Dictionary<int, (int Y, int X)> ImpactRadii { get; } = new();
    internal string Sprite { get; }
    internal int TileBase { get; }
    internal int Palette { get; }
    internal int Radius { get; }
    internal int Damage { get; }
    internal string[] Animations { get; }

    internal VolcanoDatabase()
    {
        foreach (var row in Load("objects/volcano_placements.tsv", "group room order subid y x source", "group room order").Rows)
        {
            var record = new VolcanoPlacement(row.Decimal(0, 0, 7), row.HexByte(1),
                row.UnsignedDecimal(2), row.HexByte(3), row.HexByte(4), row.HexByte(5));
            if (record.SubId is not (0x05 or 0x06 or 0x13))
                throw row.Invalid(3, "miscellaneous2.s: supported volcano subid $05/$06/$13");
            _ = row.RequiredString(6);
            _placements.GetOrAdd((record.Group, record.Room)).Add(record);
        }
        foreach (var row in Load("effects/volcano_script.tsv", "index shake-y shake-x mask base source", "index").Rows)
        {
            if (row.UnsignedDecimal(0) != Steps.Count) throw row.Invalid(0, "volcanoHandler.s:@script source order");
            Steps.Add(new(row.Decimal(1, 0, 255), row.Decimal(2, 0, 255), row.Decimal(3, 0, 255), row.Decimal(4, 0, 255)));
            _ = row.RequiredString(5);
        }
        if (Steps.Count != 7) throw new InvalidOperationException("volcanoHandler.s:@script requires seven steps.");
        foreach (var row in Load("effects/volcano_constants.tsv", "key value", "key").Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1, 0, 255));
        foreach (var row in Load("effects/volcano_radii.tsv", "parameter y x", "parameter").Rows)
            ImpactRadii.Add(row.UnsignedDecimal(0), (row.UnsignedDecimal(1), row.UnsignedDecimal(2)));
        var visuals = Load("effects/volcano_rock.tsv", "sprite tile-base palette radius damage-quarters animations-base64 source", "sprite");
        if (visuals.Rows.Count != 1) throw new InvalidOperationException("PART_VOLCANO_ROCK $11: missing visual.");
        var visual = visuals.Rows[0];
        Sprite = visual.RequiredString(0);
        TileBase = visual.UnsignedDecimal(1);
        Palette = visual.Decimal(2, 0, 7);
        Radius = visual.UnsignedDecimal(3);
        Damage = visual.UnsignedDecimal(4);
        Animations = Encoding.UTF8.GetString(Convert.FromBase64String(visual.RequiredString(5))).Split('\n');
        _ = visual.RequiredString(6);
        if (Animations.Length != 4 || ImpactRadii.Count != 5 || _constants.Count != 5)
            throw new InvalidOperationException("volcanoRock.s: incomplete animation, collision, or native constants.");
    }

    internal IReadOnlyList<VolcanoPlacement> Placements(int group, int room) => _placements.ValuesOrEmpty((group, room));
    internal int Constant(string key) => _constants.TryGetValue(key, out int value) ? value :
        throw new InvalidOperationException($"volcanoHandler.s: missing '{key}'.");

    private static GeneratedTable Load(string path, string columns, string keys) => GeneratedTable.Load(
        "res://assets/oracle/" + path, new GeneratedTableSchema("volcano $dc/$b2/$11", GeneratedTableKeySemantics.Unique,
            columns.Split(' '), keys.Split(' '), headerRequired: true));
}

internal readonly record struct VolcanoPlacement(int Group, int Room, int Order, int SubId, int Y, int X);
internal readonly record struct VolcanoStep(int Y, int X, int Mask, int Base);
