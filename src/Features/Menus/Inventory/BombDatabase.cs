using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed ITEM_BOMB ($03) data imported from the supported Ages disassembly.
/// The runtime owns state; presentation, motion, fuse, and explosion probes
/// remain source-derived records.
/// </summary>
public sealed class BombDatabase
{
    internal BombRecord Data { get; }

    public BombDatabase(
        string path = "res://assets/oracle/metadata/bomb.tsv")
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "bomb",
                GeneratedTableKeySemantics.Unique,
                [
                    "item", "treasure-id", "sprite", "tile-base", "palette",
                    "collision", "radius-y", "radius-x", "base-damage",
                    "explosion-sprite", "explosion-tile-base",
                    "explosion-oam-flags", "pickup-sound", "throw-sound",
                    "landing-sound", "explosion-sound", "gravity",
                    "initial-speed-z", "speed-raw", "toss-speed-raw",
                    "conveyor-speed-raw", "lift-low-frames",
                    "lift-mid-frames", "lift-high-frames", "throw-frames",
                    "edge-offsets", "bounce-speeds", "item-passable-tiles",
                    "break-probes",
                    "fuse-animation", "explosion-animation", "source"
                ],
                ["item"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one ITEM_BOMB record, got {table.Rows.Count}.");
        }

        GeneratedTableRow row = table.Rows[0];
        Data = new BombRecord(
            row.HexByte(0),
            row.HexByte(1),
            row.RequiredString(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.UnsignedDecimal(6),
            row.UnsignedDecimal(7),
            row.UnsignedDecimal(8),
            row.RequiredString(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.HexByte(13),
            row.HexByte(14),
            row.HexByte(15),
            row.UnsignedDecimal(16),
            row.Decimal(17),
            row.HexByte(18),
            row.HexByte(19),
            row.HexByte(20),
            row.UnsignedDecimal(21),
            row.UnsignedDecimal(22),
            row.UnsignedDecimal(23),
            row.UnsignedDecimal(24),
            ParseEdgeOffsets(row.RequiredString(25), row),
            ParseBounceSpeeds(row.RequiredString(26), row),
            ParseItemPassableTiles(row.RequiredString(27), row),
            ParseBreakProbes(row.RequiredString(28), row),
            row.RequiredString(29),
            row.RequiredString(30),
            row.RequiredString(31));
        Validate(Data);
    }

    private static Vector2I[] ParseEdgeOffsets(
        string encoded,
        GeneratedTableRow row)
    {
        string[] entries = encoded.Split(';');
        if (entries.Length != 4)
            throw row.Invalid(25, "four direction-ordered Y/X offsets");
        var result = new Vector2I[entries.Length];
        for (int index = 0; index < entries.Length; index++)
        {
            int[] values = ParseDecimalTuple(entries[index], 2, row, 25);
            result[index] = new Vector2I(values[1], values[0]);
        }
        return result;
    }

    private static Dictionary<int, int> ParseBounceSpeeds(
        string encoded,
        GeneratedTableRow row)
    {
        var result = new Dictionary<int, int>();
        foreach (string entry in encoded.Split(';'))
        {
            string[] values = entry.Split(':');
            if (values.Length != 2 ||
                !int.TryParse(values[0], out int from) ||
                !int.TryParse(values[1], out int to) ||
                from is < 0 or > 0xff || to is < 0 or > 0xff ||
                !result.TryAdd(from, to))
            {
                throw row.Invalid(26, "unique decimal speed:reduced-speed pairs");
            }
        }
        return result;
    }

    private static BombBreakProbe[] ParseBreakProbes(
        string encoded,
        GeneratedTableRow row)
    {
        string[] entries = encoded.Split(';');
        if (entries.Length != 9)
            throw row.Invalid(28, "nine ordered Z/Y/X probes");
        var result = new BombBreakProbe[entries.Length];
        for (int index = 0; index < entries.Length; index++)
        {
            int[] values = ParseDecimalTuple(entries[index], 3, row, 28);
            result[index] = new BombBreakProbe(
                values[0], new Vector2I(values[2], values[1]));
        }
        return result;
    }

    private static byte[][] ParseItemPassableTiles(
        string encoded,
        GeneratedTableRow row)
    {
        string[] groups = encoded.Split(';');
        if (groups.Length != 6)
            throw row.Invalid(27, "six collision-set tile lists");
        var result = new byte[groups.Length][];
        for (int index = 0; index < groups.Length; index++)
        {
            string[] pair = groups[index].Split(':');
            if (pair.Length != 2 ||
                !int.TryParse(pair[0], out int collisionSet) ||
                collisionSet != index)
            {
                throw row.Invalid(27, "ordered collision-set:tile-list entries");
            }
            if (pair[1].Length == 0)
            {
                result[index] = [];
                continue;
            }
            string[] tiles = pair[1].Split(',');
            result[index] = new byte[tiles.Length];
            for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
            {
                if (!byte.TryParse(tiles[tileIndex], out result[index][tileIndex]))
                    throw row.Invalid(27, "decimal byte tile lists");
            }
        }
        return result;
    }

    private static int[] ParseDecimalTuple(
        string encoded,
        int count,
        GeneratedTableRow row,
        int column)
    {
        string[] entries = encoded.Split(',');
        if (entries.Length != count)
            throw row.Invalid(column, $"{count} comma-separated decimals");
        var result = new int[count];
        for (int index = 0; index < entries.Length; index++)
        {
            if (!int.TryParse(entries[index], out result[index]) ||
                result[index] is < -256 or > 255)
            {
                throw row.Invalid(column, $"{count} signed byte-like decimals");
            }
        }
        return result;
    }

    private static void Validate(BombRecord record)
    {
        AnimationDefinition fuse =
            OracleGraphicsCache.GetAnimationDefinition(record.FuseAnimation);
        AnimationDefinition explosion =
            OracleGraphicsCache.GetAnimationDefinition(
                record.ExplosionAnimation);
        if (record.Item != InventoryState.ItemBomb ||
            record.TreasureId != TreasureDatabase.TreasureBombs ||
            record.Sprite != "spr_common_items" ||
            record.TileBase != 0x10 || record.Palette != 0x04 ||
            record.Collision != 0x18 ||
            record.RadiusY != 4 || record.RadiusX != 4 ||
            record.BaseDamage != 4 ||
            record.ExplosionSprite != "spr_common_sprites" ||
            record.ExplosionTileBase != 0x0c ||
            record.ExplosionOamFlags != 0x0a ||
            record.PickupSound != OracleSoundEngine.SndPickup ||
            record.ThrowSound != OracleSoundEngine.SndThrow ||
            record.LandingSound != OracleSoundEngine.SndBombLand ||
            record.ExplosionSound != OracleSoundEngine.SndExplosion ||
            record.Gravity != 0x1c || record.InitialSpeedZ != -0xf0 ||
            record.SpeedRaw != 0x3c || record.TossSpeedRaw != 0x64 ||
            record.ConveyorSpeedRaw != 0x14 ||
            record.LiftLowFrames != 7 || record.LiftMidFrames != 4 ||
            record.LiftHighFrames != 2 || record.ThrowFrames != 8 ||
            record.EdgeOffsets.Length != 4 ||
            record.BounceSpeeds.Count != 25 ||
            record.ItemPassableTiles.Length != 6 ||
            record.ItemPassableTiles[0].Length != 2 ||
            record.ItemPassableTiles[1].Length != 3 ||
            record.ItemPassableTiles[2].Length != 16 ||
            record.ItemPassableTiles[3].Length != 0 ||
            record.ItemPassableTiles[4].Length != 2 ||
            record.ItemPassableTiles[5].Length != 16 ||
            record.BreakProbes.Length != 9 ||
            fuse.Frames.Length != 11 ||
            fuse.Frames[^1].Parameter != 1 ||
            explosion.Frames.Length != 7 ||
            explosion.Frames[0].Parameter != 6 ||
            explosion.Frames[^1].Parameter != 0xff)
        {
            throw new InvalidOperationException(
                $"Invalid ITEM_BOMB record imported from {record.Source}.");
        }
    }
}

internal sealed record BombRecord(
    int Item,
    int TreasureId,
    string Sprite,
    int TileBase,
    int Palette,
    int Collision,
    int RadiusY,
    int RadiusX,
    int BaseDamage,
    string ExplosionSprite,
    int ExplosionTileBase,
    int ExplosionOamFlags,
    int PickupSound,
    int ThrowSound,
    int LandingSound,
    int ExplosionSound,
    int Gravity,
    int InitialSpeedZ,
    int SpeedRaw,
    int TossSpeedRaw,
    int ConveyorSpeedRaw,
    int LiftLowFrames,
    int LiftMidFrames,
    int LiftHighFrames,
    int ThrowFrames,
    Vector2I[] EdgeOffsets,
    IReadOnlyDictionary<int, int> BounceSpeeds,
    byte[][] ItemPassableTiles,
    BombBreakProbe[] BreakProbes,
    string FuseAnimation,
    string ExplosionAnimation,
    string Source)
{
    internal int ExplosionPalette => ExplosionOamFlags & 0x07;

    internal Vector2I EdgeOffset(Vector2I direction) =>
        EdgeOffsets[DirectionIndex(direction)];

    internal int ReducedBounceSpeed(int speed) =>
        BounceSpeeds.TryGetValue(speed, out int reduced)
            ? reduced
            : throw new InvalidOperationException(
                $"{Source} has no bounce reduction for speed ${speed:x2}.");

    internal bool CanPassSolidTile(OracleRoomData room, Vector2 point)
    {
        int collisionSet = room.ActiveCollisions;
        return collisionSet >= 0 && collisionSet < ItemPassableTiles.Length &&
            Array.IndexOf(
                ItemPassableTiles[collisionSet],
                room.GetMetatile(point)) >= 0;
    }

    private static int DirectionIndex(Vector2I direction) =>
        direction == Vector2I.Up ? 0
        : direction == Vector2I.Right ? 1
        : direction == Vector2I.Down ? 2
        : direction == Vector2I.Left ? 3
        : throw new ArgumentOutOfRangeException(nameof(direction));
}

internal readonly record struct BombBreakProbe(
    int NecessaryZ,
    Vector2I Offset);
