using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

/// <summary>
/// Imported visuals and text used by the linked father/son/observer
/// interactions in past room $49.
/// </summary>
internal sealed class Room149FamilyDatabase
{
    private readonly Dictionary<string, VisualRecord> _visuals = new();
    private readonly Dictionary<int, string> _texts = new();

    public Color[] StonePalette { get; }

    public Room149FamilyDatabase()
    {
        foreach (string line in ReadRows(
            "res://assets/oracle/objects/room149_family_visuals.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 5)
                throw new InvalidOperationException(
                    $"Malformed room 1:49 visual row: {line}");
            _visuals.Add(columns[0], new VisualRecord(
                columns[0],
                columns[1],
                int.Parse(columns[2]),
                int.Parse(columns[3]),
                columns[4]));
        }
        if (_visuals.Count != 6)
            throw new InvalidOperationException(
                $"Expected six room 1:49 visuals, got {_visuals.Count}.");

        foreach (string line in ReadRows(
            "res://assets/oracle/objects/room149_family_texts.tsv"))
        {
            string[] columns = line.Split('\t');
            if (columns.Length != 2)
                throw new InvalidOperationException(
                    $"Malformed room 1:49 text row: {line}");
            int id = Convert.ToInt32(columns[0], 16);
            _texts.Add(id, Encoding.UTF8.GetString(
                Convert.FromBase64String(columns[1])));
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

    public VisualRecord Visual(string key) =>
        _visuals.TryGetValue(key, out VisualRecord visual)
            ? visual
            : throw new InvalidOperationException(
                $"Unknown room 1:49 visual '{key}'.");

    public string Text(int id) => _texts.TryGetValue(id, out string? text)
        ? text
        : throw new InvalidOperationException(
            $"Room 1:49 text TX_{id:x4} was not imported.");

    private static IEnumerable<string> ReadRows(string path)
    {
        string source = FileAccess.GetFileAsString(path);
        foreach (string rawLine in source.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
                yield return line;
        }
    }

    internal readonly record struct VisualRecord(
        string Key,
        string SpriteName,
        int TileBase,
        int Palette,
        string Animation);
}
