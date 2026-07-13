using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class EnemyDatabase
{
    private readonly Dictionary<int, List<EnemyRecord>> _keeseByRoom = new();
    private readonly Dictionary<int, List<OctorokRecord>> _octoroksByRoom = new();
    private readonly Dictionary<int, List<ZolRecord>> _zolsByRoom = new();
    private readonly Dictionary<int, List<GelRecord>> _gelsByRoom = new();

    public int KeeseRecordCount { get; }
    public int KeeseInstanceCount { get; }
    public int OctorokRecordCount { get; }
    public int OctorokInstanceCount { get; }
    public int ZolRecordCount { get; }
    public int ZolInstanceCount { get; }
    public int GelRecordCount { get; }
    public int GelInstanceCount { get; }
    public OctorokProjectileRecord OctorokProjectile { get; }
    public GelDefinition Gel { get; }

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

        source = FileAccess.GetFileAsString("res://assets/oracle/objects/octoroks.tsv");
        records = 0;
        instances = 0;
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 22)
                throw new InvalidOperationException($"Malformed Octorok data row: {line}");

            var record = new OctorokRecord(
                int.Parse(columns[0]),
                Convert.ToInt32(columns[1], 16),
                Convert.ToInt32(columns[2], 16),
                Convert.ToInt32(columns[3], 16),
                Convert.ToInt32(columns[4], 16),
                int.Parse(columns[5]),
                columns[6] == "F",
                ParsePosition(columns[7]),
                ParsePosition(columns[8]),
                columns[9],
                int.Parse(columns[10]),
                int.Parse(columns[11]),
                int.Parse(columns[12]),
                int.Parse(columns[13]),
                int.Parse(columns[14]),
                int.Parse(columns[15]),
                int.Parse(columns[16]),
                int.Parse(columns[17]),
                columns[18],
                columns[19],
                columns[20],
                columns[21]);

            int key = MakeKey(record.Group, record.Room);
            if (!_octoroksByRoom.TryGetValue(key, out List<OctorokRecord>? roomRecords))
            {
                roomRecords = new List<OctorokRecord>();
                _octoroksByRoom.Add(key, roomRecords);
            }
            roomRecords.Add(record);
            records++;
            instances += record.Count;
        }
        OctorokRecordCount = records;
        OctorokInstanceCount = instances;

        source = FileAccess.GetFileAsString("res://assets/oracle/objects/zols.tsv");
        records = 0;
        instances = 0;
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 22)
                throw new InvalidOperationException($"Malformed Zol data row: {line}");

            var record = new ZolRecord(
                int.Parse(columns[0]),
                Convert.ToInt32(columns[1], 16),
                Convert.ToInt32(columns[2], 16),
                Convert.ToInt32(columns[3], 16),
                Convert.ToInt32(columns[4], 16),
                int.Parse(columns[5]),
                columns[6] == "F",
                ParsePosition(columns[7]),
                ParsePosition(columns[8]),
                columns[9],
                int.Parse(columns[10]),
                int.Parse(columns[11]),
                int.Parse(columns[12]),
                int.Parse(columns[13]),
                int.Parse(columns[14]),
                int.Parse(columns[15]),
                columns[16],
                columns[17],
                columns[18],
                columns[19],
                columns[20],
                columns[21]);
            int key = MakeKey(record.Group, record.Room);
            if (!_zolsByRoom.TryGetValue(key, out List<ZolRecord>? roomRecords))
            {
                roomRecords = new List<ZolRecord>();
                _zolsByRoom.Add(key, roomRecords);
            }
            roomRecords.Add(record);
            records++;
            instances += record.Count;
        }
        ZolRecordCount = records;
        ZolInstanceCount = instances;

        source = FileAccess.GetFileAsString("res://assets/oracle/objects/gels.tsv");
        records = 0;
        instances = 0;
        GelDefinition? gelDefinition = null;
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 19)
                throw new InvalidOperationException($"Malformed Gel data row: {line}");

            var definition = new GelDefinition(
                Convert.ToInt32(columns[2], 16),
                columns[9],
                int.Parse(columns[10]),
                int.Parse(columns[11]),
                int.Parse(columns[12]),
                int.Parse(columns[13]),
                int.Parse(columns[14]),
                int.Parse(columns[15]),
                columns[16],
                columns[17],
                columns[18]);
            gelDefinition ??= definition;
            if (gelDefinition.Value != definition)
                throw new InvalidOperationException("Room Gel records disagree on their shared definition.");

            var record = new GelRecord(
                int.Parse(columns[0]),
                Convert.ToInt32(columns[1], 16),
                Convert.ToInt32(columns[4], 16),
                int.Parse(columns[5]),
                columns[6] == "F",
                ParsePosition(columns[7]),
                ParsePosition(columns[8]));
            int key = MakeKey(record.Group, record.Room);
            if (!_gelsByRoom.TryGetValue(key, out List<GelRecord>? roomRecords))
            {
                roomRecords = new List<GelRecord>();
                _gelsByRoom.Add(key, roomRecords);
            }
            roomRecords.Add(record);
            records++;
            instances += record.Count;
        }
        Gel = gelDefinition ?? throw new InvalidOperationException("Gel data is empty.");
        GelRecordCount = records;
        GelInstanceCount = instances;

        string[] projectileRows = FileAccess.GetFileAsString(
                "res://assets/oracle/effects/octorok_projectile.tsv")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        OctorokProjectileRecord? projectile = null;
        foreach (string rawLine in projectileRows)
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            if (columns.Length != 9)
                throw new InvalidOperationException($"Malformed Octorok projectile row: {line}");
            projectile = new OctorokProjectileRecord(
                columns[0],
                int.Parse(columns[1]),
                int.Parse(columns[2]),
                int.Parse(columns[3]),
                int.Parse(columns[4]),
                int.Parse(columns[5]),
                int.Parse(columns[6]),
                columns[7],
                columns[8]);
        }
        OctorokProjectile = projectile ?? throw new InvalidOperationException(
            "Octorok projectile data is empty.");
    }

    public IReadOnlyList<EnemyRecord> GetRoomKeese(int group, int room)
    {
        return _keeseByRoom.TryGetValue(MakeKey(group, room), out List<EnemyRecord>? records)
            ? records
            : Array.Empty<EnemyRecord>();
    }

    public IReadOnlyList<OctorokRecord> GetRoomOctoroks(int group, int room)
    {
        return _octoroksByRoom.TryGetValue(
            MakeKey(group, room), out List<OctorokRecord>? records)
            ? records
            : Array.Empty<OctorokRecord>();
    }

    public IReadOnlyList<ZolRecord> GetRoomZols(int group, int room)
    {
        return _zolsByRoom.TryGetValue(MakeKey(group, room), out List<ZolRecord>? records)
            ? records
            : Array.Empty<ZolRecord>();
    }

    public IReadOnlyList<GelRecord> GetRoomGels(int group, int room)
    {
        return _gelsByRoom.TryGetValue(MakeKey(group, room), out List<GelRecord>? records)
            ? records
            : Array.Empty<GelRecord>();
    }

    private static int MakeKey(int group, int room) => (group << 8) | room;

    private static int ParsePosition(string value) =>
        value == "-1" ? -1 : Convert.ToInt32(value, 16);

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

    public readonly record struct OctorokRecord(
        int Group,
        int Room,
        int Id,
        int SubId,
        int Flags,
        int Count,
        bool FixedPosition,
        int Y,
        int X,
        string SpriteName,
        int TileBase,
        int Palette,
        int CollisionRadiusY,
        int CollisionRadiusX,
        int DamageQuarters,
        int Health,
        int SpeedRaw,
        int CounterMask,
        string UpAnimation,
        string RightAnimation,
        string DownAnimation,
        string LeftAnimation);

    public readonly record struct OctorokProjectileRecord(
        string SpriteName,
        int TileBase,
        int Palette,
        int CollisionRadiusY,
        int CollisionRadiusX,
        int DamageQuarters,
        int SpeedRaw,
        string NormalAnimation,
        string BounceAnimation);

    public readonly record struct ZolRecord(
        int Group,
        int Room,
        int Id,
        int SubId,
        int Flags,
        int Count,
        bool FixedPosition,
        int Y,
        int X,
        string SpriteName,
        int TileBase,
        int Palette,
        int CollisionRadiusY,
        int CollisionRadiusX,
        int DamageQuarters,
        int Health,
        string EmergeAnimation,
        string WaitAnimation,
        string HopAnimation,
        string DisappearAnimation,
        string RedIdleAnimation,
        string RedShakeAnimation);

    public readonly record struct GelRecord(
        int Group,
        int Room,
        int Flags,
        int Count,
        bool FixedPosition,
        int Y,
        int X);

    public readonly record struct GelDefinition(
        int Id,
        string SpriteName,
        int TileBase,
        int Palette,
        int CollisionRadiusY,
        int CollisionRadiusX,
        int DamageQuarters,
        int Health,
        string NormalAnimation,
        string AttachedAnimation,
        string ShakeAnimation);
}
