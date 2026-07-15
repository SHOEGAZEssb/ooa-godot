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
    public ImpaStoneEventRecord StoneRecord { get; }
    public IReadOnlyList<FakeOctorokRecord> Octoroks { get; }
    public Color[] PossessedPalette { get; }
    public Color[] StonePalette { get; }

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

        string stoneRow = ReadSingleRow(
            "res://assets/oracle/cutscenes/impa_stone_event.tsv",
            "Impa stone event");
        string[] stone = stoneRow.Split('\t');
        if (stone.Length != 91)
        {
            throw new InvalidOperationException(
                $"Impa stone event row should contain 91 columns, got {stone.Length}.");
        }
        static int Decimal(string[] values, int index) => int.Parse(values[index]);
        static int Hex(string[] values, int index) => Convert.ToInt32(values[index], 16);
        static string Text(string[] values, int index) =>
            Encoding.UTF8.GetString(Convert.FromBase64String(values[index]));

        StoneRecord = new ImpaStoneEventRecord(
            new ImpaStoneActorRecord(
                Decimal(stone, 0), Hex(stone, 1), Hex(stone, 2), Hex(stone, 3),
                Decimal(stone, 4), Decimal(stone, 5), Decimal(stone, 6),
                Decimal(stone, 7), Decimal(stone, 8), Decimal(stone, 9),
                Decimal(stone, 10), Hex(stone, 11), Hex(stone, 12),
                Decimal(stone, 13), Decimal(stone, 14), Decimal(stone, 15),
                Decimal(stone, 16), Decimal(stone, 17), Decimal(stone, 66),
                Decimal(stone, 67), stone[74], Decimal(stone, 75),
                Decimal(stone, 76), stone[77], Hex(stone, 78), Hex(stone, 79),
                Decimal(stone, 80), Decimal(stone, 81), Decimal(stone, 90) != 0),
            new ImpaStoneTimingRecord(
                Decimal(stone, 18), Decimal(stone, 19), Decimal(stone, 20),
                Decimal(stone, 21), Decimal(stone, 23), Decimal(stone, 24),
                Decimal(stone, 25), Decimal(stone, 26), Decimal(stone, 27),
                Decimal(stone, 28), Decimal(stone, 30), Decimal(stone, 31),
                Decimal(stone, 32), Decimal(stone, 33), Decimal(stone, 34),
                Decimal(stone, 35), Decimal(stone, 37), Decimal(stone, 38),
                Decimal(stone, 39), Decimal(stone, 40), Decimal(stone, 42),
                Decimal(stone, 43), Decimal(stone, 44), Decimal(stone, 46),
                Decimal(stone, 47), Decimal(stone, 48), Decimal(stone, 49),
                Decimal(stone, 50), Decimal(stone, 51), Decimal(stone, 52),
                Decimal(stone, 53), Decimal(stone, 54), Decimal(stone, 55),
                Decimal(stone, 56), Decimal(stone, 57), Decimal(stone, 58),
                Decimal(stone, 59), Decimal(stone, 60), Decimal(stone, 61),
                Decimal(stone, 63), Decimal(stone, 64), Decimal(stone, 65)),
            new ImpaStoneTexts(
                new ImpaStoneText(Hex(stone, 22), Text(stone, 82)),
                new ImpaStoneText(Hex(stone, 29), Text(stone, 83)),
                new ImpaStoneText(Hex(stone, 36), Text(stone, 84)),
                new ImpaStoneText(Hex(stone, 41), Text(stone, 85)),
                new ImpaStoneText(Hex(stone, 45), Text(stone, 86)),
                new ImpaStoneText(Hex(stone, 62), Text(stone, 87)),
                new ImpaStoneText(Hex(stone, 68), Text(stone, 88)),
                new ImpaStoneText(Hex(stone, 69), Text(stone, 89))),
            new ImpaStoneSounds(
                Hex(stone, 70), Hex(stone, 71), Hex(stone, 72), Hex(stone, 73)));

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

        StonePalette = ReadSpritePalette(
            "res://assets/oracle/metadata/impa_stone_palette.bin",
            "Triforce-stone PALH_98 palette");
    }

    private static Color[] ReadSpritePalette(string path, string description)
    {
        byte[] palette = FileAccess.GetFileAsBytes(path);
        if (palette.Length != 12)
        {
            throw new InvalidOperationException(
                $"{description} should contain 12 bytes, got {palette.Length}.");
        }
        var result = new Color[4];
        result[0] = Colors.Transparent;
        for (int color = 1; color < result.Length; color++)
        {
            int offset = color * 3;
            result[color] = new Color(
                palette[offset] / 31.0f,
                palette[offset + 1] / 31.0f,
                palette[offset + 2] / 31.0f);
        }
        return result;
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

    public readonly record struct ImpaStoneEventRecord(
        ImpaStoneActorRecord Actor,
        ImpaStoneTimingRecord Timing,
        ImpaStoneTexts Texts,
        ImpaStoneSounds Sounds);

    public readonly record struct ImpaStoneActorRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        int InitialY,
        int InitialX,
        int MovedY,
        int LeftX,
        int RightX,
        int CollisionRadiusY,
        int CollisionRadiusX,
        int LeftRoomFlag,
        int RightRoomFlag,
        int ApproachY,
        int ApproachX,
        int TargetY,
        int TargetX,
        int CloseRadius,
        int LeaveY,
        int LeaveX,
        string SpriteName,
        int TileBase,
        int Palette,
        string Animation,
        int FinalLayoutTile,
        int FinalCollision,
        int LinkTargetY,
        int LinkTargetX,
        bool SourceGrayscaleInverted)
    {
        public NpcDatabase.NpcRecord ToNpcRecord(int y, int x) => new(
            Group,
            Room,
            InteractionId,
            SubId,
            y,
            x,
            0,
            0,
            SpriteName,
            TileBase,
            Palette,
            0,
            false,
            Animation,
            Animation,
            Animation,
            Animation,
            string.Empty);
    }

    public readonly record struct ImpaStoneTimingRecord(
        int SpotHoldFrames,
        int SpotJumpSpeedZ,
        int Gravity,
        int FirstLandingWait,
        int FirstTextPostFrames,
        int ApproachSpeed,
        int StoneWaitFrames,
        int SecondHoldFrames,
        int SecondJumpSpeedZ,
        int SecondLandingWait,
        int SignTextPostFrames,
        int LinkAxisWaitFrames,
        int LinkTargetWaitFrames,
        int LinkFaceWaitFrames,
        int LinkSpeed,
        int RequestLeadFrames,
        int RequestPostFrames,
        int BackAwaySpeed,
        int FirstBackAwayFrames,
        int BetweenFirstBackAwayFrames,
        int HesitationPostFrames,
        int SecondBackAwayFrames,
        int BetweenSecondBackAwayFrames,
        int FailurePostFrames,
        int PushHoldFrames,
        int StoneMoveFrames,
        int StoneSpeed,
        int LinkPushSpeed,
        int ReactionLeadFrames,
        int LeftCorrectionFrames,
        int LeftCorrectionSpeed,
        int RightBranchWaitFrames,
        int CommonWaitFrames,
        int ResponseRightFrames,
        int ResponseRightSpeed,
        int ResponseWait1Frames,
        int ResponseUpFrames,
        int ResponseWait2Frames,
        int PoseWaitFrames,
        int ThanksPostFrames,
        int FinalSpeed,
        int FinalMoveFrames);

    public readonly record struct ImpaStoneText(int Id, string Message);

    public readonly record struct ImpaStoneTexts(
        ImpaStoneText First,
        ImpaStoneText Sign,
        ImpaStoneText Request,
        ImpaStoneText Hesitation,
        ImpaStoneText Failure,
        ImpaStoneText Thanks,
        ImpaStoneText Leave,
        ImpaStoneText Talk);

    public readonly record struct ImpaStoneSounds(
        int Spot,
        int Push,
        int Stop,
        int Solve);

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
