using Godot;
using System;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported contract for room 0:ba interaction $6b:$06, its temporary
/// room 1:16 Ambi/Nayru scene, and stage 1 of the Black Tower explanation.
/// </summary>
internal sealed class PostD3RemoteMakuDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly NpcDatabase _npcs = new();

    internal PostD3RemoteMakuRecord Record { get; }
    internal Color[] PossessedNayruPalette { get; }

    internal PostD3RemoteMakuDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "post_d3_remote_maku_event.tsv",
            new GeneratedTableSchema(
                "room 0:ba post-D3 remote Maku route",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "essence-mask",
                    "room-flag", "initial-wait", "flash-frames",
                    "fade-frames", "palace-group", "palace-room", "ambi-id",
                    "ambi-subid", "ambi-y", "ambi-x", "nayru-id",
                    "nayru-subid", "nayru-y", "nayru-x", "palace-wait",
                    "palace-post-wait", "palace-text-id",
                    "palace-text-base64", "explanation-wait",
                    "explanation-post-wait", "explanation-text-id",
                    "explanation-text-base64", "explanation-textbox-flags",
                    "screen-offset-y", "return-y", "return-x",
                    "return-direction", "past-flag-group", "past-flag-room",
                    "past-room-flag", "standard-global-flag", "music",
                    "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new PostD3RemoteMakuRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2),
            row.HexByte(3), row.HexByte(4), row.HexByte(5),
            row.UnsignedDecimal(6), row.UnsignedDecimal(7),
            row.UnsignedDecimal(8), row.Decimal(9, 0, 7), row.HexByte(10),
            row.HexByte(11), row.HexByte(12), row.HexByte(13), row.HexByte(14),
            row.HexByte(15), row.HexByte(16), row.HexByte(17), row.HexByte(18),
            row.UnsignedDecimal(19), row.UnsignedDecimal(20), row.HexWord(21),
            row.Base64Utf8(22), row.UnsignedDecimal(23),
            row.UnsignedDecimal(24), row.HexWord(25), row.Base64Utf8(26),
            row.HexByte(27), row.HexByte(28), row.HexByte(29), row.HexByte(30),
            row.HexByte(31), row.Decimal(32, 0, 7), row.HexByte(33),
            row.HexByte(34), row.HexByte(35), row.HexByte(36),
            row.RequiredString(37));
        PossessedNayruPalette = ReadPossessedPalette();
        Validate();
    }

    internal NpcRecord CreateAmbiRecord() => RequireTemplate(0x4d, 0x00) with
    {
        SubId = Record.AmbiSubId,
        Y = Record.AmbiY,
        X = Record.AmbiX,
        TextId = 0,
        Message = string.Empty,
        Implementation = NpcImplementationClassification.EventOwned
    };

    internal NpcRecord CreateNayruRecord() => RequireTemplate(0x36, 0x01) with
    {
        SubId = Record.NayruSubId,
        Y = Record.NayruY,
        X = Record.NayruX,
        TextId = 0,
        Message = string.Empty,
        Implementation = NpcImplementationClassification.EventOwned
    };

    private NpcRecord RequireTemplate(int id, int subId)
    {
        NpcRecord[] matches = _npcs.GetRoomNpcs(
            Record.PalaceGroup, Record.PalaceRoom).Where(
                record => record.Id == id && record.SubId == subId).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Room {Record.PalaceGroup:x}:{Record.PalaceRoom:x2} should " +
                $"contain one actor template ${id:x2}:${subId:x2}.");
        }
        return matches[0];
    }

    private static Color[] ReadPossessedPalette()
    {
        const string path =
            "res://assets/oracle/metadata/nayru_possessed_palette.bin";
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length != 12)
        {
            throw new InvalidOperationException(
                $"Possessed Nayru palette should contain 12 bytes, got " +
                $"{bytes.Length}.");
        }
        var colors = new Color[4];
        colors[0] = Colors.Transparent;
        for (int color = 1; color < colors.Length; color++)
        {
            int offset = color * 3;
            colors[color] = new Color(
                bytes[offset] / 31.0f,
                bytes[offset + 1] / 31.0f,
                bytes[offset + 2] / 31.0f);
        }
        return colors;
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 0, Room: 0xba, InteractionId: 0x6b, SubId: 0x06,
                EssenceMask: 0x04, RoomFlag: 0x40, InitialWait: 90,
                FlashFrames: 13, FadeFrames: 32, PalaceGroup: 1,
                PalaceRoom: 0x16, AmbiId: 0x4d, AmbiSubId: 0x08,
                AmbiY: 0x28, AmbiX: 0x48, NayruId: 0x36,
                NayruSubId: 0x0e, NayruY: 0x28, NayruX: 0x58,
                PalaceWait: 60, PalacePostWait: 60,
                PalaceTextId: 0x1316, ExplanationWait: 60,
                ExplanationPostWait: 60, ExplanationTextId: 0x1317,
                ExplanationTextboxFlags: 0x01, ScreenOffsetY: 0x70,
                ReturnY: 0x65, ReturnX: 0x58, ReturnDirection: 0x02,
                PastFlagGroup: 1, PastFlagRoom: 0x76,
                PastRoomFlag: 0x01, StandardGlobalFlag: 0x1d,
                Music: OracleSoundEngine.MusDisaster
            } || PossessedNayruPalette.Length != 4)
        {
            throw new InvalidOperationException(
                "Room 0:ba post-D3 route diverged from the imported source contract.");
        }
        _ = CreateAmbiRecord();
        _ = CreateNayruRecord();
    }
}

internal readonly record struct PostD3RemoteMakuRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int EssenceMask,
    int RoomFlag,
    int InitialWait,
    int FlashFrames,
    int FadeFrames,
    int PalaceGroup,
    int PalaceRoom,
    int AmbiId,
    int AmbiSubId,
    int AmbiY,
    int AmbiX,
    int NayruId,
    int NayruSubId,
    int NayruY,
    int NayruX,
    int PalaceWait,
    int PalacePostWait,
    int PalaceTextId,
    string PalaceText,
    int ExplanationWait,
    int ExplanationPostWait,
    int ExplanationTextId,
    string ExplanationText,
    int ExplanationTextboxFlags,
    int ScreenOffsetY,
    int ReturnY,
    int ReturnX,
    int ReturnDirection,
    int PastFlagGroup,
    int PastFlagRoom,
    int PastRoomFlag,
    int StandardGlobalFlag,
    int Music,
    string Source);
