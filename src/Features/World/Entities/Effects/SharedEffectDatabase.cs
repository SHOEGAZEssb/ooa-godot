using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SharedEffectDatabase
{
    private const string Path = "res://assets/oracle/cutscenes/nayru_intro_effects.tsv";
    private readonly Dictionary<string, EffectRecord> _effects = new(StringComparer.Ordinal);

    internal SharedEffectDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            Path,
            new GeneratedTableSchema(
                "shared object effects",
                GeneratedTableKeySemantics.Unique,
                [
                    "name", "sprite", "tile-base", "palette", "duration", "speed", "angle",
                    "sway", "velocity-x-fixed", "velocity-y-fixed", "animation"
                ],
                ["name"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var effect = new EffectRecord(
                row.RequiredString(0), row.RequiredString(1),
                row.UnsignedDecimal(2), row.UnsignedDecimal(3), row.UnsignedDecimal(4),
                row.FiniteFloat(5), row.Decimal(6), row.Boolean01(7),
                row.Decimal(8), row.Decimal(9), row.RequiredString(10));
            _effects.Add(effect.Name, effect);
        }
        if (_effects.Count != 2)
            throw new InvalidOperationException(
                $"{Path}: expected 2 shared object effect templates, got {_effects.Count}.");
    }

    internal EffectRecord Effect(string name) =>
        _effects.TryGetValue(name, out EffectRecord effect)
            ? effect
            : throw new InvalidOperationException($"{Path}: missing shared object effect '{name}'.");
}

public readonly record struct EffectRecord(string Name, string SpriteName, int TileBase, int Palette, int Duration, float Speed, int Angle, bool Sway, int VelocityXFixed, int VelocityYFixed, string Animation)
{
    public NpcRecord ToNpcRecord(int group, int room, int y, int x) => new(group, room, 0, 0, y, x, 0, 0, SpriteName, TileBase, Palette, 0, false, Animation, Animation, Animation, Animation, string.Empty, NpcImplementationClassification.EventOwned);
}
