using System;
using System.Collections.Generic;
using System.Linq;

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
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/new_game_intro.tsv",
            new GeneratedTableSchema(
                "new-game intro",
                GeneratedTableKeySemantics.Ordered,
                [
                    "initial-wait", "voice-wait", "post-vanish-wait", "summon-frames",
                    "link-x", "link-y", "link-summoned-flag", "pregame-done-flag",
                    "textbox-position", "text-id", "spin-duration", "spin-graphics",
                    "vanish-durations", "vanish-graphics", "descend-oscillation",
                    "hover-oscillation", "text-base64"
                ],
                headerRequired: true)).SingleRow();

        Record = new NewGameIntroRecord(
            row.UnsignedDecimal(0),
            row.UnsignedDecimal(1),
            row.UnsignedDecimal(2),
            row.UnsignedDecimal(3),
            row.UnsignedDecimal(4),
            row.UnsignedDecimal(5),
            row.HexByte(6),
            row.HexByte(7),
            row.UnsignedDecimal(8),
            row.UnsignedDecimal(9),
            row.UnsignedDecimal(10),
            ParseCsv(row.RequiredString(11)),
            ParseCsv(row.RequiredString(12)),
            ParseCsv(row.RequiredString(13)),
            ParseSignedCsv(row.RequiredString(14)),
            ParseSignedCsv(row.RequiredString(15)),
            row.Base64Utf8(16));

        LoadSpriteFrames();
    }

    public IntroSpriteFrame[] SpriteFrames(string kind) =>
        _spriteFrames.TryGetValue(kind, out IntroSpriteFrame[]? frames)
            ? frames
            : throw new InvalidOperationException($"New-game intro sprite kind is missing: {kind}.");

    private void LoadSpriteFrames()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/new_game_intro_sprites.tsv",
            new GeneratedTableSchema(
                "new-game intro sprites",
                GeneratedTableKeySemantics.Grouped,
                ["kind", "index", "duration", "source-offset", "base-palette", "oam-parts"],
                ["kind"],
                headerRequired: true));
        var grouped = new Dictionary<string, List<IntroSpriteFrame>>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            string kind = row.RequiredString(0);
            int index = row.UnsignedDecimal(1);
            var parts = row.RequiredString(5).Split(';').Select(value =>
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
                row.UnsignedDecimal(2),
                row.HexWord(3),
                row.UnsignedDecimal(4),
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
