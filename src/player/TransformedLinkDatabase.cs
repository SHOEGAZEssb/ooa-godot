using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed runtime view of special objects $03-$07, selected by the five
/// transformation rings. Frames retain their source GFX pointer and shared
/// special-object OAM composition.
/// </summary>
internal sealed class TransformedLinkDatabase
{
    internal const int ExpectedFrameCount = 5 * 4 * 2;

    internal readonly record struct FrameRecord(
        int Ring,
        int SpecialObject,
        int Direction,
        int Frame,
        string Sprite,
        int TileBase,
        string Oam,
        int InitialDuration,
        int LoopDuration);

    private readonly Dictionary<(int SpecialObject, int Direction, int Frame), FrameRecord>
        _records = new();
    private readonly Dictionary<string, Image> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<(int SpecialObject, int Direction, int Frame), Texture2D>
        _textures = new();

    internal IReadOnlyCollection<FrameRecord> Records => _records.Values;

    internal TransformedLinkDatabase(
        string path = "res://assets/oracle/metadata/transformed_link.tsv")
    {
        string text = FileAccess.GetFileAsString(path);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"Missing transformed-Link data: {path}");

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#')
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 9)
                throw new InvalidOperationException(
                    $"Malformed transformed-Link row in {path}: {line}");
            var record = new FrameRecord(
                ParseHex(fields[0], "ring"),
                ParseHex(fields[1], "special object"),
                ParseDecimal(fields[2], "direction"),
                ParseDecimal(fields[3], "frame"),
                fields[4],
                ParseDecimal(fields[5], "tile base"),
                fields[6],
                ParseDecimal(fields[7], "initial duration"),
                ParseDecimal(fields[8], "loop duration"));
            ValidateRecord(record);
            if (!_records.TryAdd(
                (record.SpecialObject, record.Direction, record.Frame), record))
            {
                throw new InvalidOperationException(
                    $"Duplicate transformed-Link frame {record.SpecialObject:x2}:" +
                    $"{record.Direction}:{record.Frame} in {path}.");
            }
        }

        if (_records.Count != ExpectedFrameCount)
            throw new InvalidOperationException(
                $"Expected {ExpectedFrameCount} transformed-Link frames, got {_records.Count}.");
        foreach (int specialObject in new[] { 3, 4, 5, 6, 7 })
        for (int direction = 0; direction < 4; direction++)
        for (int frame = 0; frame < 2; frame++)
        {
            if (!_records.ContainsKey((specialObject, direction, frame)))
            {
                throw new InvalidOperationException(
                    $"Missing transformed-Link frame {specialObject:x2}:{direction}:{frame}.");
            }
        }
    }

    internal FrameRecord Record(int specialObject, int direction, int frame) =>
        _records.TryGetValue((specialObject, direction & 3, frame & 1), out FrameRecord record)
            ? record
            : throw new ArgumentOutOfRangeException(
                nameof(specialObject),
                $"Unknown transformed-Link frame {specialObject:x2}:{direction}:{frame}.");

    internal Texture2D Texture(int specialObject, int direction, int frame)
    {
        var key = (specialObject, direction & 3, frame & 1);
        if (_textures.TryGetValue(key, out Texture2D? texture))
            return texture;
        FrameRecord record = Record(key.Item1, key.Item2, key.Item3);
        if (!_images.TryGetValue(record.Sprite, out Image? image))
        {
            image = OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{record.Sprite}.png");
            _images.Add(record.Sprite, image);
        }
        texture = NpcCharacter.BuildOamTexture(
            image, record.Oam, record.TileBase, basePalette: 0);
        _textures.Add(key, texture);
        return texture;
    }

    private static void ValidateRecord(FrameRecord record)
    {
        int expectedRing = record.SpecialObject switch
        {
            3 => (int)RingId.Subrosian,
            4 => (int)RingId.FirstGen,
            5 => (int)RingId.Octo,
            6 => (int)RingId.Moblin,
            7 => (int)RingId.LikeLike,
            _ => -1
        };
        if (record.Ring != expectedRing || record.Direction is < 0 or > 3 ||
            record.Frame is < 0 or > 1 || record.TileBase < 0 ||
            record.Sprite.Length == 0 || record.Oam.Length == 0 ||
            record.InitialDuration != 2 || record.LoopDuration != 6)
        {
            throw new InvalidOperationException(
                $"Invalid transformed-Link frame {record.SpecialObject:x2}:" +
                $"{record.Direction}:{record.Frame}.");
        }
    }

    private static int ParseHex(string value, string name) =>
        int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out int parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Invalid transformed-Link {name} value '{value}'.");

    private static int ParseDecimal(string value, string name) =>
        int.TryParse(value, out int parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"Invalid transformed-Link {name} value '{value}'.");
}
