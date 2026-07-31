using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Shared imported definitions for implemented dungeon bosses and minibosses.
/// Species records are keyed by enemy ID/subid, independent of the first
/// dungeon route that made them playable.
/// </summary>
internal sealed class DungeonBossDatabase
{
    private readonly Dictionary<(int Id, int SubId), ImportedEnemyDefinition>
        _enemies = new();
    private readonly Dictionary<string, int> _constants =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, Color[]> _headThwompPalettes = new();

    internal DungeonBossDatabase()
    {
        LoadEnemies();
        LoadConstants();
        LoadHeadThwompPalettes();
        ValidateContract();
    }

    internal ImportedEnemyDefinition Enemy(int id, int subId = 0) =>
        _enemies.TryGetValue((id, subId), out ImportedEnemyDefinition record)
            ? record
            : throw new KeyNotFoundException(
                $"Dungeon boss ${id:x2}:${subId:x2} was not imported.");

    internal IReadOnlyDictionary<int, Color[]> HeadThwompPalettes =>
        _headThwompPalettes;

    internal int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Dungeon boss constant {key} was not imported.");

    private void LoadEnemies()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_bosses.tsv",
            new GeneratedTableSchema(
                "implemented dungeon bosses",
                GeneratedTableKeySemantics.Unique,
                [
                    "id", "subid", "sprites", "tile-base", "palette",
                    "source-grayscale-inverted", "radius-y", "radius-x",
                    "damage-quarters", "health", "animations-base64"
                ],
                ["id", "subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            ImportedEnemyDefinition record = new(
                row.HexByte(0),
                row.HexByte(1),
                SplitRequired(row, 2, ','),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                row.Boolean01(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.UnsignedDecimal(9),
                SplitDecoded(row, 10));
            if (!_enemies.TryAdd((record.Id, record.SubId), record))
            {
                throw row.Invalid(0, "a unique dungeon boss ID/subid");
            }
        }
    }

    private void LoadHeadThwompPalettes()
    {
        Color[,] palettes = OracleGraphicsData.LoadPalette(
            "res://assets/oracle/objects/dungeon_head_thwomp_palette.bin",
            1,
            6);
        var colors = new Color[4];
        for (int shade = 0; shade < colors.Length; shade++)
            colors[shade] = palettes[6, shade];
        _headThwompPalettes.Add(6, colors);
    }

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_boss_constants.tsv",
            new GeneratedTableSchema(
                "implemented dungeon boss constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (!_constants.TryAdd(
                    row.RequiredString(0),
                    row.UnsignedDecimal(1)))
            {
                throw row.Invalid(0, "a unique dungeon boss constant");
            }
        }
    }

    private void ValidateContract()
    {
        if (_enemies.Count != 5 ||
            Enemy(0x3f) is not
                { Health: 2, DamageQuarters: 128, Sprites.Length: 2 } ||
            Enemy(0x70) is not
                { Health: 12, DamageQuarters: 1, Sprites.Length: 2 } ||
            Enemy(0x71).Sprites is not ["spr_swoop", "spr_pound"] ||
            Enemy(0x78) is not { Health: 8, Sprites.Length: 3 } ||
            Enemy(0x79).Sprites.Length != 3 ||
            _constants.Count != 2 ||
            Constant("pumpkin-body-palette") != 1 ||
            Constant("pumpkin-ghost-palette") != 5 ||
            _headThwompPalettes.Count != 1)
        {
            throw new InvalidOperationException(
                "Imported dungeon boss contract is incomplete.");
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
