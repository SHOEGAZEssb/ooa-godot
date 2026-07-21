using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class TimePortalDatabase
{
    public readonly record struct PortalRecord(
        int Group,
        int Room,
        int SubId,
        int Y,
        int X,
        string SpriteName,
        int TileBase,
        int Palette,
        int LoopStart,
        string Animation);

    private readonly Dictionary<int, List<PortalRecord>> _byRoom = new();

    public TimePortalDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/timePortals.tsv",
            new GeneratedTableSchema(
                "time portals",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "subid", "y", "x", "sprite",
                    "tile-base", "palette", "loop-start", "animation"
                ],
                ["group", "room"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new PortalRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.RequiredString(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.RequiredString(9));
            int key = MakeKey(record.Group, record.Room);
            if (!_byRoom.TryGetValue(key, out List<PortalRecord>? records))
            {
                records = new List<PortalRecord>();
                _byRoom.Add(key, records);
            }
            records.Add(record);
            count++;
        }

        if (count != 21)
            throw new InvalidOperationException($"Expected 21 time-portal spawners, loaded {count}.");
    }

    public IReadOnlyList<PortalRecord> GetRoomPortals(int group, int room) =>
        _byRoom.TryGetValue(MakeKey(group, room), out List<PortalRecord>? records)
            ? records
            : Array.Empty<PortalRecord>();

    private static int MakeKey(int group, int room) => (group << 8) | room;
}
