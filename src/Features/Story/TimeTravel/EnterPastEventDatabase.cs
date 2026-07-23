using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported parameters for INTERAC_MALE_VILLAGER ($3a) subid $0d, the
/// one-shot cutscene that runs when Link first enters past room $39.
/// </summary>
internal sealed class EnterPastEventDatabase
{
    public EnterPastEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public EnterPastEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/enter_past_event.tsv",
            new GeneratedTableSchema(
                "enter-past event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "intro-wait", "pre-jump-wait",
                    "post-jump-wait", "post-text-wait", "jump-speed-z", "jump-gravity",
                    "fast-speed", "slow-speed", "first-down-counter", "right-counter",
                    "second-down-counter", "slow-down-counter", "final-down-counter",
                    "global-flag", "text-id", "right-animation", "down-animation",
                    "text-base64", "jump-sound", "expected-arrival-counter"
                ],
                headerRequired: true)).SingleRow();

        Record = new EnterPastEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.UnsignedDecimal(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.UnsignedDecimal(7),
            row.Decimal(8),
            row.Decimal(9),
            row.HexByte(10),
            row.HexByte(11),
            row.UnsignedDecimal(12),
            row.UnsignedDecimal(13),
            row.UnsignedDecimal(14),
            row.UnsignedDecimal(15),
            row.UnsignedDecimal(16),
            row.HexByte(17),
            row.HexWord(18),
            row.RequiredString(19),
            row.RequiredString(20),
            row.Base64Utf8(21),
            row.HexByte(22),
            row.UnsignedDecimal(23));

        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/enter_past_commands.tsv");
        ValidateCommandStream();
    }

    private void ValidateCommandStream()
    {
        if (Commands.Count != 19 ||
            Commands[0] is not CutsceneSetDisabledObjectsCommand { Value: 0x11 } ||
            Commands[1] is not CutsceneWaitCommand { Frames: var intro } ||
                intro != Record.IntroWaitFrames ||
            Commands[2] is not CutsceneDisableInputCommand ||
            Commands[3] is not CutsceneWaitCommand { Frames: var preJump } ||
                preJump != Record.PreJumpWaitFrames ||
            Commands[4] is not CutsceneJumpCommand
            {
                Actor: "Villager",
                InitialSpeedZ: var speedZ,
                Gravity: var gravity,
                Sound: var jumpSound
            } || speedZ != Record.JumpSpeedZ || gravity != Record.JumpGravity ||
                jumpSound != Record.JumpSound ||
            Commands[5] is not CutsceneWaitCommand { Frames: var postJump } ||
                postJump != Record.PostJumpWaitFrames ||
            Commands[6] is not CutsceneShowTextCommand text ||
                text.TextId != Record.TextId || text.Message != Record.Text ||
            Commands[7] is not CutsceneWaitCommand { Frames: var postText } ||
                postText != Record.PostTextWaitFrames ||
            Commands[8] is not CutsceneSetSpeedCommand
                { Actor: "Villager", Speed: var firstSpeed } ||
                firstSpeed != Record.FastSpeed ||
            !MatchesMove(Commands[9], 0x10, Record.FirstDownCounter, Record.DownAnimation) ||
            !MatchesMove(Commands[10], 0x08, Record.RightCounter, Record.RightAnimation) ||
            !MatchesMove(Commands[11], 0x10, Record.SecondDownCounter, Record.DownAnimation) ||
            Commands[12] is not CutsceneSetSpeedCommand
                { Actor: "Villager", Speed: var slowSpeed } ||
                slowSpeed != Record.SlowSpeed ||
            Commands[13] is not CutsceneApplySpeedCommand
                { Actor: "Villager", Counter: var slowCounter } ||
                slowCounter != Record.SlowDownCounter ||
            Commands[14] is not CutsceneSetSpeedCommand
                { Actor: "Villager", Speed: var finalSpeed } ||
                finalSpeed != Record.FastSpeed ||
            Commands[15] is not CutsceneApplySpeedCommand
                { Actor: "Villager", Counter: var finalCounter } ||
                finalCounter != Record.FinalDownCounter ||
            Commands[16] is not CutsceneSetGlobalFlagCommand { Flag: var flag } ||
                flag != Record.GlobalFlag ||
            Commands[17] is not CutsceneEnableInputCommand ||
            Commands[18] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "villagerSubid0dScript command stream diverges from its imported metadata record.");
        }
    }

    private static bool MatchesMove(
        CutsceneCommand command,
        int angle,
        int counter,
        string animation) =>
        command is CutsceneMoveCommand
        {
            Actor: "Villager",
            Angle: var actualAngle,
            Counter: var actualCounter,
            EncodedAnimation: var actualAnimation
        } && actualAngle == angle && actualCounter == counter &&
            actualAnimation == animation;
}
