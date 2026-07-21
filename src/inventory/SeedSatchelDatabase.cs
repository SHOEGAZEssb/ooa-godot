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
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/seed_satchel.tsv",
            new GeneratedTableSchema(
                "seed satchel",
                GeneratedTableKeySemantics.Unique,
                [
                    "parent-item", "seed-item", "treasure-id", "sprite", "tile-base",
                    "palette", "collision", "radius-y", "radius-x", "damage", "initial-z",
                    "speed-z", "gravity", "speed-raw", "up-y", "up-x", "right-y",
                    "right-x", "down-y", "down-x", "left-y", "left-x", "link-frames",
                    "flame-sprite", "flame-tile-base", "flame-oam-flags", "flame-counter",
                    "landing-sound", "flame-sound", "animation", "source"
                ],
                ["seed-item"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int seedItem = row.HexByte(1);
            _records.Add(seedItem, new SeedRecord(
                row.HexByte(0), seedItem, row.HexByte(2), row.RequiredString(3),
                row.HexByte(4), row.HexByte(5), row.HexByte(6),
                row.UnsignedDecimal(7), row.UnsignedDecimal(8), row.HexByte(9),
                row.Decimal(10), row.Decimal(11), row.Decimal(12),
                row.HexByte(13),
                new Vector2I(row.Decimal(15), row.Decimal(14)),
                new Vector2I(row.Decimal(17), row.Decimal(16)),
                new Vector2I(row.Decimal(19), row.Decimal(18)),
                new Vector2I(row.Decimal(21), row.Decimal(20)),
                row.UnsignedDecimal(22), row.RequiredString(23), row.HexByte(24),
                row.HexByte(25), row.UnsignedDecimal(26), row.HexByte(27),
                row.HexByte(28), row.RequiredString(29), row.RequiredString(30)));
        }
        if (_records.Count != 1 || !_records.ContainsKey(0x20))
            throw new InvalidOperationException(
                "Expected the imported first-Satchel ITEM_EMBER_SEED record ($20).");
    }

    public bool TryGet(int seedItem, out SeedRecord record) =>
        _records.TryGetValue(seedItem, out record);

    public SeedRecord Ember => _records[0x20];

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
