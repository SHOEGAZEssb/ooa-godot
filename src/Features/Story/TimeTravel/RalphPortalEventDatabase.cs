using System;
using System.Collections.Generic;

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
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/ralph_portal_event.tsv",
            new GeneratedTableSchema(
                "Ralph portal event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "entry-direction", "intro-delay",
                    "post-text", "applyspeed-counter", "flicker-frames", "speed", "angle",
                    "global-flag", "text-id", "move-animation", "portal-animation", "text-base64"
                ],
                headerRequired: true)).SingleRow();

        Record = new RalphPortalEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.UnsignedDecimal(7),
            row.UnsignedDecimal(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexWord(12),
            row.RequiredString(13),
            row.RequiredString(14),
            row.Base64Utf8(15));

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
}

internal readonly record struct RalphPortalEventRecord(int Group, int Room, int InteractionId, int SubId, int EntryDirection, int IntroDelayFrames, int PostTextFrames, int MovementCounter, int FlickerFrames, int Speed, int Angle, int GlobalFlag, int TextId, string MovementAnimation, string PortalAnimation, string Text);
