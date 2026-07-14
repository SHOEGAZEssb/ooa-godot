using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace oracleofages;

/// <summary>
/// Imported CUTSCENE_PREGAME_INTRO and linkSummonedCutscene parameters.
/// </summary>
public sealed class NewGameIntroDatabase
{
    public NewGameIntroRecord Record { get; }
    private readonly Dictionary<string, IntroSpriteFrame[]> _spriteFrames = new();

    public NewGameIntroDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/new_game_intro.tsv");
        string? row = source.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .FirstOrDefault(line => !line.StartsWith('#'));
        if (row is null)
            throw new InvalidOperationException("New-game intro data is empty.");

        string[] columns = row.Split('\t');
        if (columns.Length != 17)
            throw new InvalidOperationException(
                $"New-game intro row should contain 17 columns, got {columns.Length}.");

        Record = new NewGameIntroRecord(
            int.Parse(columns[0]),
            int.Parse(columns[1]),
            int.Parse(columns[2]),
            int.Parse(columns[3]),
            int.Parse(columns[4]),
            int.Parse(columns[5]),
            Convert.ToInt32(columns[6], 16),
            Convert.ToInt32(columns[7], 16),
            int.Parse(columns[8]),
            int.Parse(columns[9]),
            int.Parse(columns[10]),
            ParseCsv(columns[11]),
            ParseCsv(columns[12]),
            ParseCsv(columns[13]),
            ParseSignedCsv(columns[14]),
            ParseSignedCsv(columns[15]),
            Encoding.UTF8.GetString(Convert.FromBase64String(columns[16])));

        LoadSpriteFrames();
    }

    public IntroSpriteFrame[] SpriteFrames(string kind) =>
        _spriteFrames.TryGetValue(kind, out IntroSpriteFrame[]? frames)
            ? frames
            : throw new InvalidOperationException($"New-game intro sprite kind is missing: {kind}.");

    private void LoadSpriteFrames()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/new_game_intro_sprites.tsv");
        var grouped = new Dictionary<string, List<IntroSpriteFrame>>();
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] columns = line.Split('\t');
            if (columns.Length != 6)
                throw new InvalidOperationException(
                    $"New-game intro sprite row should contain 6 columns, got {columns.Length}.");

            string kind = columns[0];
            int index = int.Parse(columns[1]);
            var parts = columns[5].Split(';').Select(value =>
            {
                string[] fields = value.Split(',');
                if (fields.Length != 4)
                    throw new InvalidOperationException("Invalid new-game intro OAM part.");
                return new IntroOamPart(
                    Convert.ToInt32(fields[0], 16),
                    Convert.ToInt32(fields[1], 16),
                    Convert.ToInt32(fields[2], 16),
                    Convert.ToInt32(fields[3], 16));
            }).ToArray();
            if (!grouped.TryGetValue(kind, out List<IntroSpriteFrame>? frames))
            {
                frames = new List<IntroSpriteFrame>();
                grouped[kind] = frames;
            }
            if (index != frames.Count)
                throw new InvalidOperationException(
                    $"New-game intro sprite {kind} expected frame {frames.Count}, got {index}.");
            frames.Add(new IntroSpriteFrame(
                int.Parse(columns[2]),
                Convert.ToInt32(columns[3], 16),
                int.Parse(columns[4]),
                parts));
        }
        foreach ((string kind, List<IntroSpriteFrame> frames) in grouped)
            _spriteFrames[kind] = frames.ToArray();
    }

    private static int[] ParseCsv(string value) => value.Split(',')
        .Select(part => Convert.ToInt32(part, 16))
        .ToArray();

    private static int[] ParseSignedCsv(string value) => ParseCsv(value)
        .Select(part => part >= 0x80 ? part - 0x100 : part)
        .ToArray();

    public readonly record struct NewGameIntroRecord(
        int InitialWaitFrames,
        int VoiceWaitFrames,
        int PostVanishWaitFrames,
        int SummonFrames,
        int LinkX,
        int LinkY,
        int LinkSummonedFlag,
        int PregameIntroDoneFlag,
        int TextPosition,
        int TextId,
        int SpinFrameDuration,
        int[] SpinGraphics,
        int[] VanishDurations,
        int[] VanishGraphics,
        int[] DescendOscillation,
        int[] HoverOscillation,
        string Text);

    public readonly record struct IntroSpriteFrame(
        int Duration,
        int SourceOffset,
        int BasePalette,
        IntroOamPart[] Parts);

    public readonly record struct IntroOamPart(int Y, int X, int Tile, int Flags);
}
