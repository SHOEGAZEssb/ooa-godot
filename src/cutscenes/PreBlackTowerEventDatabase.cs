using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported actors, effects, and independent interaction-script lanes for the
/// pre-Black Tower sequence in past room $75.
/// </summary>
internal sealed class PreBlackTowerEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";

    public EventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> RalphUnlinked { get; }
    public IReadOnlyList<CutsceneCommand> RalphLinked { get; }
    public IReadOnlyList<CutsceneCommand> ImpaUnlinked { get; }
    public IReadOnlyList<CutsceneCommand> ImpaLinked { get; }
    public IReadOnlyList<CutsceneCommand> NayruUnlinked { get; }
    public IReadOnlyList<CutsceneCommand> NayruLinked { get; }
    public IReadOnlyList<CutsceneCommand> ZeldaLinked { get; }

    public PreBlackTowerEventDatabase()
    {
        string source = FileAccess.GetFileAsString(Root + "pre_black_tower_event.tsv");
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
            throw new InvalidOperationException("Pre-Black Tower event data is empty.");

        string[] columns = row.Split('\t');
        if (columns.Length != 23)
        {
            throw new InvalidOperationException(
                $"Pre-Black Tower event row should contain 23 columns, got {columns.Length}.");
        }
        Record = new EventRecord(
            int.Parse(columns[0]),
            Hex(columns[1]),
            Hex(columns[2]),
            Hex(columns[3]),
            Hex(columns[4]),
            Hex(columns[5]),
            Hex(columns[6]),
            Hex(columns[7]),
            Hex(columns[8]),
            Hex(columns[9]),
            Hex(columns[10]),
            Hex(columns[11]),
            Hex(columns[12]),
            Hex(columns[13]),
            Hex(columns[14]),
            Hex(columns[15]),
            Hex(columns[16]),
            Hex(columns[17]),
            Hex(columns[18]),
            columns[19],
            int.Parse(columns[20]),
            int.Parse(columns[21]),
            columns[22]);

        RalphUnlinked = Load("pre_black_tower_ralph_unlinked.tsv");
        RalphLinked = Load("pre_black_tower_ralph_linked.tsv");
        ImpaUnlinked = Load("pre_black_tower_impa_unlinked.tsv");
        ImpaLinked = Load("pre_black_tower_impa_linked.tsv");
        NayruUnlinked = Load("pre_black_tower_nayru_unlinked.tsv");
        NayruLinked = Load("pre_black_tower_nayru_linked.tsv");
        ZeldaLinked = Load("pre_black_tower_zelda_linked.tsv");
        Validate();
    }

    public NpcDatabase.NpcRecord CreateEffectRecord(int group, int room, int y, int x) =>
        new(
            group,
            room,
            Record.EffectId,
            Record.EffectSubId,
            y,
            x,
            0,
            0,
            Record.EffectSprite,
            Record.EffectTileBase,
            Record.EffectPalette,
            0,
            false,
            Record.EffectAnimation,
            Record.EffectAnimation,
            Record.EffectAnimation,
            Record.EffectAnimation,
            string.Empty);

    private static IReadOnlyList<CutsceneCommand> Load(string file) =>
        CutsceneCommandCatalog.Load(Root + file);

    private void Validate()
    {
        foreach (IReadOnlyList<CutsceneCommand> lane in new[]
        {
            RalphUnlinked, RalphLinked, ImpaUnlinked, ImpaLinked,
            NayruUnlinked, NayruLinked, ZeldaLinked
        })
        {
            if (lane.Count < 2 || lane[^1] is not CutsceneEndCommand)
            {
                throw new InvalidOperationException(
                    "Every pre-Black Tower actor lane must terminate in scriptend.");
            }
        }
        if (RalphUnlinked[1] is not CutsceneShowTextCommand { TextId: 0x2a19 } ||
            RalphLinked[0] is not CutsceneNativeCommand
                { Handler: "CreateLinkedExclamation" } ||
            ImpaUnlinked[0] is not CutsceneShowTextCommand { TextId: 0x0124 } ||
            ImpaLinked[^2] is not CutsceneMoveCommand { Actor: "Impa", Angle: 0x00 } ||
            NayruUnlinked.AnyText(0x1d13) is false ||
            NayruLinked.AnyText(0x1d12) is false ||
            ZeldaLinked.AnyText(0x0607) is false)
        {
            throw new InvalidOperationException(
                "Pre-Black Tower command lanes diverge from the imported actor scripts.");
        }
    }

    private static int Hex(string value) => Convert.ToInt32(value, 16);

    internal readonly record struct EventRecord(
        int Group,
        int Room,
        int MakuSeedTreasure,
        int CompletionFlag,
        int RalphEnteredFlag,
        int ClinkSound,
        int Gravity,
        int RalphId,
        int RalphSubId,
        int ImpaId,
        int ImpaUnlinkedSubId,
        int ImpaLinkedSubId,
        int NayruId,
        int NayruLinkedSubId,
        int NayruSpawnedSubId,
        int ZeldaId,
        int ZeldaSubId,
        int EffectId,
        int EffectSubId,
        string EffectSprite,
        int EffectTileBase,
        int EffectPalette,
        string EffectAnimation);
}

internal static class PreBlackTowerCommandExtensions
{
    public static bool AnyText(this IReadOnlyList<CutsceneCommand> commands, int textId)
    {
        foreach (CutsceneCommand command in commands)
        {
            if (command is CutsceneShowTextCommand text && text.TextId == textId)
                return true;
        }
        return false;
    }
}
