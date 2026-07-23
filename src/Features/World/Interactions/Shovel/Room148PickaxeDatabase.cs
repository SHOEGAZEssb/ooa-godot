using System;

namespace oracleofages;

/// <summary>
/// Imported script-selected visuals, text, sound, and dirt-chip physics for
/// INTERAC_PICKAXE_WORKER $57:$00 in past room $48.
/// </summary>
internal sealed class Room148PickaxeDatabase
{

    public PickaxeRecord Record { get; }

    public Room148PickaxeDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/room148_pickaxe.tsv",
            new GeneratedTableSchema(
                "room 1:48 pickaxe worker",
                GeneratedTableKeySemantics.Ordered,
                [
                    "worker-sprite", "worker-tile-base", "worker-palette", "work-animation",
                    "talk-animation", "debris-sprite", "debris-tile-base", "debris-animation",
                    "text-id", "utf8-base64", "sound", "debris-count", "offset-y",
                    "offset-x", "speed", "speed-z", "gravity", "angle-0", "angle-1"
                ],
                headerRequired: true));
        if (table.Rows.Count != 1)
            throw new InvalidOperationException(
                $"Room 1:48 pickaxe data should have one row, got {table.Rows.Count}.");
        GeneratedTableRow row = table.Rows[0];

        Record = new PickaxeRecord(
            row.RequiredString(0),
            row.UnsignedDecimal(1),
            row.UnsignedDecimal(2),
            row.RequiredString(3),
            row.RequiredString(4),
            row.RequiredString(5),
            row.UnsignedDecimal(6),
            row.RequiredString(7),
            row.HexWord(8),
            row.Base64Utf8(9),
            row.UnsignedDecimal(10),
            row.UnsignedDecimal(11),
            row.Decimal(12),
            row.Decimal(13),
            row.UnsignedDecimal(14),
            row.Decimal(15),
            row.UnsignedDecimal(16),
            row.UnsignedDecimal(17),
            row.UnsignedDecimal(18));

        AnimationDefinition work =
            OracleGraphicsCache.GetAnimationDefinition(Record.WorkAnimation);
        AnimationDefinition talk =
            OracleGraphicsCache.GetAnimationDefinition(Record.TalkAnimation);
        AnimationDefinition debris =
            OracleGraphicsCache.GetAnimationDefinition(Record.DebrisAnimation);
        bool hasLeftStrike = Array.Exists(
            work.Frames, frame => frame.Parameter == 1);
        bool hasRightStrike = Array.Exists(
            work.Frames, frame => frame.Parameter == 2);
        if (Record.DebrisSpriteName != "spr_common_sprites" ||
            Record.DebrisTileBase != 0x02 ||
            Record.TextId != 0x1b00 || string.IsNullOrEmpty(Record.Message) ||
            Record.DebrisCount != 2 || Record.OffsetY != 4 ||
            Record.OffsetX != 14 || Record.Speed <= 0 ||
            Record.InitialSpeedZ >= 0 || Record.Gravity <= 0 ||
            Record.Angle0 != 0x08 || Record.Angle1 != 0x18 ||
            work.Frames.Length == 0 || !hasLeftStrike || !hasRightStrike ||
            talk.Frames.Length == 0 || debris.Frames.Length == 0)
        {
            throw new InvalidOperationException(
                "Room 1:48 pickaxe data does not match the imported $57:$00/$92:$06 contract.");
        }
    }
}
