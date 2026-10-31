using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>
/// Imported ITEM_SEED_SATCHEL parent/seed-child data for the supported active
/// Ember, Scent, Pegasus, Gale, and Mystery projectiles. Pegasus Satchel use
/// branches in its parent before allocating the projectile represented here.
/// </summary>
public sealed class SeedSatchelDatabase
{
    public string AssetRoot { get; }
    internal bool RejectsUnderwater { get; }
    internal bool RejectsRaft { get; }

    private readonly Dictionary<int, SeedRecord> _records = new();

    public SeedSatchelDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        var parent = GeneratedTable.Load(AssetRoot + "metadata/seed_satchel_parent.tsv",
            new GeneratedTableSchema("Satchel parent", GeneratedTableKeySemantics.Unique,
                ["rejects-underwater", "rejects-raft", "source"], ["source"], headerRequired: true));
        if (parent.Rows.Count != 1)
            throw new InvalidOperationException("Expected one native Satchel parent profile.");
        RejectsUnderwater = parent.Rows[0].Boolean01(0);
        RejectsRaft = parent.Rows[0].Boolean01(1);

        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "metadata/seed_satchel.tsv",
            new GeneratedTableSchema(
                "seed satchel",
                GeneratedTableKeySemantics.Unique,
                [
                    "parent-item", "seed-item", "treasure-id", "sprite", "tile-base",
                    "palette", "collision", "radius-y", "radius-x", "damage", "initial-z",
                    "speed-z", "gravity", "speed-raw", "up-y", "up-x", "right-y",
                    "right-x", "down-y", "down-x", "left-y", "left-x", "link-frames",
                    "flame-sprite", "flame-tile-base", "flame-oam-flags", "flame-counter",
                    "landing-sound", "flame-sound", "animation",
                    "collision-effect-tile-base", "collision-effect-oam-flags",
                    "collision-effect-counter", "collision-effect-sound",
                    "effect-animation", "initial-bounces", "gale-outdoors-mask", "source"
                ],
                ["seed-item"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int seedItem = row.HexByte(1);
            _records.Add(seedItem, new SeedRecord(
                AssetRoot, row.HexByte(0), seedItem, row.HexByte(2), row.RequiredString(3),
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
                row.HexByte(28), row.RequiredString(29), row.HexByte(30),
                row.HexByte(31), row.UnsignedDecimal(32), row.HexByte(33),
                row.RequiredString(34), row.UnsignedDecimal(35), row.HexByte(36), row.RequiredString(37)));
        }
        if (_records.Count != 5 ||
            !_records.ContainsKey(0x20) ||
            !_records.ContainsKey(0x21) ||
            !_records.ContainsKey(0x22) ||
            !_records.ContainsKey(0x23) || !_records.ContainsKey(0x24))
        {
            throw new InvalidOperationException(
                "Expected imported ITEM_EMBER_SEED ($20), " +
                "ITEM_SCENT_SEED ($21), ITEM_PEGASUS_SEED ($22), ITEM_GALE_SEED ($23), and " +
                "ITEM_MYSTERY_SEED ($24) records.");
        }
    }

    public bool TryGet(int seedItem, out SeedRecord record) =>
        _records.TryGetValue(seedItem, out record);

    public SeedRecord Ember => _records[0x20];
    public SeedRecord Scent => _records[0x21];
    public SeedRecord Pegasus => _records[0x22];
    public SeedRecord Gale => _records[0x23];
    public SeedRecord Mystery => _records[0x24];
}

public readonly record struct SeedRecord(string AssetRoot, int ParentItem, int SeedItem, int TreasureId, string Sprite, int TileBase, int Palette, int Collision, int CollisionRadiusY, int CollisionRadiusX, int Damage, int InitialZ, int SpeedZ, int Gravity, int SpeedRaw, Vector2I UpOffset, Vector2I RightOffset, Vector2I DownOffset, Vector2I LeftOffset, int LinkFrames, string FlameSprite, int FlameTileBase, int FlameOamFlags, int FlameCounter, int LandingSound, int FlameSound, string Animation, int CollisionEffectTileBase, int CollisionEffectOamFlags, int CollisionEffectCounter, int CollisionEffectSound, string EffectAnimation, int InitialBounces, int GaleOutdoorsMask, string Source)
{
    public int FlamePalette => FlameOamFlags & 0x07;
    public int CollisionEffectPalette => CollisionEffectOamFlags & 0x07;

    public Vector2I Offset(Vector2I direction) => direction == Vector2I.Up ? UpOffset : direction == Vector2I.Right ? RightOffset : direction == Vector2I.Down ? DownOffset : direction == Vector2I.Left ? LeftOffset : throw new ArgumentOutOfRangeException(nameof(direction));
}
