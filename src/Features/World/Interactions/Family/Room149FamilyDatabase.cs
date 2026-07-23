using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported visuals and text used by the linked father/son/observer
/// interactions in past room $49.
/// </summary>
internal sealed class Room149FamilyDatabase
{
    private readonly Dictionary<string, Room149FamilyDatabaseVisualRecord> _visuals = new();
    private readonly Dictionary<int, string> _texts = new();

    public Color[] StonePalette { get; }

    public Room149FamilyDatabase()
    {
        GeneratedTable visuals = GeneratedTable.Load(
            "res://assets/oracle/objects/room149_family_visuals.tsv",
            new GeneratedTableSchema(
                "room 1:49 family visuals",
                GeneratedTableKeySemantics.Unique,
                ["key", "sprite", "tile-base", "palette", "animation"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in visuals.Rows)
        {
            string key = row.RequiredString(0);
            _visuals.Add(key, new Room149FamilyDatabaseVisualRecord(
                key, row.RequiredString(1), row.UnsignedDecimal(2),
                row.UnsignedDecimal(3), row.RequiredString(4)));
        }
        if (_visuals.Count != 6)
            throw new InvalidOperationException(
                $"Expected six room 1:49 visuals, got {_visuals.Count}.");

        GeneratedTable texts = GeneratedTable.Load(
            "res://assets/oracle/objects/room149_family_texts.tsv",
            new GeneratedTableSchema(
                "room 1:49 family text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in texts.Rows)
        {
            int id = row.HexWord(0);
            _texts.Add(id, row.Base64Utf8(1));
        }
        if (_texts.Count != 6)
            throw new InvalidOperationException(
                $"Expected six room 1:49 texts, got {_texts.Count}.");

        byte[] palette = FileAccess.GetFileAsBytes(
            "res://assets/oracle/cutscenes/nayru_stone_sprite_palette.bin");
        if (palette.Length != 12)
            throw new InvalidOperationException(
                $"PALH_a2 stone palette should contain 12 bytes, got {palette.Length}.");
        StonePalette = new Color[4];
        for (int color = 0; color < StonePalette.Length; color++)
        {
            int offset = color * 3;
            StonePalette[color] = new Color(
                palette[offset] / 31.0f,
                palette[offset + 1] / 31.0f,
                palette[offset + 2] / 31.0f,
                color == 0 ? 0.0f : 1.0f);
        }
    }

    public Room149FamilyDatabaseVisualRecord Visual(string key) =>
        _visuals.TryGetValue(key, out Room149FamilyDatabaseVisualRecord visual)
            ? visual
            : throw new InvalidOperationException(
                $"Unknown room 1:49 visual '{key}'.");

    public string Text(int id) => _texts.TryGetValue(id, out string? text)
        ? text
        : throw new InvalidOperationException(
            $"Room 1:49 text TX_{id:x4} was not imported.");
}

internal readonly record struct Room149FamilyDatabaseVisualRecord(string Key, string SpriteName, int TileBase, int Palette, string Animation);
