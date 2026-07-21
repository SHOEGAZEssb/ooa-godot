using Godot;
using System;
using System.Collections.Generic;
using System.Text;

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
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/graveyard_ghost_kids_event.tsv");
        string? row = FirstDataRow(source);
        if (row is null)
            throw new InvalidOperationException("Spirit's Grave child event data is empty.");

        string[] columns = row.Split('\t');
        if (columns.Length != 30)
        {
            throw new InvalidOperationException(
                $"Spirit's Grave event row should contain 30 columns, got {columns.Length}.");
        }

        return new EventRecord(
            ParseDecimal(columns[0]),
            ParseHex(columns[1]),
            ParseHex(columns[2]),
            ParseHex(columns[3]),
            ParseHex(columns[4]),
            ParseHex(columns[5]),
            ParseHex(columns[6]),
            ParseHex(columns[7]),
            ParseHex(columns[8]),
            ParseHex(columns[9]),
            ParseHex(columns[10]),
            ParseHex(columns[11]),
            ParseHex(columns[12]),
            ParseDecimal(columns[13]),
            ParseDecimal(columns[14]),
            ParseDecimal(columns[15]),
            ParseHex(columns[16]),
            ParseDecimal(columns[17]),
            ParseDecimal(columns[18]),
            ParseDecimal(columns[19]),
            ParseDecimal(columns[20]),
            ParseHex(columns[21]),
            ParseHex(columns[22]),
            ParseDecimal(columns[23]),
            ParseDecimal(columns[24]),
            ParseHex(columns[25]),
            ParseHex(columns[26]),
            ParseHex(columns[27]),
            ParseHex(columns[28]),
            ParseHex(columns[29]));
    }

    private static IReadOnlyList<TextRecord> LoadTexts()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/graveyard_ghost_kids_text.tsv");
        var records = new List<TextRecord>();
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            if (columns.Length != 4)
            {
                throw new InvalidOperationException(
                    $"Spirit's Grave text row should contain 4 columns, got {columns.Length}.");
            }
            records.Add(new TextRecord(
                ParseDecimal(columns[0]),
                columns[1],
                ParseHex(columns[2]),
                Encoding.UTF8.GetString(Convert.FromBase64String(columns[3]))));
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

    private static string? FirstDataRow(string source)
    {
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
                return line;
        }
        return null;
    }

    private static int ParseDecimal(string value) => int.Parse(value);
    private static int ParseHex(string value) => Convert.ToInt32(value, 16);

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
