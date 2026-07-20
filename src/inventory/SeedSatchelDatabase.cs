using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported ITEM_SEED_SATCHEL parent/seed-child data. The first acquired
/// Satchel can select only Ember Seeds, so unsupported later seed effects are
/// rejected before a seed is consumed.
/// </summary>
public sealed class SeedSatchelDatabase
{
    private readonly Dictionary<int, SeedRecord> _records = new();

    public SeedSatchelDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/metadata/seed_satchel.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            if (columns.Length != 31)
                throw new InvalidOperationException($"Malformed Seed Satchel row: {line}");

            int seedItem = Hex(columns[1]);
            _records.Add(seedItem, new SeedRecord(
                Hex(columns[0]), seedItem, Hex(columns[2]), columns[3],
                Hex(columns[4]), Hex(columns[5]), Hex(columns[6]),
                int.Parse(columns[7]), int.Parse(columns[8]), Hex(columns[9]),
                int.Parse(columns[10]), int.Parse(columns[11]), int.Parse(columns[12]),
                Hex(columns[13]),
                new Vector2I(int.Parse(columns[15]), int.Parse(columns[14])),
                new Vector2I(int.Parse(columns[17]), int.Parse(columns[16])),
                new Vector2I(int.Parse(columns[19]), int.Parse(columns[18])),
                new Vector2I(int.Parse(columns[21]), int.Parse(columns[20])),
                int.Parse(columns[22]), columns[23], Hex(columns[24]),
                Hex(columns[25]), int.Parse(columns[26]), Hex(columns[27]),
                Hex(columns[28]), columns[29], columns[30]));
        }
        if (_records.Count != 1 || !_records.ContainsKey(0x20))
            throw new InvalidOperationException(
                "Expected the imported first-Satchel ITEM_EMBER_SEED record ($20).");
    }

    public bool TryGet(int seedItem, out SeedRecord record) =>
        _records.TryGetValue(seedItem, out record);

    public SeedRecord Ember => _records[0x20];

    private static int Hex(string value) => Convert.ToInt32(value, 16);

    public readonly record struct SeedRecord(
        int ParentItem,
        int SeedItem,
        int TreasureId,
        string Sprite,
        int TileBase,
        int Palette,
        int Collision,
        int CollisionRadiusY,
        int CollisionRadiusX,
        int Damage,
        int InitialZ,
        int SpeedZ,
        int Gravity,
        int SpeedRaw,
        Vector2I UpOffset,
        Vector2I RightOffset,
        Vector2I DownOffset,
        Vector2I LeftOffset,
        int LinkFrames,
        string FlameSprite,
        int FlameTileBase,
        int FlameOamFlags,
        int FlameCounter,
        int LandingSound,
        int FlameSound,
        string Animation,
        string Source)
    {
        public int FlamePalette => FlameOamFlags & 0x07;

        public Vector2I Offset(Vector2I direction) => direction == Vector2I.Up
            ? UpOffset : direction == Vector2I.Right
            ? RightOffset : direction == Vector2I.Down
            ? DownOffset : direction == Vector2I.Left
            ? LeftOffset : throw new ArgumentOutOfRangeException(nameof(direction));
    }
}
