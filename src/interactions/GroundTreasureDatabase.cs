using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Positioned treasures created by room interaction $dc:$07. The source
/// interaction is not itself visible: it conditionally creates INTERAC_TREASURE
/// $60 from an imported treasure-object record, then deletes itself.
/// </summary>
internal sealed class GroundTreasureDatabase
{
    private readonly Dictionary<(int Group, int Room), List<Record>> _byRoom = new();

    public GroundTreasureDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/ground_treasures.tsv",
            new GeneratedTableSchema(
                "ground treasures",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "y", "x", "treasure-object", "sprite",
                    "tile-base", "palette", "animation", "completion-text-id",
                    "completion-text-base64", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new Record(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.HexByte(3),
                row.HexByte(4),
                row.RequiredString(5),
                row.RequiredString(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.RequiredString(9),
                row.HexWord(10),
                row.Base64Utf8(11),
                row.RequiredString(12));
            if (record.Group is < 0 or > 7 || record.Room is < 0 or > 0xff ||
                record.Order < 0 || record.Y is < 0 or > 0xff ||
                record.X is < 0 or > 0xff ||
                string.IsNullOrWhiteSpace(record.TreasureObject) ||
                string.IsNullOrWhiteSpace(record.Sprite) ||
                string.IsNullOrWhiteSpace(record.Animation) ||
                record.CompletionTextId != 0x0049 ||
                string.IsNullOrWhiteSpace(record.CompletionMessage))
            {
                throw new InvalidOperationException(
                    $"Invalid ground-treasure row at {row.Path}:{row.LineNumber}.");
            }

            if (!_byRoom.TryGetValue(
                (record.Group, record.Room), out List<Record>? records))
            {
                records = new List<Record>();
                _byRoom.Add((record.Group, record.Room), records);
            }
            records.Add(record);
            count++;
        }

        if (count != 8)
            throw new InvalidOperationException(
                $"Expected eight $dc:$07 ground treasures, loaded {count}.");
        foreach (List<Record> records in _byRoom.Values)
            records.Sort((left, right) => left.Order.CompareTo(right.Order));
    }

    public IReadOnlyList<Record> GetRoomRecords(int group, int room) =>
        _byRoom.TryGetValue((group, room), out List<Record>? records)
            ? records
            : Array.Empty<Record>();

    internal readonly record struct Record(
        int Group,
        int Room,
        int Order,
        int Y,
        int X,
        string TreasureObject,
        string Sprite,
        int TileBase,
        int Palette,
        string Animation,
        int CompletionTextId,
        string CompletionMessage,
        string Source,
        int SpawnMode = 0,
        int GrabMode = 2,
        int SpawnDelayFrames = 0,
        int InitialZPixels = 0,
        int BounceCount = 0,
        int Gravity = 0,
        int BounceSpeed = 0,
        int SpawnSound = 0,
        int LandingSound = 0,
        bool InitialZAboveScreen = false,
        int AboveScreenMargin = 8,
        int AboveScreenFallback = -128);
}
