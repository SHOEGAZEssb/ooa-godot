using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported graphics and animation records owned by globally dispatched
/// dungeon interactions rather than by the first dungeon that uses them.
/// </summary>
internal sealed class DungeonInteractionVisualDatabase
{
    private readonly Dictionary<string, DungeonInteractionVisual> _visuals =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, Color[]> _cubePalettes = new();

    internal DungeonInteractionVisualDatabase()
    {
        LoadVisuals();
        LoadCubePalettes();
        ValidateContract();
    }

    internal DungeonInteractionVisual Visual(string key) =>
        _visuals.TryGetValue(key, out DungeonInteractionVisual record)
            ? record
            : throw new KeyNotFoundException(
                $"Dungeon interaction visual {key} was not imported.");

    internal IReadOnlyDictionary<int, Color[]> CubePalettes => _cubePalettes;

    private void LoadVisuals()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_interaction_visuals.tsv",
            new GeneratedTableSchema(
                "shared dungeon interaction visuals",
                GeneratedTableKeySemantics.Unique,
                [
                    "key", "sprites", "tile-base", "palette",
                    "source-grayscale-inverted", "animations-base64"
                ],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            DungeonInteractionVisual record = new(
                row.RequiredString(0),
                SplitRequired(row, 1, ','),
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3),
                row.Boolean01(4),
                SplitDecoded(row, 5));
            if (!_visuals.TryAdd(record.Key, record))
            {
                throw row.Invalid(
                    0,
                    "a unique shared dungeon interaction visual");
            }
        }
    }

    private void LoadCubePalettes()
    {
        Color[,] palettes = OracleGraphicsData.LoadPalette(
            "res://assets/oracle/objects/colored_cube_palettes.bin", 2, 6);
        for (int palette = 6; palette <= 7; palette++)
        {
            var colors = new Color[4];
            for (int shade = 0; shade < colors.Length; shade++)
                colors[shade] = palettes[palette, shade];
            _cubePalettes.Add(palette, colors);
        }
    }

    private void ValidateContract()
    {
        if (_visuals.Count != 19 ||
            Visual("colored-cube").Animations.Length != 30 ||
            Visual("eternal-spirit") is not
                { TileBase: 0, Palette: 1, Animations.Length: 1 } ||
            Visual("ancient-wood") is not
                { TileBase: 4, Palette: 0, Animations.Length: 1 } ||
            Visual("moving-side-platform").Animations.Length != 5 ||
            Visual("circular-side-platform").Animations.Length != 1 ||
            Visual("minecart").Animations.Length != 4 ||
            Visual("minecart-gate") is not
                { TileBase: 0x10, Palette: 0, Animations.Length: 4 } ||
            Visual("head-thwomp-fireball") is not
                { TileBase: 0, Palette: 2, Animations.Length: 1 } ||
            Visual("head-thwomp-fireball-impact") is not
                { TileBase: 0x26, Palette: 3, Animations.Length: 1 } ||
            Visual("head-thwomp-circular-projectile") is not
                { TileBase: 0x14, Palette: 2, Animations.Length: 1 } ||
            Visual("head-thwomp-boulder") is not
                { TileBase: 0, Palette: 5, Animations.Length: 1 } ||
            Visual("head-thwomp-boulder-impact") is not
                { TileBase: 2, Palette: 3, Animations.Length: 1 } ||
            Visual("essence-pedestal") is not
                { TileBase: 0, Palette: 4, Animations.Length: 1 } ||
            Visual("essence-glow") is not
                { TileBase: 6, Palette: 4, Animations.Length: 1 } ||
            Visual("energy-bead").Animations.Length != 8 ||
            Visual("pumpkin-projectile").Animations.Length != 1 ||
            _cubePalettes.Count != 2)
        {
            throw new InvalidOperationException(
                "Imported shared dungeon interaction visuals are incomplete.");
        }
    }

    private static string[] SplitRequired(
        GeneratedTableRow row,
        int column,
        char separator)
    {
        string[] values = row.RequiredString(column).Split(
            separator,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        if (values.Length == 0)
            throw row.Invalid(column, "one or more values");
        return values;
    }

    private static string[] SplitDecoded(GeneratedTableRow row, int column)
    {
        string[] values = row.Base64Utf8(column).Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries);
        if (values.Length == 0)
            throw row.Invalid(column, "one or more encoded animations");
        return values;
    }
}

internal readonly record struct DungeonInteractionVisual(
    string Key,
    string[] Sprites,
    int TileBase,
    int Palette,
    bool SourceGrayscaleInverted,
    string[] Animations);
