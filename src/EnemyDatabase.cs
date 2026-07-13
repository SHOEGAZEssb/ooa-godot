using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class EnemyDatabase
{
    private readonly Dictionary<int, List<EnemyRecord>> _keeseByRoom = new();

    public int KeeseRecordCount { get; }
    public int KeeseInstanceCount { get; }

    public EnemyDatabase()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/objects/keese.tsv");
        int records = 0;
        int instances = 0;
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 15)
                throw new InvalidOperationException($"Malformed Keese data row: {line}");

            var record = new EnemyRecord(
                int.Parse(columns[0]),
                Convert.ToInt32(columns[1], 16),
                Convert.ToInt32(columns[2], 16),
                Convert.ToInt32(columns[3], 16),
                Convert.ToInt32(columns[4], 16),
                int.Parse(columns[5]),
                columns[6],
                int.Parse(columns[7]),
                int.Parse(columns[8]),
                int.Parse(columns[9]),
                int.Parse(columns[10]),
                int.Parse(columns[11]),
                int.Parse(columns[12]),
                columns[13],
                columns[14]);

            int key = MakeKey(record.Group, record.Room);
            if (!_keeseByRoom.TryGetValue(key, out List<EnemyRecord>? roomRecords))
            {
                roomRecords = new List<EnemyRecord>();
                _keeseByRoom.Add(key, roomRecords);
            }
            roomRecords.Add(record);
            records++;
            instances += record.Count;
        }

        KeeseRecordCount = records;
        KeeseInstanceCount = instances;
    }

    public IReadOnlyList<EnemyRecord> GetRoomKeese(int group, int room)
    {
        return _keeseByRoom.TryGetValue(MakeKey(group, room), out List<EnemyRecord>? records)
            ? records
            : Array.Empty<EnemyRecord>();
    }

    private static int MakeKey(int group, int room) => (group << 8) | room;

    public readonly record struct EnemyRecord(
        int Group,
        int Room,
        int Id,
        int SubId,
        int Flags,
        int Count,
        string SpriteName,
        int TileBase,
        int Palette,
        int CollisionRadiusY,
        int CollisionRadiusX,
        int DamageQuarters,
        int Health,
        string IdleAnimation,
        string FlyAnimation);
}
