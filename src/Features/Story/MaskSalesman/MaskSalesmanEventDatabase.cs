using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_MASK_SALESMAN metadata and maskSalesmanScript command
/// stream for past interior room $2:$e6.
/// </summary>
internal sealed class MaskSalesmanEventDatabase
{
    public MaskSalesmanEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public MaskSalesmanEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/mask_salesman_event.tsv",
            new GeneratedTableSchema(
                "room 2:e6 Mask Salesman event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "animation0", "animation1",
                    "initial-animation", "collision-y", "collision-x", "room-flag",
                    "required-trade", "reward-treasure", "reward-parameter",
                    "reward-object", "initial-script-updates", "always-update"
                ],
                headerRequired: true)).SingleRow();
        Record = new MaskSalesmanEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.RequiredString(4),
            row.RequiredString(5),
            row.HexByte(6),
            row.HexByte(7),
            row.HexByte(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.RequiredString(13),
            row.UnsignedDecimal(14),
            row.Boolean01(15));
        ValidateRecord();

        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/mask_salesman_commands.tsv");
        ValidateCommands();
    }

    private void ValidateRecord()
    {
        if (Record is not
            {
                Group: 2,
                Room: 0xe6,
                InteractionId: 0x5c,
                SubId: 0,
                InitialAnimation: 0,
                CollisionRadiusY: 4,
                CollisionRadiusX: 6,
                RoomFlag: OracleSaveData.RoomFlagItem,
                RequiredTradeItem: 0x03,
                RewardTreasure: TreasureDatabase.TreasureTradeItem,
                RewardParameter: 0x04,
                RewardObject: "TREASURE_OBJECT_TRADEITEM_04",
                InitialScriptUpdates: 1,
                AlwaysUpdate: true
            } ||
            string.IsNullOrWhiteSpace(Record.Animation0) ||
            string.IsNullOrWhiteSpace(Record.Animation1))
        {
            throw new InvalidOperationException(
                "Room 2:e6 Mask Salesman metadata diverges from its source handlers.");
        }
    }

    private void ValidateCommands()
    {
        if (Commands.Count != 44 ||
            Commands[0] is not CutsceneSetCollisionRadiiCommand
                {
                    Actor: "MaskSalesman",
                    RadiusY: 4,
                    RadiusX: 6
                } ||
            Commands[1] is not CutsceneMakeAButtonSensitiveCommand
                { Actor: "MaskSalesman" } ||
            Commands[2] is not CutsceneCheckAButtonCommand
                { Actor: "MaskSalesman" } ||
            Commands[3] is not CutsceneDisableInputCommand ||
            Commands[4] is not CutsceneRoomFlagBranchCommand
                { Flag: OracleSaveData.RoomFlagItem, TargetCommand: 41 } ||
            Commands[17] is not CutsceneTradeItemBranchCommand
                { Value: 0x03, TargetCommand: 19 } ||
            Commands[19] is not CutsceneShowTextCommand { TextId: 0x0b10 } ||
            Commands[21] is not CutsceneTextOptionBranchCommand
                { Value: 0, TargetCommand: 24 } ||
            Commands[38] is not CutsceneGiveItemCommand
                {
                    TreasureId: TreasureDatabase.TreasureTradeItem,
                    Parameter: 0x04
                } ||
            Commands[41] is not CutsceneShowTextCommand { TextId: 0x0b15 } ||
            Commands[42] is not CutsceneEnableInputCommand ||
            Commands[43] is not CutsceneBranchCommand { TargetCommand: 2 })
        {
            throw new InvalidOperationException(
                "maskSalesmanScript command stream diverges from imported metadata.");
        }

        int[] expectedTextIds =
        [
            0x0b0d, 0x0b0e, 0x0b0f, 0x0b0e, 0x0b10,
            0x0b14, 0x0b45, 0x0b11, 0x0b12, 0x0b13, 0x0b45, 0x0b15
        ];
        int[] actualTextIds = Commands
            .OfType<CutsceneShowTextCommand>()
            .Select(command => command.TextId)
            .ToArray();
        if (!actualTextIds.SequenceEqual(expectedTextIds))
        {
            throw new InvalidOperationException(
                "maskSalesmanScript text order diverges from TX_0b0d-$0b15/$0b45.");
        }

        foreach (CutsceneCommand command in Commands)
        {
            if (command is CutsceneSetAnimationCommand animation &&
                animation.EncodedAnimation != Record.Animation(animation.Animation))
            {
                throw new InvalidOperationException(
                    $"Mask Salesman animation ${animation.Animation:x2} diverges at " +
                    $"{animation.Source}.");
            }
        }
    }
}

internal readonly record struct MaskSalesmanEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    string Animation0,
    string Animation1,
    int InitialAnimation,
    int CollisionRadiusY,
    int CollisionRadiusX,
    int RoomFlag,
    int RequiredTradeItem,
    int RewardTreasure,
    int RewardParameter,
    string RewardObject,
    int InitialScriptUpdates,
    bool AlwaysUpdate)
{
    public string Animation(int index) => index switch
    {
        0 => Animation0,
        1 => Animation1,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
