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

    private readonly Dictionary<(int SpecialObject, int Direction, int Frame), FrameRecord>
        _records = new();
    private readonly Dictionary<string, Image> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<
        (int SpecialObject, int Direction, int Frame, bool DamagePalette),
        Texture2D>
        _textures = new();

    internal IReadOnlyCollection<FrameRecord> Records => _records.Values;

    internal TransformedLinkDatabase(
        string path = "res://assets/oracle/metadata/transformed_link.tsv")
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "transformed Link frames",
                GeneratedTableKeySemantics.Unique,
                [
                    "ring", "special-object", "direction", "frame", "sprite",
                    "tile-base", "oam", "initial-duration", "loop-duration"
                ],
                ["special-object", "direction", "frame"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            FrameRecord record = new FrameRecord(
                row.HexByte(0),
                row.HexByte(1),
                row.Decimal(2, 0, 3),
                row.Decimal(3, 0, 1),
                row.RequiredString(4),
                row.UnsignedDecimal(5),
                row.RequiredString(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8));
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

    internal Texture2D Texture(
        int specialObject,
        int direction,
        int frame,
        bool damagePalette = false)
    {
        var key = (specialObject, direction & 3, frame & 1, damagePalette);
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
            image,
            record.Oam,
            record.TileBase,
            basePalette: damagePalette ? 5 : 0);
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

}

internal readonly record struct FrameRecord(int Ring, int SpecialObject, int Direction, int Frame, string Sprite, int TileBase, string Oam, int InitialDuration, int LoopDuration);
