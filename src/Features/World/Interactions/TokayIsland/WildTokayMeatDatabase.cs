using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-derived INTERAC_TOKAY_MEAT physics, sound, and visual contract.
/// </summary>
internal sealed class WildTokayMeatDatabase
{
    private readonly Dictionary<string, WildTokayMeatConstant> _constants =
        new(StringComparer.Ordinal);

    internal int StartY => Constant("meat-start-y");
    internal int StartX => Constant("meat-start-x");
    internal int StartZ => Constant("meat-start-z");
    internal int FallDelay => Constant("meat-fall-delay");
    internal int FallGravity => Constant("meat-fall-gravity");
    internal int CollisionRadius => Constant("meat-collision-radius");
    internal int DropLife => Constant("meat-drop-life");
    internal int SoundFall => Constant("sound-fall");
    internal int SoundLand => Constant("sound-land");
    internal string Sprite => TextConstant("meat-sprite");
    internal int TileBase => Constant("meat-tile-base");
    internal int Palette => Constant("meat-palette");
    internal string Animation => TextConstant("meat-animation");

    internal WildTokayMeatDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wild_tokay_meat_constants.tsv",
            new GeneratedTableSchema(
                "Wild Tokay meat constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value", "text"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _constants.Add(
                row.RequiredString(0),
                new WildTokayMeatConstant(
                    row.Decimal(1), row.RequiredString(2)));
        }
        Validate();
    }

    private int Constant(string key) =>
        _constants.TryGetValue(key, out WildTokayMeatConstant value)
            ? value.Value
            : throw new KeyNotFoundException(
                $"Wild Tokay meat constant '{key}' was not imported.");

    private string TextConstant(string key) =>
        _constants.TryGetValue(key, out WildTokayMeatConstant value) &&
            value.Text != "-"
            ? value.Text
            : throw new KeyNotFoundException(
                $"Wild Tokay meat text constant '{key}' was not imported.");

    private void Validate()
    {
        if (StartY != 0x38 || StartX != 0x50 || StartZ != -0x40 ||
            FallDelay != 30 || FallGravity != 0x28 ||
            CollisionRadius != 8 || DropLife != 20 ||
            string.IsNullOrWhiteSpace(Sprite) ||
            string.IsNullOrWhiteSpace(Animation))
        {
            throw new InvalidOperationException(
                "INTERAC_TOKAY_MEAT generated data does not match the traced source contract.");
        }
    }
}

internal readonly record struct WildTokayMeatConstant(int Value, string Text);
