using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_COMEDIAN metadata and comedianScript command stream for
/// present overworld room $0:$56.
/// </summary>
internal sealed class ComedianEventDatabase
{
    public ComedianEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public ComedianEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/comedian_event.tsv",
            new GeneratedTableSchema(
                "room 0:56 comedian event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "animation0", "animation1",
                    "animation4", "animation5", "collision-y", "collision-x",
                    "room-flag", "progress-binding", "required-trade",
                    "reward-treasure", "reward-parameter", "reward-object",
                    "initial-script-updates"
                ],
                headerRequired: true)).SingleRow();
        Record = new ComedianEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.RequiredString(4),
            row.RequiredString(5),
            row.RequiredString(6),
            row.RequiredString(7),
            row.HexByte(8),
            row.HexByte(9),
            row.HexByte(10),
            row.RequiredString(11),
            row.HexByte(12),
            row.HexByte(13),
            row.HexByte(14),
            row.RequiredString(15),
            row.UnsignedDecimal(16));
        ValidateRecord();

        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/comedian_commands.tsv");
        ValidateCommands();
    }

    private void ValidateRecord()
    {
        if (Record is not
            {
                Group: 0,
                Room: 0x56,
                InteractionId: 0x65,
                SubId: 0,
                CollisionRadiusY: 6,
                CollisionRadiusX: 6,
                RoomFlag: OracleSaveData.RoomFlagItem,
                ProgressBinding: "ComedianProgress",
                RequiredTradeItem: 0x06,
                RewardTreasure: TreasureDatabase.TreasureTradeItem,
                RewardParameter: 0x07,
                RewardObject: "TREASURE_OBJECT_TRADEITEM_07",
                InitialScriptUpdates: 2
            })
        {
            throw new InvalidOperationException(
                "Room 0:56 comedian metadata diverges from its source handlers.");
        }
    }

    private void ValidateCommands()
    {
        if (Commands.Count != 34 ||
            Commands[0] is not CutsceneNativeCommand
                { Handler: "comedian_checkGameProgress" } ||
            Commands[1] is not CutsceneRoomFlagBranchCommand
                { Flag: OracleSaveData.RoomFlagItem, TargetCommand: 5 } ||
            Commands[3] is not CutsceneSetAnimationCommand
                { Actor: "Comedian", Animation: 1 } ||
            Commands[7] is not CutsceneInitCollisionsCommand
                { Actor: "Comedian" } ||
            Commands[8] is not CutsceneCheckAButtonCommand
                { Actor: "Comedian" } ||
            Commands[11] is not CutsceneMemoryJumpTableCommand
                { Binding: "ComedianProgress" } jumpTable ||
            jumpTable.TargetCommands.Count != 3 ||
            jumpTable.TargetCommands[0] != 12 ||
            jumpTable.TargetCommands[1] != 14 ||
            jumpTable.TargetCommands[2] != 16 ||
            Commands[18] is not CutsceneTradeItemBranchCommand
                { Value: 0x06, TargetCommand: 20 } ||
            Commands[22] is not CutsceneTextOptionBranchCommand
                { Value: 0, TargetCommand: 25 } ||
            Commands[28] is not CutsceneGiveItemCommand
                { TreasureId: TreasureDatabase.TreasureTradeItem, Parameter: 0x07 } ||
            Commands[33] is not CutsceneBranchCommand { TargetCommand: 8 })
        {
            throw new InvalidOperationException(
                "comedianScript command stream diverges from imported metadata.");
        }

        foreach (CutsceneCommand command in Commands)
        {
            if (command is CutsceneSetAnimationCommand animation &&
                animation.EncodedAnimation != Record.Animation(animation.Animation))
            {
                throw new InvalidOperationException(
                    $"Comedian animation ${animation.Animation:x2} diverges at " +
                    $"{animation.Source}.");
            }
            if (command is CutsceneShowTextCommand text &&
                text.TextId is < 0x0b2c or > 0x0b32)
            {
                throw new InvalidOperationException(
                    $"Unexpected comedian text TX_{text.TextId:x4} at {text.Source}.");
            }
        }
    }
}

internal readonly record struct ComedianEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    string Animation0,
    string Animation1,
    string Animation4,
    string Animation5,
    int CollisionRadiusY,
    int CollisionRadiusX,
    int RoomFlag,
    string ProgressBinding,
    int RequiredTradeItem,
    int RewardTreasure,
    int RewardParameter,
    string RewardObject,
    int InitialScriptUpdates)
{
    public string Animation(int index) => index switch
    {
        0 => Animation0,
        1 => Animation1,
        4 => Animation4,
        5 => Animation5,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
