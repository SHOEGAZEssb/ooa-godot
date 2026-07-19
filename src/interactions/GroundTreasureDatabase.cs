using Godot;
using System;
using System.Collections.Generic;
using System.Text;

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
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/ground_treasures.tsv");
        int count = 0;
        foreach (string rawLine in source.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 13)
                throw new InvalidOperationException(
                    $"Malformed ground-treasure row: {line}");

            var record = new Record(
                int.Parse(fields[0]),
                Convert.ToInt32(fields[1], 16),
                int.Parse(fields[2]),
                Convert.ToInt32(fields[3], 16),
                Convert.ToInt32(fields[4], 16),
                fields[5],
                fields[6],
                int.Parse(fields[7]),
                int.Parse(fields[8]),
                fields[9],
                Convert.ToInt32(fields[10], 16),
                Encoding.UTF8.GetString(Convert.FromBase64String(fields[11])),
                fields[12]);
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
                    $"Invalid ground-treasure row: {line}");
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
        string Source);
}
