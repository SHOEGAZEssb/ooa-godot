using System;
using System.Collections.Generic;
using System.Globalization;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_BOY $3c:$07 metadata and boySubid07Script command stream
/// for past room $2:$f3.
/// </summary>
internal sealed class DepressedBoyEventDatabase
{
    public DepressedBoyEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public DepressedBoyEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/depressed_boy_event.tsv",
            new GeneratedTableSchema(
                "room 2:f3 depressed-boy trade event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "collision-y",
                    "collision-x", "room-flag", "required-trade",
                    "reward-treasure", "reward-parameter", "reward-object",
                    "darken-target", "approach-y", "dance-count",
                    "dance-animations", "dance-music", "reward-sound",
                    "source-animation-count", "initial-script-updates"
                ],
                headerRequired: true)).SingleRow();
        Record = new DepressedBoyEventRecord(
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
            row.RequiredString(10),
            row.Decimal(11, -31, 31),
            row.HexByte(12),
            row.UnsignedDecimal(13),
            ParseDanceAnimations(row, 14),
            row.HexByte(15),
            row.HexByte(16),
            row.UnsignedDecimal(17),
            row.UnsignedDecimal(18));
        ValidateRecord();

        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/depressed_boy_commands.tsv");
        ValidateCommands();
    }

    private void ValidateRecord()
    {
        if (Record is not
            {
                Group: 2,
                Room: 0xf3,
                InteractionId: 0x3c,
                SubId: 0x07,
                CollisionRadiusY: 6,
                CollisionRadiusX: 6,
                RoomFlag: OracleSaveData.RoomFlagItem,
                RequiredTradeItem: 0x07,
                RewardTreasure: TreasureDatabase.TreasureTradeItem,
                RewardParameter: 0x08,
                RewardObject: "TREASURE_OBJECT_TRADEITEM_08",
                DarkenTarget: -9,
                ApproachY: 0x48,
                DanceCount: 20,
                DanceMusic: OracleSoundEngine.MusCrazyDance,
                RewardSound: OracleSoundEngine.SndSwordObtained,
                SourceAnimationCount: 21,
                InitialScriptUpdates: 1
            } ||
            Record.DanceAnimations.Count != Record.SourceAnimationCount)
        {
            throw new InvalidOperationException(
                "Room 2:f3 depressed-boy metadata diverges from its source handlers.");
        }

        DepressedBoyDanceFrame[] expected =
        [
            new(0x08, 0x14), new(0x09, 0x14), new(0x08, 0x14),
            new(0x09, 0x14), new(0x07, 0x14), new(0x0e, 0x14),
            new(0x06, 0x14), new(0x1c, 0x14), new(0x08, 0x14),
            new(0x09, 0x14), new(0x08, 0x14), new(0x08, 0x28),
            new(0x09, 0x32), new(0x07, 0x14), new(0x0e, 0x14),
            new(0x06, 0x14), new(0x1c, 0x14), new(0x08, 0x14),
            new(0x09, 0x14), new(0x08, 0x14), new(0x09, 0x14)
        ];
        for (int index = 0; index < expected.Length; index++)
        {
            if (Record.DanceAnimations[index] != expected[index])
            {
                throw new InvalidOperationException(
                    $"Funny Joke dance frame {index} diverges from source metadata.");
            }
        }
    }

    private void ValidateCommands()
    {
        if (Commands.Count != 44 ||
            Commands[0] is not CutsceneInitCollisionsCommand
                { Actor: "DepressedBoy" } ||
            Commands[1] is not CutsceneMemoryGateCommand
                { Binding: "MenuDisabled", Value: 0 } ||
            Commands[2] is not CutsceneNativeCommand
                { Handler: "DarkenRoomLightly" } ||
            Commands[3] is not CutsceneGateCommand { Gate: "PaletteFade" } ||
            Commands[4] is not CutsceneCheckAButtonCommand
                { Actor: "DepressedBoy" } ||
            Commands[6] is not CutsceneRoomFlagBranchCommand
                { Flag: OracleSaveData.RoomFlagItem, TargetCommand: 41 } ||
            Commands[7] is not CutsceneTradeItemBranchCommand
                { Value: 0x07, TargetCommand: 11 } ||
            Commands[8] is not CutsceneShowTextCommand { TextId: 0x2517 } ||
            Commands[11] is not CutsceneShowTextCommand { TextId: 0x2515 } ||
            Commands[13] is not CutsceneTextOptionBranchCommand
                { Value: 0, TargetCommand: 15 } ||
            Commands[15] is not CutsceneWriteObjectByteCommand
                { Actor: "DepressedBoy", Address: 0x3d, Value: 1 } ||
            Commands[16] is not CutsceneNativeCommand
                { Handler: "MoveLinkToFunnyJokePosition" } ||
            Commands[18] is not CutsceneMemoryGateCommand
                { Binding: "LinkObjectId", Value: 0 } ||
            Commands[19] is not CutsceneWriteObjectByteCommand
                { Actor: "DepressedBoy", Address: 0x3d, Value: 0 } ||
            Commands[20] is not CutsceneNativeCommand
                { Handler: "SetLinkNormalDown" } ||
            Commands[22] is not CutsceneSetMusicCommand
                { Music: OracleSoundEngine.MusCrazyDance } ||
            Commands[24] is not CutsceneNativeCommand
                { Handler: "AdvanceFunnyJokeDance" } ||
            Commands[25] is not CutsceneMemoryBranchYieldOnMissCommand
                { Binding: "DanceComplete", Value: 1, TargetCommand: 27 } ||
            Commands[27] is not CutsceneNativeCommand
                { Handler: "RestartSound" } ||
            Commands[29] is not CutscenePlaySoundCommand
                { Sound: OracleSoundEngine.SndSwordObtained } ||
            Commands[30] is not CutsceneNativeCommand
                { Handler: "SetLinkGetItemTwoHand" } ||
            Commands[32] is not CutsceneNativeCommand
                { Handler: "SetLinkNormalUp" } ||
            Commands[34] is not CutsceneShowTextCommand { TextId: 0x2516 } ||
            Commands[36] is not CutsceneGiveItemCommand
                {
                    TreasureId: TreasureDatabase.TreasureTradeItem,
                    Parameter: 0x08
                } ||
            Commands[38] is not CutsceneNativeCommand { Handler: "ResetMusic" } ||
            Commands[39] is not CutsceneEnableInputCommand ||
            Commands[40] is not CutsceneBranchCommand { TargetCommand: 4 } ||
            Commands[41] is not CutsceneShowTextCommand { TextId: 0x2518 } ||
            Commands[42] is not CutsceneEnableInputCommand ||
            Commands[43] is not CutsceneBranchCommand { TargetCommand: 4 })
        {
            throw new InvalidOperationException(
                "boySubid07Script command stream diverges from imported metadata.");
        }
    }

    private static IReadOnlyList<DepressedBoyDanceFrame> ParseDanceAnimations(
        GeneratedTableRow row,
        int column)
    {
        string[] entries = row.RequiredString(column).Split(',');
        var frames = new List<DepressedBoyDanceFrame>(entries.Length);
        foreach (string entry in entries)
        {
            string[] fields = entry.Split(':');
            if (fields.Length != 2 ||
                !int.TryParse(
                    fields[0], NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture, out int mode) ||
                !int.TryParse(
                    fields[1], NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture, out int duration) ||
                mode is < 0 or > 0xff || duration is < 1 or > 0xff)
            {
                throw new InvalidOperationException(
                    $"{row.Path}:{row.LineNumber}: malformed Funny Joke " +
                    $"animation '{entry}'.");
            }
            frames.Add(new DepressedBoyDanceFrame(mode, duration));
        }
        return frames.AsReadOnly();
    }
}

internal readonly record struct DepressedBoyEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int CollisionRadiusY,
    int CollisionRadiusX,
    int RoomFlag,
    int RequiredTradeItem,
    int RewardTreasure,
    int RewardParameter,
    string RewardObject,
    int DarkenTarget,
    int ApproachY,
    int DanceCount,
    IReadOnlyList<DepressedBoyDanceFrame> DanceAnimations,
    int DanceMusic,
    int RewardSound,
    int SourceAnimationCount,
    int InitialScriptUpdates);

internal readonly record struct DepressedBoyDanceFrame(int Mode, int Duration);
