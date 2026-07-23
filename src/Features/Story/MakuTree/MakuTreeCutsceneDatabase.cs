using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MakuTreeCutsceneDatabase
{
    public const int PaletteCount = 4;
    public const int BackgroundPalettesPerHeader = 6;
    public const int ColorsPerPalette = 4;

    public MakuTreeCutsceneRecord Record { get; }
    public Color[,,] BackgroundPalettes { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public MakuTreeCutsceneDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/maku_tree_cutscene.tsv",
            new GeneratedTableSchema(
                "Maku Tree disappearance cutscene",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "initial-palette", "input-idle",
                    "input-right", "input-stop", "input-up", "input-tail", "intro-delay",
                    "post-intro", "frown-delay", "disappearance", "post-ahh", "finish-delay",
                    "source-transition", "destination-group", "destination-room",
                    "destination-position", "destination-parameter", "destination-transition",
                    "animation0", "animation1", "animation2", "animation3", "animation4",
                    "extra-sprite", "textbox-position", "intro-base64", "ahh-base64", "help-base64"
                ],
                headerRequired: true)).SingleRow();

        Record = new MakuTreeCutsceneRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.UnsignedDecimal(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.UnsignedDecimal(7),
            row.UnsignedDecimal(8),
            row.UnsignedDecimal(9),
            row.UnsignedDecimal(10),
            row.UnsignedDecimal(11),
            row.UnsignedDecimal(12),
            row.UnsignedDecimal(13),
            row.UnsignedDecimal(14),
            row.UnsignedDecimal(15),
            row.UnsignedDecimal(16),
            row.Decimal(17, 0, 7),
            row.HexByte(18),
            row.HexByte(19),
            row.UnsignedDecimal(20),
            row.UnsignedDecimal(21),
            row.RequiredString(22),
            row.RequiredString(23),
            row.RequiredString(24),
            row.RequiredString(25),
            row.RequiredString(26),
            row.RequiredString(27),
            row.UnsignedDecimal(28),
            row.Base64Utf8(29),
            row.Base64Utf8(30),
            row.Base64Utf8(31));

        byte[] bytes = FileAccess.GetFileAsBytes(
            "res://assets/oracle/metadata/maku_tree_disappear_palettes.bin");
        const int expectedLength = PaletteCount * BackgroundPalettesPerHeader *
            ColorsPerPalette * 3;
        if (bytes.Length != expectedLength)
            throw new InvalidOperationException(
                $"Maku Tree palette data should contain {expectedLength} bytes, got {bytes.Length}.");

        BackgroundPalettes = new Color[
            PaletteCount, BackgroundPalettesPerHeader, ColorsPerPalette];
        int offset = 0;
        for (int header = 0; header < PaletteCount; header++)
        for (int palette = 0; palette < BackgroundPalettesPerHeader; palette++)
        for (int shade = 0; shade < ColorsPerPalette; shade++)
        {
            BackgroundPalettes[header, palette, shade] = new Color(
                bytes[offset++] / 31.0f,
                bytes[offset++] / 31.0f,
                bytes[offset++] / 31.0f);
        }

        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/maku_tree_commands.tsv");
        ValidateCommandStream();
    }

    private void ValidateCommandStream()
    {
        if (Commands.Count != 23 ||
            Commands[0] is not CutsceneDisableMenuCommand ||
            !MatchesAnimation(Commands[1], 0x00, Record.Animation0) ||
            Commands[2] is not CutsceneSetCollisionRadiiCommand
                { Actor: "MakuTree", RadiusY: 0x08, RadiusX: 0x08 } ||
            Commands[3] is not CutsceneMakeAButtonSensitiveCommand
                { Actor: "MakuTree" } ||
            Commands[4] is not CutsceneGateCommand { Gate: "palette-fade-done" } ||
            !MatchesWait(Commands[5], Record.IntroDelayFrames) ||
            !MatchesText(Commands[6], 0x0564, Record.IntroText) ||
            !MatchesWait(Commands[7], Record.PostIntroFrames) ||
            Commands[8] is not CutscenePlaySoundCommand
                { Sound: OracleSoundEngine.SndCtrlStopMusic } ||
            !MatchesAnimation(Commands[9], 0x04, Record.Animation4) ||
            !MatchesWait(Commands[10], Record.FrownFrames) ||
            Commands[11] is not CutscenePlaySoundCommand
                { Sound: OracleSoundEngine.SndMakuDisappear } ||
            Commands[12] is not CutsceneWriteMemoryCommand
                { Binding: "wCutsceneTrigger", Value: 0x07 } ||
            !MatchesWait(Commands[13], Record.DisappearanceFrames) ||
            !MatchesText(Commands[14], 0x0540, Record.AhhText) ||
            Commands[15] is not CutscenePlaySoundCommand
                { Sound: OracleSoundEngine.SndMakuDisappear } ||
            !MatchesWait(Commands[16], Record.PostAhhFrames) ||
            !MatchesText(Commands[17], 0x0541, Record.HelpText) ||
            Commands[18] is not CutscenePlaySoundCommand
                { Sound: OracleSoundEngine.SndMakuDisappear } ||
            !MatchesWait(Commands[19], Record.FinishDelayFrames) ||
            Commands[20] is not CutsceneWriteMemoryCommand
                { Binding: "wTmpcfc0.genericCutscene.state", Value: 0x01 } ||
            Commands[21] is not CutsceneNativeCommand { Handler: "incMakuTreeState" } ||
            Commands[22] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "makuTree_subid01Script_body command stream diverges from imported metadata.");
        }
    }

    private static bool MatchesAnimation(
        CutsceneCommand command,
        int animation,
        string encodedAnimation) =>
        command is CutsceneSetAnimationContinueCommand
        {
            Actor: "MakuTree",
            Animation: var actualAnimation,
            EncodedAnimation: var actualEncoding
        } && actualAnimation == animation && actualEncoding == encodedAnimation;

    private static bool MatchesWait(CutsceneCommand command, int frames) =>
        command is CutsceneWaitCommand { Frames: var actual } && actual == frames;

    private static bool MatchesText(
        CutsceneCommand command,
        int textId,
        string message) =>
        command is CutsceneShowTextCommand
            { TextId: var actualId, Message: var actualMessage } &&
        actualId == textId && actualMessage == message;
}

internal readonly record struct MakuTreeCutsceneRecord(int Group, int Room, int InteractionId, int SubId, int InitialPaletteHeader, int InputIdleFrames, int InputRightFrames, int InputStopFrames, int InputUpFrames, int InputTailFrames, int IntroDelayFrames, int PostIntroFrames, int FrownFrames, int DisappearanceFrames, int PostAhhFrames, int FinishDelayFrames, int SourceTransition, int DestinationGroup, int DestinationRoom, int DestinationPosition, int DestinationParameter, int DestinationTransition, string Animation0, string Animation1, string Animation2, string Animation3, string Animation4, string ExtraSprite, int TextboxPosition, string IntroText, string AhhText, string HelpText);
