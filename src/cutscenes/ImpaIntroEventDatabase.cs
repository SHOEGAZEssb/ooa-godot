using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

/// <summary>
/// Imported records for the first possessed-Impa encounter in present room
/// $6a, including linkCutscene1 and objectData.impaOctoroks.
/// </summary>
public sealed class ImpaIntroEventDatabase
{
    public ImpaIntroEventRecord Record { get; }
    public ImpaHelpEventRecord HelpRecord { get; }
    public IReadOnlyList<FakeOctorokRecord> Octoroks { get; }
    public Color[] PossessedPalette { get; }

    public ImpaIntroEventDatabase()
    {
        string row = ReadSingleRow(
            "res://assets/oracle/cutscenes/impa_intro_event.tsv",
            "Impa intro event");
        string[] columns = row.Split('\t');
        if (columns.Length != 21)
        {
            throw new InvalidOperationException(
                $"Impa intro event row should contain 21 columns, got {columns.Length}.");
        }

        Record = new ImpaIntroEventRecord(
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
            int.Parse(columns[10]),
            Convert.ToInt32(columns[11], 16),
            int.Parse(columns[12]),
            Convert.ToInt32(columns[13], 16),
            int.Parse(columns[14]),
            int.Parse(columns[15]),
            columns[16],
            columns[17],
            columns[18],
            columns[19],
            Encoding.UTF8.GetString(Convert.FromBase64String(columns[20])));

        string helpRow = ReadSingleRow(
            "res://assets/oracle/cutscenes/impa_help_event.tsv",
            "Impa help event");
        string[] help = helpRow.Split('\t');
        if (help.Length != 11)
        {
            throw new InvalidOperationException(
                $"Impa help event row should contain 11 columns, got {help.Length}.");
        }
        HelpRecord = new ImpaHelpEventRecord(
            int.Parse(help[0]),
            Convert.ToInt32(help[1], 16),
            Convert.ToInt32(help[2], 16),
            Convert.ToInt32(help[3], 16),
            Convert.ToInt32(help[4], 16),
            int.Parse(help[5]),
            int.Parse(help[6]),
            int.Parse(help[7]),
            Convert.ToInt32(help[8], 16),
            int.Parse(help[9]),
            Encoding.UTF8.GetString(Convert.FromBase64String(help[10])));

        var octoroks = new List<FakeOctorokRecord>();
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/impa_intro_octoroks.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] actor = line.Split('\t');
            if (actor.Length != 15)
            {
                throw new InvalidOperationException(
                    $"Fake Octorok row should contain 15 columns, got {actor.Length}.");
            }
            octoroks.Add(new FakeOctorokRecord(
                int.Parse(actor[0]),
                Convert.ToInt32(actor[1], 16),
                Convert.ToInt32(actor[2], 16),
                Convert.ToInt32(actor[3], 16),
                Convert.ToInt32(actor[4], 16),
                Convert.ToInt32(actor[5], 16),
                actor[6],
                int.Parse(actor[7]),
                int.Parse(actor[8]),
                actor[9],
                actor[10],
                int.Parse(actor[11]),
                int.Parse(actor[12]),
                Convert.ToInt32(actor[13], 16),
                Convert.ToInt32(actor[14], 16)));
        }
        if (octoroks.Count != 3)
            throw new InvalidOperationException($"Expected three fake Octoroks, got {octoroks.Count}.");
        Octoroks = octoroks;

        byte[] palette = FileAccess.GetFileAsBytes(
            "res://assets/oracle/metadata/impa_possessed_palette.bin");
        if (palette.Length != 12)
        {
            throw new InvalidOperationException(
                $"Possessed Impa palette should contain 12 bytes, got {palette.Length}.");
        }
        PossessedPalette = new Color[4];
        PossessedPalette[0] = Colors.Transparent;
        for (int color = 1; color < PossessedPalette.Length; color++)
        {
            int offset = color * 3;
            PossessedPalette[color] = new Color(
                palette[offset] / 31.0f,
                palette[offset + 1] / 31.0f,
                palette[offset + 2] / 31.0f);
        }
    }

    private static string ReadSingleRow(string path, string description)
    {
        string source = FileAccess.GetFileAsString(path);
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
                return line;
        }
        throw new InvalidOperationException($"{description} data is empty.");
    }

    public readonly record struct ImpaIntroEventRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        int RoomFlag,
        int LinkWaitFrames,
        int TargetX,
        int CenterWaitFrames,
        int ApproachFrames,
        int LinkSpeed,
        int ImpaWaitFrames,
        int TextId,
        int PostTextFrames,
        int ImpaSpeed,
        int ImpaMoveFrames,
        int FollowLag,
        string UpAnimation,
        string RightAnimation,
        string DownAnimation,
        string LeftAnimation,
        string Text);

    public readonly record struct ImpaHelpEventRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        int RoomFlag,
        int EdgeY,
        int PostTextFrames,
        int InputUpFrames,
        int TextId,
        int TextboxPosition,
        string Text);

    public readonly record struct FakeOctorokRecord(
        int Index,
        int Id,
        int SubId,
        int Y,
        int X,
        int Var03,
        string SpriteName,
        int TileBase,
        int Palette,
        string InitialAnimation,
        string FleeAnimation,
        int SignalWaitFrames,
        int FleeCounter,
        int Angle,
        int Speed)
    {
        public NpcDatabase.NpcRecord ToNpcRecord(int group, int room) => new(
            group,
            room,
            Id,
            SubId,
            Y,
            X,
            Var03,
            0,
            SpriteName,
            TileBase,
            Palette,
            0,
            false,
            InitialAnimation,
            InitialAnimation,
            InitialAnimation,
            InitialAnimation,
            string.Empty);
    }
}
