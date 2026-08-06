using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class TokayTheftEventDatabase
{
    private readonly Dictionary<int, TokayAccessoryRecord> _accessories;
    internal TokayTheftEventRecord Record { get; }
    internal IntroSpriteFrame[] LinkFrames { get; }
    internal int LinkLoopStart { get; }

    internal TokayTheftEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/tokay_theft_event.tsv",
            new GeneratedTableSchema(
                "room 1:aa Tokay theft event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "interaction-id", "room-flag",
                    "main-subid", "link-wait", "steal-first-wait",
                    "steal-repeat-wait", "item-wait", "final-wait",
                    "down-speed", "right-speed", "jump-speed", "jump-angle",
                    "jump-z-fixed", "jump-gravity-fixed", "initial-animations",
                    "animation0", "animation1", "animation3", "animation5",
                    "stolen-items", "accessory-subids", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new TokayTheftEventRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2),
            (byte)row.HexByte(3), row.HexByte(4), row.HexByte(5),
            row.HexByte(6), row.HexByte(7), row.HexByte(8), row.HexByte(9),
            row.HexByte(10), row.HexByte(11), row.HexByte(12), row.HexByte(13),
            unchecked((short)row.HexWord(14)), row.HexWord(15),
            ParseHexBytes(row, 16),
            [row.RequiredString(17), row.RequiredString(18),
             row.RequiredString(19), row.RequiredString(20)],
            ParseHexBytes(row, 21), ParseHexBytes(row, 22),
            row.RequiredString(23));

        GeneratedTable accessories = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/tokay_theft_accessories.tsv",
            new GeneratedTableSchema(
                "Tokay theft accessories",
                GeneratedTableKeySemantics.Unique,
                ["item-index", "subid", "sprite", "tile-base", "palette", "animation"],
                ["item-index"],
                headerRequired: true));
        _accessories = accessories.Rows.Select(accessory =>
            new TokayAccessoryRecord(
                accessory.UnsignedDecimal(0), accessory.HexByte(1),
                accessory.RequiredString(2), accessory.UnsignedDecimal(3),
                accessory.UnsignedDecimal(4), accessory.RequiredString(5)))
            .ToDictionary(accessory => accessory.ItemIndex);

        GeneratedTable linkVisual = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/tokay_theft_link_visual.tsv",
            new GeneratedTableSchema(
                "Tokay theft Link visual",
                GeneratedTableKeySemantics.Ordered,
                ["index", "duration", "gfx-index", "anim-parameter",
                 "source-offset", "base-palette", "oam-parts", "loop-start", "source"],
                headerRequired: true));
        var linkFrames = new List<IntroSpriteFrame>();
        var graphics = new List<int>();
        var parameters = new List<int>();
        int? loopStart = null;
        foreach (GeneratedTableRow visual in linkVisual.Rows)
        {
            if (visual.UnsignedDecimal(0) != linkFrames.Count)
                throw new InvalidOperationException("Tokay Link frames are not ordered.");
            IntroOamPart[] parts = visual.RequiredString(6).Split(';').Select(value =>
            {
                string[] fields = value.Split(',');
                if (fields.Length != 4)
                    throw new InvalidOperationException("Malformed Tokay Link OAM part.");
                return new IntroOamPart(
                    Convert.ToInt32(fields[0], 16), Convert.ToInt32(fields[1], 16),
                    Convert.ToInt32(fields[2], 16), Convert.ToInt32(fields[3], 16));
            }).ToArray();
            linkFrames.Add(new IntroSpriteFrame(
                visual.UnsignedDecimal(1), visual.HexWord(4),
                visual.UnsignedDecimal(5), parts));
            graphics.Add(visual.HexByte(2));
            parameters.Add(visual.HexByte(3));
            int rowLoopStart = visual.UnsignedDecimal(7);
            loopStart ??= rowLoopStart;
            if (loopStart != rowLoopStart)
                throw new InvalidOperationException("Tokay Link loop start is inconsistent.");
        }
        LinkFrames = linkFrames.ToArray();
        LinkLoopStart = loopStart ?? -1;
        if (LinkFrames.Select(frame => frame.Duration).ToArray() is not
                [180, 127] ||
            graphics.ToArray() is not [0x04, 0x56] ||
            parameters.ToArray() is not [0, 0] ||
            LinkLoopStart != 1)
        {
            throw new InvalidOperationException(
                "Tokay linkCutscene7 animation $14 diverges from the source contract.");
        }
        Validate();
    }

    internal TokayAccessoryRecord Accessory(int itemIndex) =>
        _accessories.TryGetValue(itemIndex, out TokayAccessoryRecord record)
            ? record
            : throw new InvalidOperationException(
                $"Missing Tokay theft accessory index ${itemIndex:x2}.");

    internal string Animation(int animation) => animation switch
    {
        0 => Record.Animations[0],
        1 => Record.Animations[1],
        3 => Record.Animations[2],
        5 => Record.Animations[3],
        _ => throw new InvalidOperationException(
            $"Unsupported Tokay theft animation ${animation:x2}.")
    };

    private void Validate()
    {
        if (Record is not
            {
                Group: 1, Room: 0xaa, InteractionId: 0x48,
                RoomFlag: 0x40, MainSubId: 2, LinkWait: 0xf0,
                StealFirstWait: 0x46, StealRepeatWait: 0x0a,
                ItemWait: 0x5a, FinalWait: 0x3c,
                DownSpeed: 0x50, RightSpeed: 0x14,
                JumpSpeed: 0x64, JumpAngle: 6,
                JumpZFixed: -0x1c0, JumpGravityFixed: 0x20
            } ||
            Record.InitialAnimations is not [1, 3, 0, 1, 3] ||
            Record.StolenItems is not
                [0x05, 0x15, 0x11, 0x2e, 0x19, 0x01, 0x03, 0x16, 0x17] ||
            Record.AccessorySubIds is not [0x10, 0x1b, 0x68, 0x31, 0x20] ||
            Record.Animations.Length != 4 || _accessories.Count != 5)
        {
            throw new InvalidOperationException(
                "Room 1:aa Tokay theft import diverges from the source contract.");
        }
    }

    private static int[] ParseHexBytes(GeneratedTableRow row, int column) =>
        row.RequiredString(column).Split(',').Select(value =>
            Convert.ToInt32(value, 16)).ToArray();
}

internal readonly record struct TokayTheftEventRecord(
    int Group, int Room, int InteractionId, byte RoomFlag, int MainSubId,
    int LinkWait, int StealFirstWait, int StealRepeatWait, int ItemWait,
    int FinalWait, int DownSpeed, int RightSpeed, int JumpSpeed, int JumpAngle,
    int JumpZFixed, int JumpGravityFixed, int[] InitialAnimations,
    string[] Animations, int[] StolenItems, int[] AccessorySubIds, string Source);

internal readonly record struct TokayAccessoryRecord(
    int ItemIndex, int SubId, string Sprite, int TileBase, int Palette,
    string Animation);
