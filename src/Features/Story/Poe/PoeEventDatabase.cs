using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_POE metadata and poeScript command stream for present
/// overworld room $0:$7c and tomb room $2:$2e.
/// </summary>
internal sealed class PoeEventDatabase
{
    public PoeEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public PoeEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/poe_event.tsv",
            new GeneratedTableSchema(
                "rooms 0:7c and 2:2e Poe event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "first-var03",
                    "tomb-var03", "final-var03", "progress-flag", "item-flag",
                    "tomb-group", "tomb-room", "collision-y", "collision-x",
                    "disappear-wait", "flicker-count", "flicker-address",
                    "flicker-mask", "poof-sound", "reward-treasure",
                    "reward-parameter", "reward-object", "speed-100",
                    "initial-script-updates", "animation0", "animation1",
                    "animation2", "animation3"
                ],
                headerRequired: true)).SingleRow();
        Record = new PoeEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.HexByte(7),
            row.HexByte(8),
            row.Decimal(9, 0, 7),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.UnsignedDecimal(13),
            row.UnsignedDecimal(14),
            row.HexByte(15),
            row.HexByte(16),
            row.HexByte(17),
            row.HexByte(18),
            row.HexByte(19),
            row.RequiredString(20),
            row.HexByte(21),
            row.UnsignedDecimal(22),
            row.RequiredString(23),
            row.RequiredString(24),
            row.RequiredString(25),
            row.RequiredString(26));
        ValidateRecord();

        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/poe_commands.tsv");
        ValidateCommands();
    }

    private void ValidateRecord()
    {
        if (Record is not
            {
                Group: 0,
                Room: 0x7c,
                InteractionId: 0x59,
                SubId: 0,
                FirstVariant: 0,
                TombVariant: 1,
                FinalVariant: 2,
                ProgressFlag: OracleSaveData.RoomFlag40,
                ItemFlag: OracleSaveData.RoomFlagItem,
                TombGroup: 2,
                TombRoom: 0x2e,
                CollisionRadiusY: 6,
                CollisionRadiusX: 6,
                DisappearWait: 40,
                FlickerCount: 30,
                FlickerAddress: 0x3e,
                FlickerMask: 0x02,
                PoofSound: OracleSoundEngine.SndPoof,
                RewardTreasure: TreasureDatabase.TreasureTradeItem,
                RewardParameter: 0,
                RewardObject: "TREASURE_OBJECT_TRADEITEM_00",
                Speed100: 0x28,
                InitialScriptUpdates: 1
            })
        {
            throw new InvalidOperationException(
                "Room 0:7c/2:2e Poe metadata diverges from its source handlers.");
        }
    }

    private void ValidateCommands()
    {
        if (Commands.Count != 28 ||
            Commands[0] is not CutsceneInitCollisionsCommand { Actor: "Poe" } ||
            Commands[1] is not CutsceneCheckAButtonCommand { Actor: "Poe" } ||
            Commands[3] is not CutsceneMemoryJumpTableCommand
                { Binding: "PoeVariant" } jumpTable ||
            jumpTable.TargetCommands.Count != 3 ||
            jumpTable.TargetCommands[0] != 4 ||
            jumpTable.TargetCommands[1] != 12 ||
            jumpTable.TargetCommands[2] != 24 ||
            Commands[4] is not CutsceneShowTextCommand { TextId: 0x0b00 } ||
            Commands[5] is not CutsceneOrRoomFlagCommand
                { Flag: OracleSaveData.RoomFlag40 } ||
            Commands[6] is not CutsceneWaitCommand { Frames: 40 } ||
            Commands[7] is not CutscenePlaySoundCommand
                { Sound: OracleSoundEngine.SndPoof } ||
            Commands[8] is not CutsceneWriteObjectByteCommand
                { Actor: "Poe", Address: 0x3e, Value: 30 } ||
            Commands[9] is not CutsceneFlickerCommand
                { Actor: "Poe", CounterAddress: 0x3e, FrameMask: 0x02 } ||
            Commands[10] is not CutsceneEnableInputCommand ||
            Commands[11] is not CutsceneEndCommand ||
            Commands[12] is not CutsceneShowTextCommand { TextId: 0x0b01 } ||
            Commands[15] is not CutsceneWriteObjectByteCommand
                { Actor: "Poe", Address: 0x3f, Value: 1 } ||
            Commands[16] is not CutsceneSetSpeedCommand
                { Actor: "Poe", Speed: 0x28 } ||
            Commands[17] is not CutsceneSetAnimationCommand
                { Actor: "Poe", Animation: 2 } ||
            Commands[19] is not CutsceneApplySpeedCommand
                { Actor: "Poe", Counter: 0x49 } ||
            Commands[20] is not CutsceneSetAnimationCommand
                { Actor: "Poe", Animation: 1 } ||
            Commands[22] is not CutsceneApplySpeedCommand
                { Actor: "Poe", Counter: 0x39 } ||
            Commands[23] is not CutsceneBranchCommand { TargetCommand: 6 } ||
            Commands[24] is not CutsceneShowTextCommand { TextId: 0x0b02 } ||
            Commands[25] is not CutsceneWaitCommand { Frames: 30 } ||
            Commands[26] is not CutsceneGiveItemCommand
                { TreasureId: TreasureDatabase.TreasureTradeItem, Parameter: 0 } ||
            Commands[27] is not CutsceneBranchCommand { TargetCommand: 6 })
        {
            throw new InvalidOperationException(
                "poeScript command stream diverges from imported metadata.");
        }

        foreach (CutsceneCommand command in Commands)
        {
            if (command is CutsceneSetAnimationCommand animation &&
                animation.EncodedAnimation != Record.Animation(animation.Animation))
            {
                throw new InvalidOperationException(
                    $"Poe animation ${animation.Animation:x2} diverges at " +
                    $"{animation.Source}.");
            }
            if (command is CutsceneShowTextCommand text &&
                text.TextId is < 0x0b00 or > 0x0b02)
            {
                throw new InvalidOperationException(
                    $"Unexpected Poe text TX_{text.TextId:x4} at {text.Source}.");
            }
        }
    }
}

internal readonly record struct PoeEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int FirstVariant,
    int TombVariant,
    int FinalVariant,
    int ProgressFlag,
    int ItemFlag,
    int TombGroup,
    int TombRoom,
    int CollisionRadiusY,
    int CollisionRadiusX,
    int DisappearWait,
    int FlickerCount,
    int FlickerAddress,
    int FlickerMask,
    int PoofSound,
    int RewardTreasure,
    int RewardParameter,
    string RewardObject,
    int Speed100,
    int InitialScriptUpdates,
    string Animation0,
    string Animation1,
    string Animation2,
    string Animation3)
{
    public string Animation(int index) => index switch
    {
        0 => Animation0,
        1 => Animation1,
        2 => Animation2,
        3 => Animation3,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
