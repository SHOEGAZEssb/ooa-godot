using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-derived non-rescue behavior for both placed interactions in room
/// $1:$38: INTERAC_MAKU_SPROUT $88:$00 and the finished-game Link statue
/// INTERAC_MISCELLANEOUS_1 $6b:$15.
/// </summary>
internal sealed class MakuSproutRoomDatabase
{
    private const string Root = "res://assets/oracle/objects/";
    private readonly Dictionary<int, MakuSproutAdviceRecord> _advice = [];

    public MakuSproutRoomRecord Record { get; }
    public IReadOnlyDictionary<int, MakuSproutAdviceRecord> Advice => _advice;
    public Color[] StatuePalette { get; }

    public MakuSproutRoomDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "maku_sprout_room.tsv",
            new GeneratedTableSchema(
                "room 1:38 Maku Sprout interactions",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "sprout-id", "sprout-subid",
                    "sprout-y", "sprout-x", "sprout-sprite",
                    "sprout-tile-base", "sprout-palette",
                    "sprout-animation-0", "sprout-animation-1",
                    "sprout-animation-2", "sprout-radius-y",
                    "sprout-radius-x", "saved-flag", "saved-text-id",
                    "saved-text-position", "saved-text-base64",
                    "finished-flag", "statue-id", "statue-subid",
                    "statue-y", "statue-x", "statue-packed-position",
                    "statue-collision", "statue-radius-y", "statue-radius-x",
                    "statue-appearance-tile", "statue-normal-animation",
                    "statue-alternate-animation", "statue-sprite",
                    "statue-tile-base", "statue-palette",
                    "statue-source-inverted", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new MakuSproutRoomRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.RequiredString(6),
            row.UnsignedDecimal(7),
            row.UnsignedDecimal(8),
            row.RequiredString(9),
            row.RequiredString(10),
            row.RequiredString(11),
            row.HexByte(12),
            row.HexByte(13),
            row.HexByte(14),
            row.HexWord(15),
            row.UnsignedDecimal(16),
            row.Base64Utf8(17),
            row.HexByte(18),
            row.HexByte(19),
            row.HexByte(20),
            row.HexByte(21),
            row.HexByte(22),
            row.HexByte(23),
            row.HexByte(24),
            row.HexByte(25),
            row.HexByte(26),
            row.HexByte(27),
            row.RequiredString(28),
            row.RequiredString(29),
            row.RequiredString(30),
            row.UnsignedDecimal(31),
            row.UnsignedDecimal(32),
            row.Boolean01(33),
            row.RequiredString(34));
        LoadAdvice();

        Color[,] palette = OracleGraphicsData.LoadPalette(
            Root + "maku_sprout_statue_palette.bin", 1);
        StatuePalette =
        [
            palette[0, 0], palette[0, 1], palette[0, 2], palette[0, 3]
        ];
        Validate();
    }

    public bool MatchesRoom(int group, int room) =>
        group == Record.Group && room == Record.Room;

    public bool MatchesSprout(NpcRecord record) =>
        record.Group == Record.Group &&
        record.Room == Record.Room &&
        record.Id == Record.SproutId &&
        record.SubId == Record.SproutSubId &&
        record.Y == Record.SproutY &&
        record.X == Record.SproutX &&
        record.SpriteName == Record.SproutSprite &&
        record.TileBase == Record.SproutTileBase &&
        record.Palette == Record.SproutPalette &&
        record.Implementation == NpcImplementationClassification.EventOwned;

    public MakuSproutAdviceRecord GetAdvice(int state)
    {
        if (_advice.TryGetValue(state, out MakuSproutAdviceRecord record))
            return record;
        throw new InvalidOperationException(
            $"Room 1:38 INTERAC_MAKU_SPROUT has no imported advice " +
            $"for wMakuTreeState=${state:x2}.");
    }

    private void LoadAdvice()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "maku_sprout_advice.tsv",
            new GeneratedTableSchema(
                "room 1:38 Maku Sprout advice",
                GeneratedTableKeySemantics.Unique,
                [
                    "state", "standard-mode", "linked-mode",
                    "standard-first-text-id", "standard-first-position",
                    "standard-first-text-base64", "standard-repeat-text-id",
                    "standard-repeat-position", "standard-repeat-text-base64",
                    "linked-first-text-id", "linked-first-position",
                    "linked-first-text-base64", "linked-repeat-text-id",
                    "linked-repeat-position", "linked-repeat-text-base64"
                ],
                ["state"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new MakuSproutAdviceRecord(
                row.HexByte(0),
                row.Decimal(1, 0, 2),
                row.Decimal(2, 0, 2),
                new MakuSproutDialogue(
                    row.HexWord(3), row.Base64Utf8(5),
                    row.Decimal(4, 0, 2)),
                new MakuSproutDialogue(
                    row.HexWord(6), row.Base64Utf8(8),
                    row.Decimal(7, 0, 2)),
                new MakuSproutDialogue(
                    row.HexWord(9), row.Base64Utf8(11),
                    row.Decimal(10, 0, 2)),
                new MakuSproutDialogue(
                    row.HexWord(12), row.Base64Utf8(14),
                    row.Decimal(13, 0, 2)));
            _advice.Add(record.State, record);
        }
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 1,
                Room: 0x38,
                SproutId: 0x88,
                SproutSubId: 0,
                SproutY: 0x28,
                SproutX: 0x50,
                SproutSprite: "spr_maku_child",
                SproutTileBase: 0,
                SproutPalette: 0,
                SproutRadiusY: 8,
                SproutRadiusX: 8,
                SavedFlag: 0x12,
                SavedTextId: 0x05d5,
                SavedTextPosition: 0,
                FinishedFlag: OracleSaveData.GlobalFlagFinishedGame,
                StatueId: 0x6b,
                StatueSubId: 0x15,
                StatueY: 0x40,
                StatueX: 0x84,
                StatuePackedPosition: 0x48,
                StatueCollision: 0x0f,
                StatueRadiusY: 8,
                StatueRadiusX: 10,
                StatueAppearanceTile: 0xf9,
                StatueSprite: "spr_linkstatue",
                StatueTileBase: 0,
                StatuePalette: 6,
                StatueSourceInverted: false
            } ||
            string.IsNullOrEmpty(Record.SproutAnimation0) ||
            string.IsNullOrEmpty(Record.SproutAnimation1) ||
            string.IsNullOrEmpty(Record.SproutAnimation2) ||
            string.IsNullOrEmpty(Record.StatueNormalAnimation) ||
            string.IsNullOrEmpty(Record.StatueAlternateAnimation) ||
            string.IsNullOrEmpty(Record.SavedText) ||
            StatuePalette.Length != 4)
        {
            throw new InvalidOperationException(
                "Room 1:38 placed-interaction data diverges from the " +
                "Maku Sprout and Link-statue handlers.");
        }

        (int State, int StandardMode, int LinkedMode,
            int StandardFirst, int StandardRepeat,
            int LinkedFirst, int LinkedRepeat)[] expected =
        [
            (0x00, 0, 0, 0x0500, 0x0501, 0x0520, 0x0521),
            (0x03, 1, 1, 0x0570, 0x0570, 0x0590, 0x0590),
            (0x04, 1, 1, 0x0570, 0x0570, 0x0590, 0x0590),
            (0x05, 1, 1, 0x0570, 0x0570, 0x0590, 0x0590),
            (0x06, 0, 0, 0x0576, 0x0577, 0x0596, 0x0597),
            (0x07, 0, 0, 0x0578, 0x0579, 0x0598, 0x0599),
            (0x08, 2, 2, 0x057a, 0x057b, 0x059a, 0x059b),
            (0x09, 1, 1, 0x057c, 0x057c, 0x059c, 0x059c),
            (0x0a, 1, 1, 0x057e, 0x057e, 0x059e, 0x059e),
            (0x0b, 0, 0, 0x0580, 0x0581, 0x05a0, 0x05a1),
            (0x0c, 0, 0, 0x0582, 0x0583, 0x05a2, 0x05a3),
            (0x0d, 1, 1, 0x0584, 0x0584, 0x05a4, 0x05a4),
            (0x0e, 1, 1, 0x0586, 0x0586, 0x05a6, 0x05a6),
            (0x0f, 2, 2, 0x0588, 0x0589, 0x05a8, 0x05a9),
            (0x10, 1, 0, 0x058c, 0x058c, 0x05aa, 0x05ab)
        ];
        if (_advice.Count != expected.Length)
        {
            throw new InvalidOperationException(
                $"Room 1:38 expected {expected.Length} Maku advice states, " +
                $"got {_advice.Count}.");
        }
        foreach (var item in expected)
        {
            MakuSproutAdviceRecord advice = GetAdvice(item.State);
            if (advice.StandardMode != item.StandardMode ||
                advice.LinkedMode != item.LinkedMode ||
                advice.StandardFirst.TextId != item.StandardFirst ||
                advice.StandardRepeat.TextId != item.StandardRepeat ||
                advice.LinkedFirst.TextId != item.LinkedFirst ||
                advice.LinkedRepeat.TextId != item.LinkedRepeat)
            {
                throw new InvalidOperationException(
                    $"Room 1:38 Maku advice state ${item.State:x2} " +
                    "diverges from makuSprout.s.");
            }
            foreach (MakuSproutDialogue dialogue in new[]
            {
                advice.StandardFirst,
                advice.StandardRepeat,
                advice.LinkedFirst,
                advice.LinkedRepeat
            })
            {
                if (dialogue.TextPosition != 2 ||
                    string.IsNullOrEmpty(dialogue.Message))
                {
                    throw new InvalidOperationException(
                        $"Room 1:38 Maku advice TX_{dialogue.TextId:x4} " +
                        "has invalid imported text or textbox position.");
                }
            }
        }
    }
}

internal readonly record struct MakuSproutDialogue(
    int TextId,
    string Message,
    int TextPosition);

internal readonly record struct MakuSproutAdviceRecord(
    int State,
    int StandardMode,
    int LinkedMode,
    MakuSproutDialogue StandardFirst,
    MakuSproutDialogue StandardRepeat,
    MakuSproutDialogue LinkedFirst,
    MakuSproutDialogue LinkedRepeat)
{
    public int Mode(bool linked) => linked ? LinkedMode : StandardMode;

    public MakuSproutDialogue Dialogue(bool linked, bool first) =>
        (linked, first) switch
        {
            (true, true) => LinkedFirst,
            (true, false) => LinkedRepeat,
            (false, true) => StandardFirst,
            _ => StandardRepeat
        };
}

internal readonly record struct MakuSproutRoomRecord(
    int Group,
    int Room,
    int SproutId,
    int SproutSubId,
    int SproutY,
    int SproutX,
    string SproutSprite,
    int SproutTileBase,
    int SproutPalette,
    string SproutAnimation0,
    string SproutAnimation1,
    string SproutAnimation2,
    int SproutRadiusY,
    int SproutRadiusX,
    int SavedFlag,
    int SavedTextId,
    int SavedTextPosition,
    string SavedText,
    int FinishedFlag,
    int StatueId,
    int StatueSubId,
    int StatueY,
    int StatueX,
    int StatuePackedPosition,
    int StatueCollision,
    int StatueRadiusY,
    int StatueRadiusX,
    int StatueAppearanceTile,
    string StatueNormalAnimation,
    string StatueAlternateAnimation,
    string StatueSprite,
    int StatueTileBase,
    int StatuePalette,
    bool StatueSourceInverted,
    string Source)
{
    public NpcRecord CreateStatueNpcRecord() => new(
        Group,
        Room,
        StatueId,
        StatueSubId,
        StatueY,
        StatueX,
        0,
        0,
        StatueSprite,
        StatueTileBase,
        StatuePalette,
        4,
        false,
        StatueNormalAnimation,
        StatueNormalAnimation,
        StatueNormalAnimation,
        StatueNormalAnimation,
        string.Empty,
        NpcImplementationClassification.SpecializedNative);
}
