using Godot;
using System;
using System.Collections.Generic;
using System.Text;

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
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/maku_tree_cutscene.tsv");
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
            throw new InvalidOperationException("Maku Tree cutscene data is empty.");

        string[] columns = row.Split('\t');
        if (columns.Length != 32)
            throw new InvalidOperationException(
                $"Maku Tree cutscene row should contain 32 columns, got {columns.Length}.");

        Record = new MakuTreeCutsceneRecord(
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
            int.Parse(columns[10]),
            int.Parse(columns[11]),
            int.Parse(columns[12]),
            int.Parse(columns[13]),
            int.Parse(columns[14]),
            int.Parse(columns[15]),
            int.Parse(columns[16]),
            int.Parse(columns[17]),
            Convert.ToInt32(columns[18], 16),
            Convert.ToInt32(columns[19], 16),
            int.Parse(columns[20]),
            int.Parse(columns[21]),
            columns[22],
            columns[23],
            columns[24],
            columns[25],
            columns[26],
            columns[27],
            int.Parse(columns[28]),
            Decode(columns[29]),
            Decode(columns[30]),
            Decode(columns[31]));

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

    private static string Decode(string encoded) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(encoded));

    internal readonly record struct MakuTreeCutsceneRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        int InitialPaletteHeader,
        int InputIdleFrames,
        int InputRightFrames,
        int InputStopFrames,
        int InputUpFrames,
        int InputTailFrames,
        int IntroDelayFrames,
        int PostIntroFrames,
        int FrownFrames,
        int DisappearanceFrames,
        int PostAhhFrames,
        int FinishDelayFrames,
        int SourceTransition,
        int DestinationGroup,
        int DestinationRoom,
        int DestinationPosition,
        int DestinationParameter,
        int DestinationTransition,
        string Animation0,
        string Animation1,
        string Animation2,
        string Animation3,
        string Animation4,
        string ExtraSprite,
        int TextboxPosition,
        string IntroText,
        string AhhText,
        string HelpText);
}
