using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-derived ENEMY_VINE_SPROUT $62 defaults, visual, and push timing.
/// The six live positions alias past-room flags $f0-$f5 in the original save.
/// </summary>
internal sealed class VineSproutDatabase
{
    internal const int PositionAddress = 0xc8f0;
    private readonly Dictionary<int, VineSproutRecord> _records = new();

    internal VineSproutDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/vine_sprouts.tsv",
            new GeneratedTableSchema(
                "vine sprout definitions",
                GeneratedTableKeySemantics.Unique,
                [
                    "subid", "default-position", "sprite", "tile-base",
                    "palette", "source-grayscale-inverted", "animation",
                    "speed-raw", "push-delay", "move-frames",
                    "cliff-overlap-radius", "cliff-ground-proximity",
                    "cliff-debris-interaction", "source"
                ],
                ["subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new VineSproutRecord(
                row.HexByte(0), row.HexByte(1), row.RequiredString(2),
                row.UnsignedDecimal(3), row.UnsignedDecimal(4),
                row.Boolean01(5), row.RequiredString(6), row.HexByte(7),
                row.UnsignedDecimal(8), row.UnsignedDecimal(9),
                row.UnsignedDecimal(10), row.UnsignedDecimal(11),
                row.HexByte(12), row.RequiredString(13));
            if (!_records.TryAdd(record.SubId, record))
                throw row.Invalid(0, "a unique vine sprout subid");
        }

        if (_records.Count != 6 ||
            Record(0) is not
                {
                    DefaultPosition: 0x41,
                    TileBase: 0x12,
                    Palette: 0,
                    SpeedRaw: 0x1e,
                    PushDelay: 20,
                    MoveFrames: 22,
                    CliffOverlapRadius: 6,
                    CliffGroundProximity: 3,
                    CliffDebrisInteraction: 0x06
                } ||
            Record(2).DefaultPosition != 0x16 ||
            Record(3).DefaultPosition != 0x35 ||
            Record(4).DefaultPosition != 0x18 ||
            Record(5).DefaultPosition != 0x53)
        {
            throw new InvalidOperationException(
                "Imported ENEMY_VINE_SPROUT contract is incomplete.");
        }
    }

    internal bool HasSubId(int subId) => _records.ContainsKey(subId);

    internal VineSproutRecord Record(int subId) =>
        _records.TryGetValue(subId, out VineSproutRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"ENEMY_VINE_SPROUT subid ${subId:x2} was not imported.");

    internal Vector2 ResolvePosition(
        int subId,
        OracleRoomData room,
        OracleSaveData save)
    {
        VineSproutRecord record = Record(subId);
        int packed = save.ReadWramByte(PositionAddress + subId);
        if (packed == 0 || IsRespawnableTile(room.GetMetatile(Point(packed))))
            packed = record.DefaultPosition;
        return Point(packed);
    }

    internal void PersistPosition(
        int subId,
        Vector2 position,
        OracleSaveData save)
    {
        int packed = ((Mathf.FloorToInt(position.Y) >> 4) << 4) |
            (Mathf.FloorToInt(position.X) >> 4);
        int row = packed >> 4;
        int column = packed & 0x0f;
        if (row == 0)
            packed += 0x10;
        else if (row == 7)
            packed -= 0x10;
        if (column == 0)
            packed++;
        else if (column == 9)
            packed--;
        save.WriteWramByte(PositionAddress + subId, checked((byte)packed));
    }

    internal void ResetPosition(int subId, OracleSaveData save) =>
        save.WriteWramByte(
            PositionAddress + subId,
            checked((byte)Record(subId).DefaultPosition));

    private static bool IsRespawnableTile(byte tile) => tile is >= 0xc0 and <= 0xca;

    private static Vector2 Point(int packed) => new(
        (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packed >> 4) * OracleRoomData.MetatileSize + 8);
}

internal readonly record struct VineSproutRecord(
    int SubId,
    int DefaultPosition,
    string Sprite,
    int TileBase,
    int Palette,
    bool SourceGrayscaleInverted,
    string Animation,
    int SpeedRaw,
    int PushDelay,
    int MoveFrames,
    int CliffOverlapRadius,
    int CliffGroundProximity,
    int CliffDebrisInteraction,
    string Source);
