using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Source-derived CPU work for blocking original graphics loads.</summary>
internal sealed class OracleLoadingWork
{
    private readonly int[] _graphics = new int[0xbb];
    private readonly Dictionary<string, List<LoadingStep>> _plans = new(StringComparer.Ordinal);
    internal static OracleLoadingWork Shared { get; } = new();

    private OracleLoadingWork()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/timing/graphics_cpu.tsv",
            new GeneratedTableSchema("original graphics CPU work", GeneratedTableKeySemantics.Unique,
                ["header", "cpu-cycles", "source"], ["header"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int header = row.HexByte(0);
            if (header >= _graphics.Length)
                throw new InvalidOperationException($"Unknown source graphics header ${header:x2}.");
            _graphics[header] = row.Decimal(1, 1, 10_000_000);
            _ = row.RequiredString(2);
        }
        if (table.Rows.Count != _graphics.Length)
            throw new InvalidOperationException("Original graphics CPU work must cover headers $00-$ba.");
        GeneratedTable plans = GeneratedTable.Load(
            "res://assets/oracle/timing/frontend_cpu.tsv",
            new GeneratedTableSchema("original frontend CPU work", GeneratedTableKeySemantics.Unique,
                ["plan", "step", "kind", "value", "source"], ["plan", "step"], headerRequired: true));
        foreach (GeneratedTableRow row in plans.Rows)
        {
            string name = row.RequiredString(0);
            if (!_plans.TryGetValue(name, out var steps)) _plans.Add(name, steps = []);
            if (row.Decimal(1, 0, 1000) != steps.Count)
                throw new InvalidOperationException($"Frontend work {name} has unordered steps.");
            string kind = row.RequiredString(2);
            if (kind is not ("cpu" or "timer" or "sound-stop" or "sound" or "lcd-off" or "lcd" or "gfx" or "dma-gfx" or "vblank-work"))
                throw new InvalidOperationException($"Unsupported frontend work operation {kind}.");
            steps.Add(new LoadingStep(kind, row.Decimal(3, 0, 10_000_000)));
        }
    }

    internal int Graphics(int header) => (uint)header < _graphics.Length
        ? _graphics[header]
        : throw new ArgumentOutOfRangeException(nameof(header), $"Unsupported graphics header ${header:x2}.");

    internal IReadOnlyList<LoadingStep> Plan(string name) => _plans.TryGetValue(name, out var steps)
        ? steps : throw new InvalidOperationException($"Missing source loading plan {name}.");

    internal IReadOnlyList<LoadingStep> CommitName(string rawName)
    {
        int length = rawName.Replace('\0', ' ').TrimEnd(' ').Length;
        int zeros = 0;
        foreach (char character in rawName) if (character == '\0') zeros++;
        var result = new List<LoadingStep>(Plan($"name-commit-{length}"));
        int perZero = (Plan("name-commit-1")[0].Value - Plan("name-commit-spaces")[0].Value) / 4;
        result[0] = result[0] with { Value = result[0].Value + (zeros - (5 - length)) * perZero };
        return result;
    }

    internal IReadOnlyList<LoadingStep> EnterName(Func<int, OracleSaveData?> load)
    {
        var result = new List<LoadingStep>(Plan("name-entry"));
        // copyNameToW4NameBuffer replaces the first six bytes with the empty
        // selected file. The other two file-display names remain in this buffer.
        int glyphs = NameGlyphs(load(1)) + NameGlyphs(load(2));
        int perGlyph = Plan("file-name")[10].Value - Plan("file-1")[10].Value;
        result[8] = result[8] with { Value = result[8].Value + glyphs * perGlyph };
        return result;
    }

    internal IReadOnlyList<LoadingStep> ReturnToFiles(Func<int, OracleSaveData?> load)
    {
        var result = new List<LoadingStep>(Plan("files-return"));
        var populated = Files(load, verifiedBefore: true);
        var cleared = Plan("files-cleared");
        // The existing file thread enters mode 1 directly, after the first
        // six steps of the cold mode-0 plan. The shared mode-1 body is identical.
        for (int i = 1; i < result.Count; i++)
            if (result[i].Kind == "cpu")
                result[i] = result[i] with { Value = result[i].Value + populated[i + 6].Value - cleared[i + 6].Value };
        return result;
    }

    private static int NameGlyphs(OracleSaveData? save)
    {
        if (save is null) return 0;
        int count = 0;
        for (int index = 0; index < 6; index++)
        {
            byte character = save.ReadWramByte(0xc602 + index);
            if (character == 0x06)
                throw new InvalidOperationException("copyTextCharactersFromHl: Japanese font escape $06 in a clean-US file name.");
            if (character >= 0x0e) count++;
        }
        return count;
    }

    internal IReadOnlyList<LoadingStep> Files(Func<int, OracleSaveData?> load, bool verifiedBefore = false)
    {
        var result = new List<LoadingStep>(Plan("files"));
        IReadOnlyList<LoadingStep> empty = Plan("files");
        for (int slot = 0; slot < 3; slot++)
        {
            OracleSaveData? save = load(slot);
            if (save is null)
            {
                // verifyFileAtHl clears invalid SRAM copies. A later visit
                // verifies zeros instead of the cold cartridge's $ff bytes.
                if (verifiedBefore)
                    result[10] = result[10] with { Value = result[10].Value +
                        (Plan("files-cleared")[10].Value - empty[10].Value) / 3 };
                continue;
            }
            IReadOnlyList<LoadingStep> occupied = Plan($"file-{slot}");
            for (int i = 0; i < result.Count; i++)
            {
                if (empty[i].Kind != "cpu") continue;
                result[i] = result[i] with { Value = result[i].Value + occupied[i].Value - empty[i].Value };
            }
            int ordinaryCharacters = NameGlyphs(save);
            // copyTextCharacterGfx's ordinary-character branch costs four
            // fewer clocks than its control-character-to-space branch.
            int nameDelta = Plan("file-name")[10].Value - Plan("file-1")[10].Value;
            result[10] = result[10] with { Value = result[10].Value + ordinaryCharacters * nameDelta };
            if (slot == 0)
                result[14] = result[14] with { Value = result[14].Value +
                    Plan($"hearts-{save.MaxHealthQuarters:x2}")[0].Value - Plan("hearts-00")[0].Value };
        }
        return result;
    }
}

internal readonly record struct LoadingStep(string Kind, int Value);
