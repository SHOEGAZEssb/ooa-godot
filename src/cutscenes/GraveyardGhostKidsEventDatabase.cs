using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported native-state parameters for the three children in present room
/// $0:$7b. Their scripts share cfd1 and run in object-list order.
/// </summary>
internal sealed class GraveyardGhostKidsEventDatabase
{
    public EventRecord Record { get; }
    public IReadOnlyList<TextRecord> Texts { get; }

    public GraveyardGhostKidsEventDatabase()
    {
        Record = LoadEvent();
        Texts = LoadTexts();
        Validate();
    }

    private static EventRecord LoadEvent()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/graveyard_ghost_kids_event.tsv",
            new GeneratedTableSchema(
                "Spirit's Grave child event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "room-flag", "red-id", "red-subid", "red-palette",
                    "red-initial-animation", "green-id", "green-subid", "green-initial-animation",
                    "blue-id", "blue-subid", "blue-initial-animation", "green-initial-wait",
                    "jump-speed-z", "jump-gravity", "jump-sound", "post-jump-wait",
                    "green-post-text-wait", "red-freeze-wait", "red-post-text-wait",
                    "red-left-animation", "red-up-animation", "red-final-wait", "shake-frames",
                    "flee-speed", "flee-counter", "flee-angle", "flee-animation", "flee-sound"
                ],
                headerRequired: true)).SingleRow();

        return new EventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.HexByte(7),
            row.HexByte(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.UnsignedDecimal(13),
            row.Decimal(14),
            row.Decimal(15),
            row.HexByte(16),
            row.UnsignedDecimal(17),
            row.UnsignedDecimal(18),
            row.UnsignedDecimal(19),
            row.UnsignedDecimal(20),
            row.HexByte(21),
            row.HexByte(22),
            row.UnsignedDecimal(23),
            row.UnsignedDecimal(24),
            row.HexByte(25),
            row.HexByte(26),
            row.HexByte(27),
            row.HexByte(28),
            row.HexByte(29));
    }

    private static IReadOnlyList<TextRecord> LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/graveyard_ghost_kids_text.tsv",
            new GeneratedTableSchema(
                "Spirit's Grave child event text",
                GeneratedTableKeySemantics.Unique,
                ["order", "actor", "text-id", "text-base64"],
                ["order"],
                headerRequired: true));
        var records = new List<TextRecord>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            records.Add(new TextRecord(
                row.UnsignedDecimal(0),
                row.RequiredString(1),
                row.HexWord(2),
                row.Base64Utf8(3)));
        }
        return records;
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 0,
                Room: 0x7b,
                RoomFlag: OracleSaveData.RoomFlag40,
                RedId: 0x3c,
                RedSubId: 0x03,
                RedPalette: 0x02,
                GreenId: 0x3c,
                GreenSubId: 0x04,
                BlueId: 0x3f,
                BlueSubId: 0x02,
                FleeAngle: 0x08
            } ||
            Texts.Count != 5 ||
            Texts[0] is not { Order: 0, Actor: "Green", TextId: 0x2511 } ||
            Texts[1] is not { Order: 1, Actor: "Blue", TextId: 0x2911 } ||
            Texts[2] is not { Order: 2, Actor: "Red", TextId: 0x2512 } ||
            Texts[3] is not { Order: 3, Actor: "Red", TextId: 0x2513 } ||
            Texts[4] is not { Order: 4, Actor: "Red", TextId: 0x2514 })
        {
            throw new InvalidOperationException(
                "Spirit's Grave child event metadata diverges from room $0:$7b.");
        }
    }

    internal readonly record struct EventRecord(
        int Group,
        int Room,
        int RoomFlag,
        int RedId,
        int RedSubId,
        int RedPalette,
        int RedInitialAnimation,
        int GreenId,
        int GreenSubId,
        int GreenInitialAnimation,
        int BlueId,
        int BlueSubId,
        int BlueInitialAnimation,
        int GreenInitialWait,
        int JumpSpeedZ,
        int JumpGravity,
        int JumpSound,
        int PostJumpWait,
        int GreenPostTextWait,
        int RedFreezeWait,
        int RedPostTextWait,
        int RedLeftAnimation,
        int RedUpAnimation,
        int RedFinalWait,
        int ShakeFrames,
        int FleeSpeed,
        int FleeCounter,
        int FleeAngle,
        int FleeAnimation,
        int FleeSound);

    internal readonly record struct TextRecord(
        int Order,
        string Actor,
        int TextId,
        string Message);
}
