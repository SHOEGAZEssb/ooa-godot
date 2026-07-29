using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Typed command streams for entity-backed interactionRunScript loops.
/// </summary>
internal sealed class NpcInteractionScriptDatabase
{
    public IReadOnlyList<CutsceneCommand> LinkedGameNpc { get; } =
        CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/linked_game_npc_commands.tsv");
    public IReadOnlyList<CutsceneCommand> PastBipin { get; } =
        CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/past_bipin_commands.tsv");
    public IReadOnlyList<CutsceneCommand> HardhatShovel { get; } =
        CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/hardhat_shovel_commands.tsv");
    public IReadOnlyList<CutsceneCommand> Postman { get; } =
        CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/postman_commands.tsv");

    public NpcInteractionScriptDatabase()
    {
        ValidateLinkedGameNpc();
        ValidatePastBipin();
        ValidateHardhatShovel();
        ValidatePostman();
    }

    private void ValidateLinkedGameNpc()
    {
        if (LinkedGameNpc.Count != 31 ||
            LinkedGameNpc[0] is not CutsceneInitCollisionsCommand
                { Actor: "LinkedNpc" } ||
            LinkedGameNpc[3] is not CutsceneCheckAButtonCommand
                { Actor: "LinkedNpc" } ||
            LinkedGameNpc[5] is not CutsceneShowLoadedTextCommand ||
            LinkedGameNpc[7] is not CutsceneTextOptionBranchCommand
                { Value: 0, TargetCommand: 12 } ||
            LinkedGameNpc[13] is not
                CutsceneMemoryBranchYieldOnMissCommand
                {
                    Binding: "LinkedNpcHasExtraText",
                    Value: 0,
                    TargetCommand: 18
                } ||
            LinkedGameNpc[18] is not CutsceneNativeCommand
                { Handler: "linkedNpc_generateSecret" } ||
            LinkedGameNpc[30] is not CutsceneBranchCommand
                { TargetCommand: 12 } ||
            LinkedGameNpc.Count(command =>
                command is CutsceneShowLoadedTextCommand) != 5)
        {
            throw new InvalidOperationException(
                "linkedGameNpcScript command stream diverges from its source loop.");
        }
    }

    private void ValidatePastBipin()
    {
        if (PastBipin.Count != 13 ||
            PastBipin[0] is not CutsceneInitCollisionsCommand
                { Actor: "PastBipin" } ||
            PastBipin[2] is not CutsceneCheckAButtonCommand
                { Actor: "PastBipin" } ||
            PastBipin[4] is not CutsceneRoomFlagBranchCommand
                {
                    Flag: OracleSaveData.RoomFlagItem,
                    TargetCommand: 11
                } ||
            PastBipin[6] is not CutsceneGiveItemCommand
                {
                    TreasureId: TreasureDatabase.TreasureGashaSeed,
                    Parameter: 0x08
                } ||
            PastBipin[7] is not CutsceneWaitCommand { Frames: 1 } ||
            PastBipin[8] is not CutsceneCheckTextCommand ||
            PastBipin[12] is not CutsceneBranchCommand { TargetCommand: 1 })
        {
            throw new InvalidOperationException(
                "bipinScript3 command stream diverges from its source loop.");
        }
    }

    private void ValidateHardhatShovel()
    {
        if (HardhatShovel.Count != 16 ||
            HardhatShovel[0] is not CutsceneInitCollisionsCommand
                { Actor: "Hardhat" } ||
            HardhatShovel[1] is not CutsceneCheckAButtonCommand
                { Actor: "Hardhat" } ||
            HardhatShovel[4] is not CutsceneMemoryJumpTableCommand
                { Binding: "HardhatVar03" } jumpTable ||
            !jumpTable.TargetCommands.SequenceEqual([5, 12]) ||
            HardhatShovel[5] is not CutsceneRoomFlagBranchCommand
                {
                    Flag: OracleSaveData.RoomFlagItem,
                    TargetCommand: 10
                } ||
            HardhatShovel[8] is not CutsceneGiveItemCommand
                {
                    TreasureId: TreasureDatabase.TreasureShovel,
                    Parameter: 0
                } ||
            HardhatShovel[13] is not CutsceneSetAnimationCommand
                { Actor: "Hardhat", Animation: 4 } ||
            HardhatShovel[15] is not CutsceneBranchCommand { TargetCommand: 1 })
        {
            throw new InvalidOperationException(
                "hardhatWorkerSubid00Script command stream diverges from its source loop.");
        }
    }

    private void ValidatePostman()
    {
        if (Postman.Count != 25 ||
            Postman[0] is not CutsceneRoomFlagBranchCommand
                {
                    Flag: OracleSaveData.RoomFlagItem,
                    TargetCommand: 24
                } ||
            Postman[1] is not CutsceneInitCollisionsCommand
                { Actor: "Postman" } ||
            Postman[2] is not CutsceneCheckAButtonCommand
                { Actor: "Postman" } ||
            Postman[6] is not CutsceneTradeItemBranchCommand
                { Value: 0, TargetCommand: 8 } ||
            Postman[10] is not CutsceneTextOptionBranchCommand
                { Value: 0, TargetCommand: 14 } ||
            Postman[16] is not CutsceneWriteObjectByteCommand
                { Actor: "Postman", Address: 0x3f, Value: 1 } ||
            Postman[17] is not CutsceneSetSpeedCommand
                { Actor: "Postman", Speed: PostmanCharacter.Speed200 } ||
            Postman[18] is not CutsceneMoveCommand
                {
                    Actor: "Postman",
                    Angle: PostmanCharacter.RightAngle,
                    Counter: 0x1d
                } ||
            Postman[19] is not CutsceneMoveCommand
                {
                    Actor: "Postman",
                    Angle: PostmanCharacter.DownAngle,
                    Counter: 0x39
                } ||
            Postman[21] is not CutsceneGiveItemCommand
                {
                    TreasureId: TreasureDatabase.TreasureTradeItem,
                    Parameter: 1
                } ||
            Postman[23] is not CutsceneEndCommand ||
            Postman[24] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "postmanScript command stream diverges from its source.");
        }
    }
}
