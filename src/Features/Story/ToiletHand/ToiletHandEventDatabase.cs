using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported room 2:3e Toilet Hand metadata and its two source script streams.
/// </summary>
internal sealed class ToiletHandEventDatabase
{
    public ToiletHandEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }
    public IReadOnlyList<CutsceneCommand> ReactionCommands { get; }

    public ToiletHandEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/toilet_hand_event.tsv",
            new GeneratedTableSchema(
                "room 2:3e Toilet Hand event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid",
                    "animation0", "animation1", "animation2",
                    "collision-y", "collision-x", "room-flag",
                    "required-trade", "reward-treasure", "reward-parameter",
                    "reward-object", "close-packed",
                    "initial-script-updates", "always-update"
                ],
                headerRequired: true)).SingleRow();
        Record = new ToiletHandEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.RequiredString(4),
            row.RequiredString(5),
            row.RequiredString(6),
            row.HexByte(7),
            row.HexByte(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.RequiredString(13),
            ParsePackedPositions(row.RequiredString(14)),
            row.UnsignedDecimal(15),
            row.Boolean01(16));
        ValidateRecord();

        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/toilet_hand_commands.tsv");
        ReactionCommands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/toilet_hand_reaction_commands.tsv");
        ValidateCommands();
        ValidateReactionCommands();
    }

    private void ValidateRecord()
    {
        if (Record is not
            {
                Group: 2,
                Room: 0x3e,
                InteractionId: 0x5b,
                SubId: 0,
                CollisionRadiusY: 0x06,
                CollisionRadiusX: 0x06,
                RoomFlag: OracleSaveData.RoomFlagItem,
                RequiredTradeItem: 0x01,
                RewardTreasure: TreasureDatabase.TreasureTradeItem,
                RewardParameter: 0x02,
                RewardObject: "TREASURE_OBJECT_TRADEITEM_02",
                InitialScriptUpdates: 1,
                AlwaysUpdate: true
            } ||
            !Record.ClosePacked.SequenceEqual([0x57, 0x68, 0x67]) ||
            string.IsNullOrWhiteSpace(Record.Animation0) ||
            string.IsNullOrWhiteSpace(Record.Animation1) ||
            string.IsNullOrWhiteSpace(Record.Animation2))
        {
            throw new InvalidOperationException(
                "Room 2:3e Toilet Hand metadata diverges from its source handlers.");
        }
    }

    private void ValidateCommands()
    {
        if (Commands.Count != 61 ||
            Commands[0] is not CutsceneNativeCommand
                { Handler: "toiletHand_setInvisible" } ||
            Commands[1] is not CutsceneInitCollisionsCommand
                { Actor: "ToiletHand" } ||
            Commands[5] is not CutsceneMemoryBranchYieldOnMissCommand
                {
                    Binding: "ToiletHandClose",
                    Value: 0,
                    TargetCommand: 3
                } ||
            Commands[7] is not CutsceneMemoryBranchCommand
                {
                    Binding: "ToiletHandPressedA",
                    Value: 1,
                    TargetCommand: 17
                } ||
            Commands[9] is not CutsceneMemoryBranchYieldOnMissCommand
                {
                    Binding: "ToiletHandClose",
                    Value: 0,
                    TargetCommand: 11
                } ||
            Commands[14] is not CutsceneMemoryBranchYieldOnMissCommand
                {
                    Binding: "ToiletHandClose",
                    Value: 0,
                    TargetCommand: 16
                } ||
            Commands[19] is not CutsceneRoomFlagBranchCommand
                {
                    Flag: OracleSaveData.RoomFlagItem,
                    TargetCommand: 45
                } ||
            Commands[21] is not CutsceneTradeItemBranchCommand
                { Value: 0x01, TargetCommand: 25 } ||
            Commands[28] is not CutsceneTextOptionBranchCommand
                { Value: 0, TargetCommand: 33 } ||
            Commands[41] is not CutsceneGiveItemCommand
                {
                    TreasureId: TreasureDatabase.TreasureTradeItem,
                    Parameter: 0x02
                } ||
            Commands[53] is not CutsceneMemoryGateCommand
                { Binding: "ToiletHandAnimParameter", Value: 0xff } ||
            Commands[58] is not CutsceneMemoryGateCommand
                { Binding: "ToiletHandAnimParameter", Value: 0xff })
        {
            throw new InvalidOperationException(
                "toiletHandScript command stream diverges from imported metadata.");
        }

        int[] expectedTextIds =
        [
            0x0b07, 0x0b08, 0x0b0a, 0x0b09,
            0x0b0b, 0x0b0c, 0x0b09
        ];
        int[] actualTextIds = Commands
            .OfType<CutsceneShowTextCommand>()
            .Select(command => command.TextId)
            .ToArray();
        if (!actualTextIds.SequenceEqual(expectedTextIds))
        {
            throw new InvalidOperationException(
                "toiletHandScript text order diverges from TX_0b07-$0b0c.");
        }
    }

    private void ValidateReactionCommands()
    {
        if (ReactionCommands.Count != 30 ||
            ReactionCommands[1] is not
                CutsceneMemoryBranchYieldOnMissCommand
                {
                    Binding: "ToiletHandPriority",
                    Value: 1,
                    TargetCommand: 4
                } ||
            ReactionCommands[7] is not CutsceneMemoryJumpTableCommand jumpTable ||
            !jumpTable.TargetCommands.SequenceEqual(
                [10, 8, 15, 17, 19, 21, 23, 25]) ||
            ReactionCommands[10] is not CutsceneNativeCommand
                { Handler: "toiletHand_setScreenShake60" } ||
            ReactionCommands[11] is not CutsceneNativeCommand
                { Handler: "toiletHand_playExplosion" } ||
            ReactionCommands[29] is not CutsceneReturnCommand)
        {
            throw new InvalidOperationException(
                "toiletHandScript_reactToObjectInHole diverges from its source.");
        }
    }

    private static int[] ParsePackedPositions(string encoded)
    {
        string[] values = encoded.Split(',');
        var result = new int[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            if (!int.TryParse(
                values[index],
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out result[index]) ||
                result[index] is < 0 or > 0xff)
            {
                throw new InvalidOperationException(
                    $"Invalid Toilet Hand packed position '{values[index]}'.");
            }
        }
        return result;
    }
}

internal readonly record struct ToiletHandEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    string Animation0,
    string Animation1,
    string Animation2,
    int CollisionRadiusY,
    int CollisionRadiusX,
    int RoomFlag,
    int RequiredTradeItem,
    int RewardTreasure,
    int RewardParameter,
    string RewardObject,
    IReadOnlyList<int> ClosePacked,
    int InitialScriptUpdates,
    bool AlwaysUpdate)
{
    public string Animation(int index) => index switch
    {
        0 => Animation0,
        1 => Animation1,
        2 => Animation2,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
