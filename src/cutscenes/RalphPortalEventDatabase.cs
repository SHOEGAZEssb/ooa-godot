using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

/// <summary>
/// Imported parameters for Ralph's first trip through a time portal. The
/// source interaction is INTERAC_RALPH ($37) subid $0d in present room $39.
/// </summary>
internal sealed class RalphPortalEventDatabase
{
    public RalphPortalEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public RalphPortalEventDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/ralph_portal_event.tsv");
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
            throw new InvalidOperationException("Ralph portal event data is empty.");

        string[] columns = row.Split('\t');
        if (columns.Length != 16)
            throw new InvalidOperationException(
                $"Ralph portal event row should contain 16 columns, got {columns.Length}.");

        Record = new RalphPortalEventRecord(
            int.Parse(columns[0]),
            Convert.ToInt32(columns[1], 16),
            Convert.ToInt32(columns[2], 16),
            Convert.ToInt32(columns[3], 16),
            Convert.ToInt32(columns[4], 16),
            int.Parse(columns[5]),
            int.Parse(columns[6]),
            int.Parse(columns[7]),
            int.Parse(columns[8]),
            Convert.ToInt32(columns[9], 16),
            Convert.ToInt32(columns[10], 16),
            Convert.ToInt32(columns[11], 16),
            Convert.ToInt32(columns[12], 16),
            columns[13],
            columns[14],
            Encoding.UTF8.GetString(Convert.FromBase64String(columns[15])));

        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/ralph_portal_commands.tsv");
        ValidateCommandStream();
    }

    private void ValidateCommandStream()
    {
        if (Commands.Count != 16 || Commands[0] is not CutsceneDisableInputCommand ||
            Commands[^1] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "ralphSubid0dScript should import as 16 commands from disableinput through scriptend.");
        }
        if (Commands[1] is not CutsceneWaitCommand { Frames: var intro } ||
            intro != Record.IntroDelayFrames ||
            Commands[2] is not CutsceneShowTextCommand text ||
            text.TextId != Record.TextId || text.Message != Record.Text ||
            Commands[3] is not CutsceneWaitCommand { Frames: var post } ||
            post != Record.PostTextFrames ||
            Commands[4] is not CutsceneSetAnimationCommand
            {
                Actor: "Ralph",
                Animation: 0x01,
                EncodedAnimation: var movementAnimation
            } || movementAnimation != Record.MovementAnimation ||
            Commands[5] is not CutsceneSetSpeedCommand
                { Actor: "Ralph", Speed: var speed } || speed != Record.Speed ||
            Commands[6] is not CutsceneSetAngleCommand
                { Actor: "Ralph", Angle: var angle } || angle != Record.Angle ||
            Commands[7] is not CutsceneApplySpeedCommand
                { Actor: "Ralph", Counter: var movementCounter } ||
                movementCounter != Record.MovementCounter ||
            Commands[8] is not CutsceneSetAnimationCommand
            {
                Actor: "Ralph",
                Animation: 0x09,
                EncodedAnimation: var portalAnimation
            } || portalAnimation != Record.PortalAnimation ||
            Commands[9] is not CutsceneWriteObjectByteCommand
                { Actor: "Ralph", Address: 0x3f, Value: var flickerFrames } ||
                flickerFrames != Record.FlickerFrames ||
            Commands[10] is not CutscenePlaySoundCommand
                { Sound: OracleSoundEngine.SndMysterySeed } ||
            Commands[11] is not CutsceneFlickerCommand
                { Actor: "Ralph", CounterAddress: 0x3f, FrameMask: 0x01 } ||
            Commands[12] is not CutsceneSetGlobalFlagCommand
                { Flag: var flag } || flag != Record.GlobalFlag ||
            Commands[13] is not CutsceneNativeCommand
                { Handler: "ralph_restoreMusic" } ||
            Commands[14] is not CutsceneEnableInputCommand)
        {
            throw new InvalidOperationException(
                "Ralph portal command stream diverges from its imported metadata record.");
        }
    }

    internal readonly record struct RalphPortalEventRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        int EntryDirection,
        int IntroDelayFrames,
        int PostTextFrames,
        int MovementCounter,
        int FlickerFrames,
        int Speed,
        int Angle,
        int GlobalFlag,
        int TextId,
        string MovementAnimation,
        string PortalAnimation,
        string Text);
}
