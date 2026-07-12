using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

public sealed class NpcDatabase
{
    private readonly Dictionary<int, List<NpcRecord>> _byRoom = new();

    public NpcDatabase()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/objects/npcs.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 18)
                throw new InvalidOperationException($"Malformed NPC data row: {line}");

            int group = int.Parse(columns[0]);
            int room = Convert.ToInt32(columns[1], 16);
            var record = new NpcRecord(
                group,
                room,
                Convert.ToInt32(columns[2], 16),
                Convert.ToInt32(columns[3], 16),
                Convert.ToInt32(columns[4], 16),
                Convert.ToInt32(columns[5], 16),
                Convert.ToInt32(columns[6], 16),
                Convert.ToInt32(columns[7], 16),
                columns[8],
                int.Parse(columns[9]),
                int.Parse(columns[10]),
                int.Parse(columns[11]),
                columns[12] == "1",
                columns[13],
                columns[14],
                columns[15],
                columns[16],
                Encoding.UTF8.GetString(Convert.FromBase64String(columns[17])));

            int key = MakeKey(group, room);
            if (!_byRoom.TryGetValue(key, out List<NpcRecord>? records))
            {
                records = new List<NpcRecord>();
                _byRoom.Add(key, records);
            }
            records.Add(record);
        }
    }

    public IReadOnlyList<NpcRecord> GetRoomNpcs(int group, int room)
    {
        return _byRoom.TryGetValue(MakeKey(group, room), out List<NpcRecord>? records)
            ? records
            : Array.Empty<NpcRecord>();
    }

    private static int MakeKey(int group, int room)
    {
        return (group << 8) | room;
    }

    public readonly record struct NpcRecord(
        int Group,
        int Room,
        int Id,
        int SubId,
        int Y,
        int X,
        int Var03,
        int TextId,
        string SpriteName,
        int TileBase,
        int Palette,
        int DefaultAnimation,
        bool CanFace,
        string UpOam,
        string RightOam,
        string DownOam,
        string LeftOam,
        string Message);
}
