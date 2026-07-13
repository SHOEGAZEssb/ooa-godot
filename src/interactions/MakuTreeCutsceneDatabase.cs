using Godot;
using System;
using System.Text;

namespace oracleofages;

public sealed class MakuTreeCutsceneDatabase
{
    public const int PaletteCount = 4;
    public const int BackgroundPalettesPerHeader = 4;
    public const int ColorsPerPalette = 4;

    public MakuTreeCutsceneRecord Record { get; }
    public Color[,,] BackgroundPalettes { get; }

    public MakuTreeCutsceneDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/maku_tree_cutscene.tsv");
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
        if (columns.Length != 31)
            throw new InvalidOperationException(
                $"Maku Tree cutscene row should contain 31 columns, got {columns.Length}.");

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
            Convert.ToInt32(columns[17], 16),
            Convert.ToInt32(columns[18], 16),
            int.Parse(columns[19]),
            int.Parse(columns[20]),
            columns[21],
            columns[22],
            columns[23],
            columns[24],
            columns[25],
            columns[26],
            int.Parse(columns[27]),
            Decode(columns[28]),
            Decode(columns[29]),
            Decode(columns[30]));

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
    }

    private static string Decode(string encoded) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(encoded));

    public readonly record struct MakuTreeCutsceneRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
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
