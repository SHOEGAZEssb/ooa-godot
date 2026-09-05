using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Imported room $1:$86 guard script and stage-0 tower scene.</summary>
internal sealed class BlackTowerEntranceEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";

    public BlackTowerEntranceEventDatabaseEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> First { get; }
    public IReadOnlyList<CutsceneCommand> Aftermath { get; }
    public IReadOnlyList<OamRecord> Oam { get; }
    public IReadOnlyList<OamRecord> Stage1Oam { get; }
    public Color[,] BackgroundPalettes { get; }
    public Color[,] SpritePalettes { get; }

    public BlackTowerEntranceEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "black_tower_entrance_event.tsv",
            new GeneratedTableSchema(
                "Black Tower entrance event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "guard-id", "guard-subid", "essence-mask", "item-flag",
                    "aftermath-flag", "complete-flag", "initial-y", "initial-x", "completed-y",
                    "completed-x", "move-speed", "move-counter", "screen-offset-y", "intro-wait",
                    "post-wait", "source-transition", "destination-transition", "explanation-text-id",
                    "explanation-text-base64"
                ],
                headerRequired: true)).SingleRow();
        Record = new BlackTowerEntranceEventDatabaseEventRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2), row.HexByte(3),
            row.HexByte(4), row.HexByte(5), row.HexByte(6), row.HexByte(7),
            row.HexByte(8), row.HexByte(9), row.HexByte(10), row.HexByte(11),
            row.HexByte(12), row.HexByte(13), row.HexByte(14), row.UnsignedDecimal(15),
            row.UnsignedDecimal(16), row.HexByte(17), row.HexByte(18), row.HexWord(19),
            row.Base64Utf8(20));

        First = CutsceneCommandCatalog.Load(
            Root + "black_tower_guard_first.tsv");
        Aftermath = CutsceneCommandCatalog.Load(
            Root + "black_tower_guard_aftermath.tsv");
        Oam = LoadOam(
            "black_tower_stage_0_oam.tsv", "stage-zero", 16);
        var stage1 = new List<OamRecord>();
        stage1.AddRange(LoadOam(
            "black_tower_stage_1_tower_oam.tsv", "stage-one tower", 16));
        stage1.AddRange(LoadOam(
            "black_tower_stage_1_workers_oam.tsv", "stage-one workers", 10));
        Stage1Oam = stage1;
        BackgroundPalettes = OracleGraphicsData.LoadPalette(
            Root + "black_tower_bg_palette.bin", 7, firstPalette: 1);
        SpritePalettes = OracleGraphicsData.LoadPalette(
            Root + "black_tower_sprite_palette.bin", transparentZero: true);
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
            Oam.Count != 16 || Stage1Oam.Count != 26)
        {
            throw new InvalidOperationException(
                "Room 1:86 guard data diverges from hardhatWorkerSubid02Script.");
        }
    }

    internal IReadOnlyList<OamRecord> OamForStage(int stage) => stage switch
    {
        0 => Oam,
        1 => Stage1Oam,
        _ => throw new InvalidOperationException(
            $"Black Tower explanation stage {stage} is not imported.")
    };

    private static IReadOnlyList<OamRecord> LoadOam(
        string file,
        string description,
        int expectedCount)
    {
        var result = new List<OamRecord>();
        GeneratedTable table = GeneratedTable.Load(
            Root + file,
            new GeneratedTableSchema(
                $"Black Tower {description} OAM",
                GeneratedTableKeySemantics.Unique,
                ["index", "y", "x", "tile", "flags", "source"],
                ["index"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (row.UnsignedDecimal(0) != result.Count)
                throw row.Invalid(0, $"sequential index {result.Count}");
            result.Add(new OamRecord(
                row.HexByte(1), row.HexByte(2), row.HexByte(3),
                row.HexByte(4), row.RequiredString(5)));
        }
        if (result.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Black Tower {description} OAM should contain " +
                $"{expectedCount} entries, got {result.Count}.");
        }
        return result;
    }

}

internal readonly record struct OamRecord(int Y, int X, int Tile, int Flags, string Source);

internal readonly record struct BlackTowerEntranceEventDatabaseEventRecord(int Group, int Room, int GuardId, int GuardSubId, int EssenceMask, int ItemFlag, int AftermathFlag, int CompleteFlag, int InitialY, int InitialX, int CompletedY, int CompletedX, int MoveSpeed, int MoveCounter, int ScreenOffsetY, int IntroWait, int PostWait, int SourceTransition, int DestinationTransition, int ExplanationTextId, string ExplanationText);
