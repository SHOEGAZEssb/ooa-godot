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
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "pre_black_tower_event.tsv",
            new GeneratedTableSchema(
                "pre-Black Tower event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "maku-seed", "completion-flag", "ralph-entered-flag",
                    "clink-sound", "gravity", "ralph-id", "ralph-subid", "impa-id",
                    "impa-unlinked-subid", "impa-linked-subid", "nayru-id", "nayru-linked-subid",
                    "nayru-spawned-subid", "zelda-id", "zelda-subid", "effect-id", "effect-subid",
                    "effect-sprite", "effect-tile-base", "effect-palette", "effect-animation"
                ],
                headerRequired: true)).SingleRow();
        Record = new EventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.HexByte(7),
            row.HexByte(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.HexByte(13),
            row.HexByte(14),
            row.HexByte(15),
            row.HexByte(16),
            row.HexByte(17),
            row.HexByte(18),
            row.RequiredString(19),
            row.UnsignedDecimal(20),
            row.UnsignedDecimal(21),
            row.RequiredString(22));

        RalphUnlinked = Load("pre_black_tower_ralph_unlinked.tsv");
        RalphLinked = Load("pre_black_tower_ralph_linked.tsv");
        ImpaUnlinked = Load("pre_black_tower_impa_unlinked.tsv");
        ImpaLinked = Load("pre_black_tower_impa_linked.tsv");
        NayruUnlinked = Load("pre_black_tower_nayru_unlinked.tsv");
        NayruLinked = Load("pre_black_tower_nayru_linked.tsv");
        ZeldaLinked = Load("pre_black_tower_zelda_linked.tsv");
        Validate();
    }

    public NpcRecord CreateEffectRecord(int group, int room, int y, int x) =>
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
}

internal readonly record struct EventRecord(int Group, int Room, int MakuSeedTreasure, int CompletionFlag, int RalphEnteredFlag, int ClinkSound, int Gravity, int RalphId, int RalphSubId, int ImpaId, int ImpaUnlinkedSubId, int ImpaLinkedSubId, int NayruId, int NayruLinkedSubId, int NayruSpawnedSubId, int ZeldaId, int ZeldaSubId, int EffectId, int EffectSubId, string EffectSprite, int EffectTileBase, int EffectPalette, string EffectAnimation);
