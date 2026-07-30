using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed PART_OWL_STATUE ($13) behavior, visuals, TX_39xx text, and
/// INTERAC_SPARKLE ($84:$00) child data.
/// </summary>
internal sealed class OwlStatueDatabase
{
    internal const int PartId = 0x13;
    internal const int MysterySeedItem = 0x24;

    private readonly Dictionary<int, OwlStatueRecord> _records = new();

    internal OwlStatueDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/owl_statues.tsv",
            new GeneratedTableSchema(
                "owl statues",
                GeneratedTableKeySemantics.Unique,
                [
                    "subid", "text-id", "utf8-base64", "sprite", "tile-base",
                    "palette", "collision-mode", "radius-y", "radius-x",
                    "damage", "health", "idle-animation",
                    "speaking-animation", "floor-tile", "floor-collision",
                    "mystery-collision", "activation-counter",
                    "speaking-counter", "text-counter", "sparkle-offsets",
                    "sparkle-sprite", "sparkle-tile-base", "sparkle-palette",
                    "sparkle-animation", "source"
                ],
                ["subid"],
                headerRequired: true));

        foreach (GeneratedTableRow row in table.Rows)
        {
            int subId = row.HexByte(0);
            var record = new OwlStatueRecord(
                subId,
                row.HexWord(1),
                row.Base64Utf8(2),
                row.RequiredString(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.HexByte(9),
                row.HexByte(10),
                row.RequiredString(11),
                row.RequiredString(12),
                row.HexByte(13),
                row.HexByte(14),
                row.HexByte(15),
                row.UnsignedDecimal(16),
                row.UnsignedDecimal(17),
                row.UnsignedDecimal(18),
                ParseOffsets(row, 19),
                new OwlStatueSparkleRecord(
                    row.RequiredString(20),
                    row.HexByte(21),
                    row.HexByte(22),
                    row.RequiredString(23)),
                row.RequiredString(24));
            _records.Add(subId, record);
        }

        if (_records.Count != 0x14)
        {
            throw new InvalidOperationException(
                $"Expected 20 PART_OWL_STATUE subids, got {_records.Count}.");
        }
        for (int subId = 0; subId < 0x14; subId++)
        {
            OwlStatueRecord record = Record(subId);
            if (record.TextId != 0x3900 + subId ||
                string.IsNullOrWhiteSpace(record.Message) ||
                record.CollisionMode != 0x82 ||
                record.RadiusY != 7 ||
                record.RadiusX != 7 ||
                record.FloorTile != 0x00 ||
                record.FloorCollision != 0x0f ||
                record.MysteryCollision != 0x9a ||
                record.ActivationCounter != 50 ||
                record.SpeakingCounter != 30 ||
                record.TextCounter != 22 ||
                record.SparkleOffsets.Length != 6)
            {
                throw new InvalidOperationException(
                    $"{record.Source} contains invalid PART_OWL_STATUE data.");
            }
        }
    }

    internal int Count => _records.Count;

    internal OwlStatueRecord Record(int subId) =>
        _records.TryGetValue(subId, out OwlStatueRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"PART_OWL_STATUE subid ${subId:x2} was not imported.");

    private static Vector2[] ParseOffsets(GeneratedTableRow row, int column)
    {
        string[] values = row.RequiredString(column).Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        var offsets = new Vector2[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            string[] pair = values[index].Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            if (pair.Length != 2 ||
                !int.TryParse(pair[0], out int x) ||
                !int.TryParse(pair[1], out int y))
            {
                throw row.Invalid(column, "semicolon-separated x,y pairs");
            }
            offsets[index] = new Vector2(x, y);
        }
        return offsets;
    }
}

internal readonly record struct OwlStatueRecord(
    int SubId,
    int TextId,
    string Message,
    string Sprite,
    int TileBase,
    int Palette,
    int CollisionMode,
    int RadiusY,
    int RadiusX,
    int Damage,
    int Health,
    string IdleAnimation,
    string SpeakingAnimation,
    int FloorTile,
    int FloorCollision,
    int MysteryCollision,
    int ActivationCounter,
    int SpeakingCounter,
    int TextCounter,
    Vector2[] SparkleOffsets,
    OwlStatueSparkleRecord Sparkle,
    string Source);

internal readonly record struct OwlStatueSparkleRecord(
    string Sprite,
    int TileBase,
    int Palette,
    string Animation);
