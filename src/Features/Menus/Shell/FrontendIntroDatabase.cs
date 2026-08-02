using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Typed generated-data boundary for runIntro, runIntroCinematic, and their
/// reachable US Ages interaction animations.
/// </summary>
internal sealed class FrontendIntroDatabase
{
    private readonly Dictionary<string, int> _timings =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, FrontendSequenceValue[]> _sequences =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, FrontendAnimation> _animations =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, FrontendOamPart[]> _staticOam =
        new(StringComparer.Ordinal);

    internal static FrontendIntroDatabase Shared { get; } = new();

    private FrontendIntroDatabase()
    {
        LoadTimings();
        LoadSequences();
        LoadAnimations();
        LoadStaticOam();
    }

    internal int Timing(string key) =>
        _timings.TryGetValue(key, out int value)
            ? value
            : throw new InvalidOperationException(
                $"Imported frontend timing '{key}' does not exist.");

    internal FrontendSequenceValue[] Sequence(string key) =>
        _sequences.TryGetValue(key, out FrontendSequenceValue[]? values)
            ? values
            : throw new InvalidOperationException(
                $"Imported frontend sequence '{key}' does not exist.");

    internal FrontendAnimation Animation(string key) =>
        _animations.TryGetValue(key, out FrontendAnimation? animation)
            ? animation
            : throw new InvalidOperationException(
                $"Imported frontend animation '{key}' does not exist.");

    internal FrontendOamPart[] StaticOam(string key) =>
        _staticOam.TryGetValue(key, out FrontendOamPart[]? parts)
            ? parts
            : throw new InvalidOperationException(
                $"Imported frontend OAM layout '{key}' does not exist.");

    private void LoadTimings()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/intro/timing.tsv",
            new GeneratedTableSchema(
                "frontend intro timing",
                GeneratedTableKeySemantics.Unique,
                ["key", "value", "source"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _timings.Add(
                row.RequiredString(0),
                row.Decimal(1, 1, ushort.MaxValue));
            _ = row.RequiredString(2);
        }
        if (_timings.Count != 24)
        {
            throw new InvalidOperationException(
                $"Frontend intro timing has {_timings.Count} records; expected 24.");
        }
    }

    private void LoadSequences()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/intro/sequences.tsv",
            new GeneratedTableSchema(
                "frontend intro sequences",
                GeneratedTableKeySemantics.Grouped,
                ["sequence", "index", "value-a", "value-b", "source"],
                ["sequence"],
                headerRequired: true));
        var grouped = new Lookup<string, FrontendSequenceValue>(
            StringComparer.Ordinal);
        foreach (GeneratedTableRow row in table.Rows)
        {
            string sequence = row.RequiredString(0);
            List<FrontendSequenceValue> values = grouped.GetOrAdd(sequence);
            int index = row.UnsignedDecimal(1);
            if (index != values.Count)
            {
                throw row.Invalid(
                    1,
                    $"ordered {sequence} index {values.Count}");
            }
            values.Add(new FrontendSequenceValue(
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3),
                row.RequiredString(4)));
        }
        foreach ((string key, IReadOnlyList<FrontendSequenceValue> values) in grouped)
            _sequences.Add(key, values.ToArray());

        RequireSequenceCount("temple-input", 18);
        RequireSequenceCount("temple-background-animation", 1);
        RequireSequenceCount("triforce-position", 3);
        RequireSequenceCount("triforce-motion", 3);
        RequireSequenceCount("temple-wave-sine", 32);
        RequireSequenceCount("title-size", 8);
        RequireSequenceCount("bird-position", 8);
        RequireSequenceCount("cloud-position", 4);
        RequireSequenceCount("horse-face-registers", 2);
        RequireSequenceCount("horse-face-bars", 2);
        RequireSequenceCount("horse-face-motion", 2);
        RequireSequenceCount("horse-face-sparkle", 2);
        RequireSequenceCount("castle-actor-position", 1);
        RequireSequenceCount("castle-actor-motion", 1);
        RequireSequenceCount("castle-animation", 2);
    }

    private void LoadAnimations()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/intro/animations.tsv",
            new GeneratedTableSchema(
                "frontend intro animations",
                GeneratedTableKeySemantics.Grouped,
                [
                    "kind", "index", "duration", "loop-start", "parameter",
                    "base-palette", "source-tile-offset", "source-inverted",
                    "oam-parts", "source"
                ],
                ["kind"],
                headerRequired: true));
        var grouped = new Lookup<string, FrontendAnimationFrame>(
            StringComparer.Ordinal);
        var loopStarts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (GeneratedTableRow row in table.Rows)
        {
            string kind = row.RequiredString(0);
            List<FrontendAnimationFrame> frames = grouped.GetOrAdd(kind);
            int index = row.UnsignedDecimal(1);
            if (index != frames.Count)
                throw row.Invalid(1, $"ordered {kind} frame {frames.Count}");
            int loopStart = row.UnsignedDecimal(3);
            if (loopStarts.TryGetValue(kind, out int priorLoop) &&
                priorLoop != loopStart)
            {
                throw row.Invalid(3, $"consistent loop start {priorLoop}");
            }
            loopStarts[kind] = loopStart;
            frames.Add(new FrontendAnimationFrame(
                row.UnsignedDecimal(2),
                row.HexByte(4),
                row.Decimal(5, 0, 7),
                row.HexWord(6),
                row.Decimal(7, 0, 1) != 0,
                ParseOamParts(row.String(8)),
                row.RequiredString(9)));
        }

        foreach ((string kind, IReadOnlyList<FrontendAnimationFrame> frames) in grouped)
        {
            int loopStart = loopStarts[kind];
            if (loopStart < 0 || loopStart >= frames.Count)
            {
                throw new InvalidOperationException(
                    $"Frontend animation {kind} has invalid loop start {loopStart}.");
            }
            _animations.Add(kind, new FrontendAnimation(
                frames.ToArray(), loopStart));
        }

        string[] expected =
        [
            "horse-0", "horse-1", "horse-2", "horse-3", "horse-4",
            "horse-5", "horse-6", "triforce", "triforce-glow",
            "tree-branches", "cloud-0", "cloud-1", "cloud-2", "cloud-3",
            "bird-0", "bird-1", "temple-link-walk", "temple-link-rise",
            "temple-link-fall"
        ];
        if (_animations.Count != expected.Length ||
            expected.Any(key => !_animations.ContainsKey(key)))
        {
            throw new InvalidOperationException(
                "Frontend intro animation coverage does not match the reachable US dispatch.");
        }
    }

    private void LoadStaticOam()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/intro/static_oam.tsv",
            new GeneratedTableSchema(
                "frontend static OAM",
                GeneratedTableKeySemantics.Grouped,
                [
                    "layout", "part", "y", "x", "tile", "attributes",
                    "source-label", "source"
                ],
                ["layout"],
                headerRequired: true));
        var grouped = new Lookup<string, FrontendOamPart>(
            StringComparer.Ordinal);
        foreach (GeneratedTableRow row in table.Rows)
        {
            string layout = row.RequiredString(0);
            List<FrontendOamPart> parts = grouped.GetOrAdd(layout);
            int index = row.UnsignedDecimal(1);
            if (index != parts.Count)
                throw row.Invalid(1, $"ordered {layout} part {parts.Count}");
            parts.Add(new FrontendOamPart(
                row.HexByte(2), row.HexByte(3), row.HexByte(4), row.HexByte(5)));
            _ = row.RequiredString(6);
            _ = row.RequiredString(7);
        }
        foreach ((string key, IReadOnlyList<FrontendOamPart> parts) in grouped)
            _staticOam.Add(key, parts.ToArray());
        RequireStaticOamCount("closeup-touchup", 38);
        RequireStaticOamCount("castle-touchup", 5);
        RequireStaticOamCount("front-facing-link", 2);
    }

    private void RequireSequenceCount(string key, int expected)
    {
        int actual = Sequence(key).Length;
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Frontend sequence {key} has {actual} records; expected {expected}.");
        }
    }

    private void RequireStaticOamCount(string key, int expected)
    {
        int actual = StaticOam(key).Length;
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Frontend OAM layout {key} has {actual} parts; expected {expected}.");
        }
    }

    private static FrontendOamPart[] ParseOamParts(string value)
    {
        if (string.IsNullOrEmpty(value))
            return [];
        return value.Split(';').Select(encoded =>
        {
            string[] fields = encoded.Split(',');
            if (fields.Length != 4)
            {
                throw new InvalidOperationException(
                    $"Malformed frontend OAM part '{encoded}'.");
            }
            return new FrontendOamPart(
                Convert.ToInt32(fields[0], 16),
                Convert.ToInt32(fields[1], 16),
                Convert.ToInt32(fields[2], 16),
                Convert.ToInt32(fields[3], 16));
        }).ToArray();
    }
}

internal readonly record struct FrontendSequenceValue(int A, int B, string Source);

internal sealed record FrontendAnimation(
    FrontendAnimationFrame[] Frames,
    int LoopStart);

internal readonly record struct FrontendAnimationFrame(
    int Duration,
    int Parameter,
    int BasePalette,
    int SourceTileOffset,
    bool SourceGrayscaleInverted,
    FrontendOamPart[] Parts,
    string Source);

internal readonly record struct FrontendOamPart(int Y, int X, int Tile, int Flags);
