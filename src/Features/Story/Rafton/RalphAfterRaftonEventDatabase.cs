using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_RALPH $37:$03 native metadata and ralphSubid03Script for
/// past overworld room $1:$97.
/// </summary>
internal sealed class RalphAfterRaftonEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";

    internal RalphAfterRaftonEventRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }

    internal RalphAfterRaftonEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "ralph_after_rafton_event.tsv",
            new GeneratedTableSchema(
                "room 1:97 Ralph-after-Rafton event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "sprite", "tile-base",
                    "palette", "animation0", "animation1", "animation2",
                    "animation3", "default-animation", "initial-animation",
                    "initial-direction", "room-flag", "required-global-flag",
                    "disabled-objects", "menu-disabled", "look-counter",
                    "look-frame-mask", "look-direction-xor", "post-look-wait",
                    "jump-speed-z", "jump-gravity", "jump-sound",
                    "landing-wait", "native-text-id", "native-text-base64",
                    "post-text-wait", "approach-counter", "speed-100",
                    "down-angle", "align-wait", "speed-200", "exit-counter",
                    "fade-sound", "completion-music", "initial-native-updates"
                ],
                headerRequired: true)).SingleRow();
        Record = new RalphAfterRaftonEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.RequiredString(4),
            row.HexByte(5),
            row.HexByte(6),
            row.RequiredString(7),
            row.RequiredString(8),
            row.RequiredString(9),
            row.RequiredString(10),
            row.HexByte(11),
            row.HexByte(12),
            row.HexByte(13),
            row.HexByte(14),
            row.HexByte(15),
            row.HexByte(16),
            row.HexByte(17),
            row.HexByte(18),
            row.HexByte(19),
            row.HexByte(20),
            row.HexByte(21),
            row.Decimal(22, short.MinValue, short.MaxValue),
            row.HexByte(23),
            row.HexByte(24),
            row.HexByte(25),
            row.HexWord(26),
            row.Base64Utf8(27),
            row.HexByte(28),
            row.HexByte(29),
            row.HexByte(30),
            row.HexByte(31),
            row.HexByte(32),
            row.HexByte(33),
            row.HexByte(34),
            row.HexByte(35),
            row.HexByte(36),
            row.UnsignedDecimal(37));
        Commands = CutsceneCommandCatalog.Load(
            Root + "ralph_after_rafton_commands.tsv");
        Validate();
    }

    internal bool Matches(NpcRecord record) =>
        record.Group == Record.Group &&
        record.Room == Record.Room &&
        record.Id == Record.InteractionId &&
        record.SubId == Record.SubId;

    private void Validate()
    {
        if (Record is not
            {
                Group: 1,
                Room: 0x97,
                InteractionId: 0x37,
                SubId: 0x03,
                Sprite: "spr_ralph_1",
                TileBase: 0,
                Palette: 1,
                DefaultAnimation: 2,
                InitialAnimation: 3,
                InitialDirection: 1,
                RoomFlag: OracleSaveData.RoomFlag40,
                RequiredGlobalFlag: 0x15,
                DisabledObjects: 0x01,
                MenuDisabled: 0x01,
                LookCounter: 0x78,
                LookFrameMask: 0x0f,
                LookDirectionXor: 0x02,
                PostLookWait: 0x1e,
                JumpSpeedZ: -0x01c0,
                JumpGravity: 0x20,
                JumpSound: OracleSoundEngine.SndJump,
                LandingWait: 0x0a,
                NativeTextId: 0x2a0a,
                PostTextWait: 0x1e,
                ApproachCounter: 0x30,
                Speed100: 0x28,
                DownAngle: 0x10,
                AlignWait: 0x06,
                Speed200: 0x50,
                ExitCounter: 0x44,
                FadeSound: OracleSoundEngine.SndCtrlFastFadeOut,
                CompletionMusic: 0x04,
                InitialNativeUpdates: 0
            } ||
            Enumerable.Range(0, 4).Any(animation =>
                string.IsNullOrWhiteSpace(Record.Animation(animation))) ||
            string.IsNullOrWhiteSpace(Record.NativeText))
        {
            throw new InvalidOperationException(
                "Room 1:97 Ralph metadata diverges from its source contract.");
        }

        if (Commands.Count != 16 ||
            Commands[0] is not CutsceneWaitCommand { Frames: 6 } ||
            Commands[1] is not CutsceneSetAnimationCommand
                { Actor: "Ralph", Animation: 2 } ||
            Commands[2] is not CutsceneWaitCommand { Frames: 10 } ||
            Commands[3] is not CutsceneShowTextCommand { TextId: 0x2a0b } ||
            Commands[4] is not CutsceneWaitCommand { Frames: 20 } ||
            Commands[5] is not CutsceneSetAnimationCommand
                { Actor: "Ralph", Animation: 0 } ||
            Commands[6] is not CutsceneWaitCommand { Frames: 20 } ||
            Commands[7] is not CutsceneShowTextCommand { TextId: 0x2a06 } ||
            Commands[8] is not CutsceneWaitCommand { Frames: 10 } ||
            Commands[9] is not CutsceneSetSpeedCommand
                { Actor: "Ralph", Speed: 0x50 } ||
            Commands[10] is not CutsceneMoveCommand
                { Actor: "Ralph", Angle: 0x00, Counter: 0x44 } ||
            Commands[11] is not CutscenePlaySoundCommand { Sound: 0xfa } ||
            Commands[12] is not CutsceneWaitCommand { Frames: 30 } ||
            Commands[13] is not CutsceneOrRoomFlagCommand { Flag: 0x40 } ||
            Commands[14] is not CutsceneEnableInputCommand ||
            Commands[15] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "ralphSubid03Script command stream diverges from imported metadata.");
        }

        foreach (CutsceneSetAnimationCommand animation in
                 Commands.OfType<CutsceneSetAnimationCommand>())
        {
            if (animation.EncodedAnimation != Record.Animation(animation.Animation))
            {
                throw new InvalidOperationException(
                    $"Ralph animation ${animation.Animation:x2} diverges at " +
                    animation.Source + ".");
            }
        }
        CutsceneMoveCommand movement = (CutsceneMoveCommand)Commands[10];
        if (movement.EncodedAnimation != Record.Animation(0))
        {
            throw new InvalidOperationException(
                "ralphSubid03Script moveup animation diverges from animation $00.");
        }
    }
}

internal readonly record struct RalphAfterRaftonEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    string Sprite,
    int TileBase,
    int Palette,
    string Animation0,
    string Animation1,
    string Animation2,
    string Animation3,
    int DefaultAnimation,
    int InitialAnimation,
    int InitialDirection,
    int RoomFlag,
    int RequiredGlobalFlag,
    int DisabledObjects,
    int MenuDisabled,
    int LookCounter,
    int LookFrameMask,
    int LookDirectionXor,
    int PostLookWait,
    int JumpSpeedZ,
    int JumpGravity,
    int JumpSound,
    int LandingWait,
    int NativeTextId,
    string NativeText,
    int PostTextWait,
    int ApproachCounter,
    int Speed100,
    int DownAngle,
    int AlignWait,
    int Speed200,
    int ExitCounter,
    int FadeSound,
    int CompletionMusic,
    int InitialNativeUpdates)
{
    internal string Animation(int index) => index switch
    {
        0 => Animation0,
        1 => Animation1,
        2 => Animation2,
        3 => Animation3,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
