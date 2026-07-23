using System;

namespace oracleofages;

/// <summary>
/// Native ITEM_BRACELET ($16) parent/child constants imported from the
/// supported Ages disassembly.
/// </summary>
internal sealed class BraceletDatabase
{
    internal readonly record struct Record(
        int Item,
        int PickupSound,
        int ThrowSound,
        int Damage,
        int RadiusY,
        int RadiusX,
        int CollisionZRadius,
        int Gravity,
        int InitialSpeedZ,
        int SpeedRaw,
        int TossSpeedRaw,
        int PushSpeedRaw,
        int PushFrames,
        int PowerGlovePushSpeedRaw,
        int PowerGlovePushFrames,
        int HeavyPropertyMask,
        int GrabPullFrames,
        int LiftLowFrames,
        int LiftMidFrames,
        int LiftHighFrames,
        int ThrowFrames,
        string Source);

    internal Record Data { get; }

    internal BraceletDatabase(
        string path = "res://assets/oracle/metadata/bracelet.tsv")
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "bracelet",
                GeneratedTableKeySemantics.Unique,
                [
                    "item", "pickup-sound", "throw-sound", "damage",
                    "radius-y", "radius-x", "collision-z-radius",
                    "gravity", "initial-speed-z",
                    "speed-raw", "toss-speed-raw", "push-speed-raw",
                    "push-frames", "power-glove-push-speed-raw",
                    "power-glove-push-frames", "heavy-property-mask",
                    "grab-pull-frames",
                    "lift-low-frames", "lift-mid-frames", "lift-high-frames",
                    "throw-frames", "source"
                ],
                ["item"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one ITEM_BRACELET record, got {table.Rows.Count}.");
        }

        GeneratedTableRow row = table.Rows[0];
        Data = new Record(
            row.HexByte(0),
            row.HexByte(1),
            row.HexByte(2),
            row.UnsignedDecimal(3),
            row.UnsignedDecimal(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.UnsignedDecimal(7),
            row.Decimal(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.UnsignedDecimal(12),
            row.HexByte(13),
            row.UnsignedDecimal(14),
            row.HexByte(15),
            row.UnsignedDecimal(16),
            row.UnsignedDecimal(17),
            row.UnsignedDecimal(18),
            row.UnsignedDecimal(19),
            row.UnsignedDecimal(20),
            row.RequiredString(21));
        Validate(Data);
    }

    private static void Validate(Record record)
    {
        if (record.Item != InventoryState.ItemBracelet ||
            record.PickupSound != OracleSoundEngine.SndPickup ||
            record.ThrowSound != OracleSoundEngine.SndThrow ||
            record.Damage != 3 ||
            record.RadiusY != 6 || record.RadiusX != 6 ||
            record.CollisionZRadius != 7 ||
            record.Gravity != 0x1c || record.InitialSpeedZ != -0xf0 ||
            record.SpeedRaw != 0x3c || record.TossSpeedRaw != 0x64 ||
            record.PushSpeedRaw != 0x14 || record.PushFrames != 0x20 ||
            record.PowerGlovePushSpeedRaw != 0x1e ||
            record.PowerGlovePushFrames != 0x15 ||
            record.HeavyPropertyMask != 0x20 ||
            record.GrabPullFrames != 11 ||
            record.LiftLowFrames != 7 ||
            record.LiftMidFrames != 4 ||
            record.LiftHighFrames != 2 ||
            record.ThrowFrames != 8)
        {
            throw new InvalidOperationException(
                $"Invalid ITEM_BRACELET record imported from {record.Source}.");
        }
    }
}
