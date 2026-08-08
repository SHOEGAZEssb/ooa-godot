using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// The two INTERAC_DECORATION $80 eyes and invisible INTERAC_PIRATE $c4:$04
/// socket at Crescent Island's southern dungeon entrance.
/// </summary>
internal sealed class TokayEntranceEyeDatabase
{
    private readonly List<TokayEntranceEyeRecord> _eyes = new();

    internal IReadOnlyList<TokayEntranceEyeRecord> Eyes => _eyes;
    internal TokayEyeballSlotRecord Slot { get; }

    internal TokayEntranceEyeDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_entrance_eyes.tsv",
            new GeneratedTableSchema(
                "Tokay entrance eyes",
                GeneratedTableKeySemantics.Ordered,
                [
                    "order", "group", "room", "id", "subid", "y", "x",
                    "room-flag-required", "sprite", "tile-base", "palette",
                    "animation", "source"
                ],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new TokayEntranceEyeRecord(
                row.UnsignedDecimal(0), row.Decimal(1, 0, 7), row.HexByte(2),
                row.HexByte(3), row.HexByte(4), row.HexByte(5), row.HexByte(6),
                row.HexByte(7), row.RequiredString(8), row.HexByte(9),
                row.HexByte(10), row.RequiredString(11), row.RequiredString(12));
            if (record.Order != _eyes.Count)
                throw row.Invalid(0, $"ordered eye index {_eyes.Count}");
            _eyes.Add(record);
        }

        table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_eyeball_slot.tsv",
            new GeneratedTableSchema(
                "Tokay eyeball socket",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "id", "subid", "room-flag", "treasure",
                    "push-delay", "eye-y", "eye-x", "eye-wait", "shake-frames",
                    "shake-wait", "open-wait", "open-position", "open-tiles",
                    "puff-y", "puff-x", "source"
                ],
                ["group", "room", "id", "subid"],
                headerRequired: true));
        if (table.Rows.Count != 1)
            throw new InvalidOperationException(
                $"Tokay eyeball socket should have one row, got {table.Rows.Count}.");
        GeneratedTableRow slot = table.Rows[0];
        Slot = new TokayEyeballSlotRecord(
            slot.Decimal(0, 0, 7), slot.HexByte(1), slot.HexByte(2),
            slot.HexByte(3), slot.HexByte(4), slot.HexByte(5),
            slot.UnsignedDecimal(6), slot.HexByte(7), slot.HexByte(8),
            slot.UnsignedDecimal(9), slot.UnsignedDecimal(10),
            slot.UnsignedDecimal(11), slot.UnsignedDecimal(12),
            slot.HexByte(13), SplitHexBytes(slot, 14), slot.HexByte(15),
            slot.HexByte(16), slot.RequiredString(17));

        if (_eyes.Count != 2 ||
            _eyes[0] is not { Group: 1, Room: 0xba, Id: 0x80, SubId: 0x05,
                Y: 0x52, X: 0x46, RequiredRoomFlag: 0 } ||
            _eyes[1] is not { Group: 1, Room: 0xba, Id: 0x80, SubId: 0x06,
                Y: 0x52, X: 0x6a, RequiredRoomFlag: 0x80 } ||
            Slot is not { Group: 1, Room: 0xba, Id: 0xc4, SubId: 0x04,
                RoomFlag: 0x80, Treasure: 0x4f, PushDelay: 10,
                EyeWait: 60, ShakeFrames: 160, ShakeWait: 120,
                OpenWait: 60, OpenPosition: 0x54 } ||
            Slot.OpenTiles is not [0xa2, 0xef, 0xa4])
        {
            throw new InvalidOperationException(
                "Imported southern Tokay entrance contract is incomplete.");
        }
    }

    internal IEnumerable<TokayEntranceEyeRecord> GetRoomEyes(
        int group,
        int room)
    {
        foreach (TokayEntranceEyeRecord eye in _eyes)
        {
            if (eye.Group == group && eye.Room == room)
                yield return eye;
        }
    }

    internal TokayEntranceEyeRecord InsertedEye => _eyes[1];

    private static int[] SplitHexBytes(GeneratedTableRow row, int column)
    {
        string[] values = row.RequiredString(column).Split(',');
        var result = new int[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            if (!int.TryParse(
                    values[index],
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out result[index]) ||
                result[index] is < 0 or > 0xff)
            {
                throw row.Invalid(column, "comma-separated hexadecimal bytes");
            }
        }
        return result;
    }
}

internal readonly record struct TokayEntranceEyeRecord(
    int Order,
    int Group,
    int Room,
    int Id,
    int SubId,
    int Y,
    int X,
    int RequiredRoomFlag,
    string Sprite,
    int TileBase,
    int Palette,
    string Animation,
    string Source);

internal sealed record TokayEyeballSlotRecord(
    int Group,
    int Room,
    int Id,
    int SubId,
    int RoomFlag,
    int Treasure,
    int PushDelay,
    int EyeY,
    int EyeX,
    int EyeWait,
    int ShakeFrames,
    int ShakeWait,
    int OpenWait,
    int OpenPosition,
    int[] OpenTiles,
    int PuffY,
    int PuffX,
    string Source);
