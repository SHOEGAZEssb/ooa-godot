using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported adult Maku Tree script and Seed Satchel drop metadata selected by
/// wMakuTreeState=$02 in present room $0:$38.
/// </summary>
internal sealed class MakuTreeSavedDatabase
{
    public SavedEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public MakuTreeSavedDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/maku_tree_saved_event.tsv");
        string? row = null;
        foreach (string rawLine in source.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
            {
                row = line;
                break;
            }
        }
        if (row is null)
            throw new InvalidOperationException("Saved Maku Tree event data is empty.");

        string[] fields = row.Split('\t');
        if (fields.Length != 31)
        {
            throw new InvalidOperationException(
                $"Saved Maku Tree event row should contain 31 columns, got {fields.Length}.");
        }
        Record = new SavedEventRecord(
            int.Parse(fields[0]),
            Convert.ToInt32(fields[1], 16),
            Convert.ToInt32(fields[2], 16),
            Convert.ToInt32(fields[3], 16),
            fields[4], fields[5], fields[6], fields[7], fields[8], fields[9],
            int.Parse(fields[10]),
            Convert.ToInt32(fields[11], 16),
            Convert.ToInt32(fields[12], 16),
            Convert.ToInt32(fields[13], 16),
            fields[14], fields[15],
            Convert.ToInt32(fields[16], 16),
            Convert.ToInt32(fields[17], 16),
            Convert.ToInt32(fields[18], 16),
            Convert.ToInt32(fields[19], 16),
            Convert.ToInt32(fields[20], 16),
            Convert.ToInt32(fields[21], 16),
            Convert.ToInt32(fields[22], 16),
            Convert.ToInt32(fields[23], 16),
            int.Parse(fields[24]),
            int.Parse(fields[25]),
            int.Parse(fields[26]),
            Convert.ToInt32(fields[27], 16),
            int.Parse(fields[28]),
            Convert.ToInt32(fields[29], 16),
            Convert.ToInt32(fields[30], 16));

        ValidateRecord();
        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/maku_tree_saved_commands.tsv");
        ValidateCommands();
    }

    private void ValidateRecord()
    {
        if (Record is not
            {
                Group: 0,
                Room: 0x38,
                InteractionId: 0x87,
                SubId: 0x00,
                TextboxPosition: 2,
                Music: OracleSoundEngine.MusMakuTree,
                AdviceFlag: OracleSaveData.GlobalFlagMakuGivesAdviceFromPresentMap,
                MapTextLow: 0x4f,
                FallingTreasureObject: "TREASURE_OBJECT_SEED_SATCHEL_02",
                RespawnTreasureObject: "TREASURE_OBJECT_SEED_SATCHEL_03",
                DropY: 0x60,
                RespawnY: 0x58,
                DefaultX: 0x50,
                LowerBound: 0x3c,
                MiddleBound: 0x50,
                UpperBound: 0x64,
                LowerBandX: 0x60,
                UpperBandX: 0x40,
                InitialZPixels: -104,
                DropDelayFrames: 40,
                BounceCount: 2,
                Gravity: 0x10,
                BounceSpeed: -0xaa,
                SpawnSound: OracleSoundEngine.SndSolvePuzzle,
                LandingSound: OracleSoundEngine.SndDropEssence
            } || string.IsNullOrWhiteSpace(Record.ExtraSprite))
        {
            throw new InvalidOperationException(
                "Saved Maku Tree event metadata diverges from its source handlers.");
        }
    }

    private void ValidateCommands()
    {
        if (Commands.Count != 68 ||
            Commands[0] is not CutsceneNativeCommand
                { Handler: "makuTree_checkSpawnSeedSatchel" } ||
            Commands[1] is not CutsceneSetMusicCommand
                { Music: OracleSoundEngine.MusMakuTree } ||
            Commands[2] is not CutsceneSetAnimationContinueCommand
                { Actor: "MakuTree", Animation: 0 } ||
            Commands[3] is not CutsceneSetCollisionRadiiCommand
                { Actor: "MakuTree", RadiusY: 8, RadiusX: 8 } ||
            Commands[4] is not CutsceneMakeAButtonSensitiveCommand
                { Actor: "MakuTree" } ||
            Commands[5] is not CutsceneRoomFlagBranchCommand
                { Flag: OracleSaveData.RoomFlag80, TargetCommand: 60 } ||
            Commands[6] is not CutsceneCheckAButtonCommand
                { Actor: "MakuTree" } ||
            Commands[36] is not CutsceneTextOptionBranchCommand
                { Value: 0, TargetCommand: 26 } ||
            Commands[51] is not CutsceneSetGlobalFlagCommand
                { Flag: OracleSaveData.GlobalFlagMakuGivesAdviceFromPresentMap } ||
            Commands[52] is not CutsceneWriteMemoryCommand
                { Binding: "wMakuMapTextPresent", Value: 0x4f } ||
            Commands[55] is not CutsceneNativeCommand
                { Handler: "makuTree_dropSeedSatchel" } ||
            Commands[56] is not CutsceneWaitCommand { Frames: 140 } ||
            Commands[59] is not CutsceneEnableInputCommand ||
            Commands[60] is not CutsceneCheckAButtonCommand
                { Actor: "MakuTree" } ||
            Commands[67] is not CutsceneBranchCommand { TargetCommand: 60 })
        {
            throw new InvalidOperationException(
                "makuTree_subid02Script_body command stream diverges from imported metadata.");
        }

        for (int index = 0; index < Commands.Count; index++)
        {
            if (Commands[index] is CutsceneSetAnimationContinueCommand animation &&
                animation.EncodedAnimation != Record.Animation(animation.Animation))
            {
                throw new InvalidOperationException(
                    $"Saved Maku Tree animation ${animation.Animation:x2} diverges at " +
                    $"{animation.Source}.");
            }
            if (Commands[index] is CutsceneShowTextCommand text &&
                (text.TextId is < 0x0542 or > 0x0550) && text.TextId != 0x0561)
            {
                throw new InvalidOperationException(
                    $"Unexpected saved Maku Tree text TX_{text.TextId:x4} at {text.Source}.");
            }
        }
    }

    internal readonly record struct SavedEventRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        string Animation0,
        string Animation1,
        string Animation2,
        string Animation3,
        string Animation4,
        string ExtraSprite,
        int TextboxPosition,
        int Music,
        int AdviceFlag,
        int MapTextLow,
        string FallingTreasureObject,
        string RespawnTreasureObject,
        int DropY,
        int RespawnY,
        int DefaultX,
        int LowerBound,
        int MiddleBound,
        int UpperBound,
        int LowerBandX,
        int UpperBandX,
        int InitialZPixels,
        int DropDelayFrames,
        int BounceCount,
        int Gravity,
        int BounceSpeed,
        int SpawnSound,
        int LandingSound)
    {
        public string Animation(int index) => index switch
        {
            0 => Animation0,
            1 => Animation1,
            2 => Animation2,
            3 => Animation3,
            4 => Animation4,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }
}
