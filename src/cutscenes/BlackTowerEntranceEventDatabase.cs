using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

/// <summary>Imported room $1:$86 guard script and stage-0 tower scene.</summary>
internal sealed class BlackTowerEntranceEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";

    public EventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> First { get; }
    public IReadOnlyList<CutsceneCommand> Aftermath { get; }
    public IReadOnlyList<OamRecord> Oam { get; }
    public Color[,] BackgroundPalettes { get; }
    public Color[,] SpritePalettes { get; }

    public BlackTowerEntranceEventDatabase()
    {
        string row = FirstDataRow(Root + "black_tower_entrance_event.tsv");
        string[] fields = row.Split('\t');
        if (fields.Length != 21)
            throw new InvalidOperationException(
                $"Black Tower entrance row should have 21 fields, got {fields.Length}.");
        Record = new EventRecord(
            int.Parse(fields[0]), Hex(fields[1]), Hex(fields[2]), Hex(fields[3]),
            Hex(fields[4]), Hex(fields[5]), Hex(fields[6]), Hex(fields[7]),
            Hex(fields[8]), Hex(fields[9]), Hex(fields[10]), Hex(fields[11]),
            Hex(fields[12]), Hex(fields[13]), Hex(fields[14]), int.Parse(fields[15]),
            int.Parse(fields[16]), Hex(fields[17]), Hex(fields[18]), Hex(fields[19]),
            Encoding.UTF8.GetString(Convert.FromBase64String(fields[20])));

        First = CutsceneCommandCatalog.Load(
            Root + "black_tower_guard_first.tsv");
        Aftermath = CutsceneCommandCatalog.Load(
            Root + "black_tower_guard_aftermath.tsv");
        Oam = LoadOam();
        BackgroundPalettes = LoadPalettes(
            Root + "black_tower_bg_palette.bin", 7, transparentZero: false,
            destinationStart: 1);
        SpritePalettes = LoadPalettes(
            Root + "black_tower_sprite_palette.bin", 8, transparentZero: true,
            destinationStart: 0);
        Validate();
    }

    private void Validate()
    {
        if (Record is not
            { Group: 1, Room: 0x86, GuardId: 0x58, GuardSubId: 0x02,
              EssenceMask: 0x08, ItemFlag: OracleSaveData.RoomFlagItem,
              AftermathFlag: OracleSaveData.RoomFlag40,
              CompleteFlag: OracleSaveData.RoomFlag80,
              MoveSpeed: 0x14, MoveCounter: 0x21,
              SourceTransition: 0x04, DestinationTransition: 0x0c,
              ExplanationTextId: 0x1005 } ||
            First.Count != 8 || First[^1] is not CutsceneEndCommand ||
            Aftermath.Count != 16 || Aftermath[^1] is not CutsceneEndCommand ||
            First[1] is not CutsceneShowTextCommand { TextId: 0x1003 } ||
            First[3] is not CutsceneOrRoomFlagCommand { Flag: 0x40 } ||
            Aftermath[4] is not CutsceneShowTextCommand { TextId: 0x1006 } ||
            Aftermath[9] is not CutsceneMoveCommand
                { Actor: "Guard", Angle: 0x08, Counter: 0x21 } ||
            Aftermath[12] is not CutsceneOrRoomFlagCommand { Flag: 0x80 } ||
            Oam.Count != 16)
        {
            throw new InvalidOperationException(
                "Room 1:86 guard data diverges from hardhatWorkerSubid02Script.");
        }
    }

    private static IReadOnlyList<OamRecord> LoadOam()
    {
        var result = new List<OamRecord>();
        string source = FileAccess.GetFileAsString(
            Root + "black_tower_stage_0_oam.tsv");
        foreach (string raw in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 6 || int.Parse(fields[0]) != result.Count)
                throw new InvalidOperationException($"Malformed Black Tower OAM row: {line}");
            result.Add(new OamRecord(
                Hex(fields[1]), Hex(fields[2]), Hex(fields[3]),
                Hex(fields[4]), fields[5]));
        }
        return result;
    }

    private static Color[,] LoadPalettes(
        string path,
        int sourceCount,
        bool transparentZero,
        int destinationStart)
    {
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length != sourceCount * 12)
            throw new InvalidOperationException(
                $"{path} should contain {sourceCount * 12} bytes, got {bytes.Length}.");
        var result = new Color[8, 4];
        for (int palette = 0; palette < sourceCount; palette++)
        for (int shade = 0; shade < 4; shade++)
        {
            int offset = (palette * 4 + shade) * 3;
            Color color = new(
                bytes[offset] / 31.0f,
                bytes[offset + 1] / 31.0f,
                bytes[offset + 2] / 31.0f,
                transparentZero && shade == 0 ? 0.0f : 1.0f);
            result[palette + destinationStart, shade] = color;
        }
        return result;
    }

    private static string FirstDataRow(string path)
    {
        foreach (string raw in FileAccess.GetFileAsString(path).Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.TrimEnd('\r');
            if (!line.StartsWith('#'))
                return line;
        }
        throw new InvalidOperationException($"{path} is empty.");
    }

    private static int Hex(string value) => Convert.ToInt32(value, 16);

    internal readonly record struct OamRecord(
        int Y, int X, int Tile, int Flags, string Source);

    internal readonly record struct EventRecord(
        int Group,
        int Room,
        int GuardId,
        int GuardSubId,
        int EssenceMask,
        int ItemFlag,
        int AftermathFlag,
        int CompleteFlag,
        int InitialY,
        int InitialX,
        int CompletedY,
        int CompletedX,
        int MoveSpeed,
        int MoveCounter,
        int ScreenOffsetY,
        int IntroWait,
        int PostWait,
        int SourceTransition,
        int DestinationTransition,
        int ExplanationTextId,
        string ExplanationText);
}
