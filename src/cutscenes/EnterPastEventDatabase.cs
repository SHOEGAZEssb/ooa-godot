using Godot;
using System;
using System.Collections.Generic;
using System.Text;

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
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/enter_past_event.tsv");
        string? row = null;
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
            {
                row = line;
                break;
            }
        }
        if (row is null)
            throw new InvalidOperationException("Enter-past event data is empty.");

        string[] columns = row.Split('\t');
        if (columns.Length != 24)
        {
            throw new InvalidOperationException(
                $"Enter-past event row should contain 24 columns, got {columns.Length}.");
        }

        Record = new EnterPastEventRecord(
            int.Parse(columns[0]),
            Convert.ToInt32(columns[1], 16),
            Convert.ToInt32(columns[2], 16),
            Convert.ToInt32(columns[3], 16),
            int.Parse(columns[4]),
            int.Parse(columns[5]),
            int.Parse(columns[6]),
            int.Parse(columns[7]),
            int.Parse(columns[8]),
            int.Parse(columns[9]),
            Convert.ToInt32(columns[10], 16),
            Convert.ToInt32(columns[11], 16),
            int.Parse(columns[12]),
            int.Parse(columns[13]),
            int.Parse(columns[14]),
            int.Parse(columns[15]),
            int.Parse(columns[16]),
            Convert.ToInt32(columns[17], 16),
            Convert.ToInt32(columns[18], 16),
            columns[19],
            columns[20],
            Encoding.UTF8.GetString(Convert.FromBase64String(columns[21])),
            Convert.ToInt32(columns[22], 16),
            int.Parse(columns[23]));

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

    internal readonly record struct EnterPastEventRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        int IntroWaitFrames,
        int PreJumpWaitFrames,
        int PostJumpWaitFrames,
        int PostTextWaitFrames,
        int JumpSpeedZ,
        int JumpGravity,
        int FastSpeed,
        int SlowSpeed,
        int FirstDownCounter,
        int RightCounter,
        int SecondDownCounter,
        int SlowDownCounter,
        int FinalDownCounter,
        int GlobalFlag,
        int TextId,
        string RightAnimation,
        string DownAnimation,
        string Text,
        int JumpSound,
        int ExpectedArrivalCounter);
}
