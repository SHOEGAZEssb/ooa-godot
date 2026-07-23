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
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/maku_tree_saved_event.tsv",
            new GeneratedTableSchema(
                "saved Maku Tree event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "animation0", "animation1",
                    "animation2", "animation3", "animation4", "extra-sprite", "textbox-position",
                    "music", "advice-flag", "map-text-low", "falling-object", "respawn-object",
                    "drop-y", "respawn-y", "default-x", "lower-bound", "middle-bound",
                    "upper-bound", "lower-band-x", "upper-band-x", "initial-z", "drop-delay",
                    "bounce-count", "gravity", "bounce-speed", "spawn-sound", "landing-sound"
                ],
                headerRequired: true)).SingleRow();
        Record = new SavedEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.RequiredString(4), row.RequiredString(5), row.RequiredString(6),
            row.RequiredString(7), row.RequiredString(8), row.RequiredString(9),
            row.UnsignedDecimal(10),
            row.HexByte(11),
            row.HexByte(12),
            row.HexByte(13),
            row.RequiredString(14), row.RequiredString(15),
            row.HexByte(16),
            row.HexByte(17),
            row.HexByte(18),
            row.HexByte(19),
            row.HexByte(20),
            row.HexByte(21),
            row.HexByte(22),
            row.HexByte(23),
            row.Decimal(24),
            row.UnsignedDecimal(25),
            row.UnsignedDecimal(26),
            row.HexByte(27),
            row.Decimal(28),
            row.HexByte(29),
            row.HexByte(30));

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
}
