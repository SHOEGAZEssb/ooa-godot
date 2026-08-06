using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_RAFTWRECK_CUTSCENE $9b:$00, its $64 helper streams,
/// and the source visuals used in past ocean room $1:$a8.
/// </summary>
internal sealed class RaftwreckEventDatabase
{
    private readonly Dictionary<int, IReadOnlyList<RaftwreckHelperRecord>> _helpers;
    private readonly Dictionary<int, RaftwreckEffectRecord> _effects;

    internal RaftwreckEventRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }
    internal RaftwreckEffectRecord Lightning { get; }
    internal RaftwreckEffectRecord Debris => Effect(8);

    internal RaftwreckEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/raftwreck_event.tsv",
            new GeneratedTableSchema(
                "raftwreck event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "interaction-id", "subid", "room-flag",
                    "initial-y", "center-x", "initial-speed",
                    "first-flash-wait", "second-flash-wait", "finish-wait",
                    "destination-room", "destination-position",
                    "destination-parameter", "destination-transition",
                    "y-oscillation", "angle-preset", "lightning-z",
                    "lightning-frames", "lightning-shake", "debris-offsets",
                    "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new RaftwreckEventRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2),
            row.HexByte(3), (byte)row.HexByte(4), row.HexByte(5),
            row.HexByte(6), row.HexByte(7), row.HexByte(8), row.HexByte(9),
            row.HexByte(10), row.HexByte(11), row.HexByte(12),
            row.HexByte(13), row.HexByte(14), ParseHexBytes(row, 15),
            ParsePreset(row, 16), ParseHexBytes(row, 17),
            ParsePreset(row, 18), row.HexByte(19), ParsePreset(row, 20),
            row.RequiredString(21));

        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/raftwreck_commands.tsv");

        GeneratedTable helperTable = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/raftwreck_helpers.tsv",
            new GeneratedTableSchema(
                "raftwreck helper streams",
                GeneratedTableKeySemantics.Grouped,
                ["helper-subid", "index", "y", "x", "effect-subid", "counter"],
                ["helper-subid"],
                headerRequired: true));
        _helpers = helperTable.Rows
            .Select(helper => new RaftwreckHelperRecord(
                helper.UnsignedDecimal(0), helper.UnsignedDecimal(1),
                helper.HexByte(2), helper.HexByte(3), helper.HexByte(4),
                helper.HexByte(5)))
            .GroupBy(helper => helper.HelperSubId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RaftwreckHelperRecord>)group
                    .OrderBy(helper => helper.Index).ToArray());

        GeneratedTable effectTable = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/raftwreck_effects.tsv",
            new GeneratedTableSchema(
                "raftwreck wind effects",
                GeneratedTableKeySemantics.Unique,
                ["subid", "sprite", "tile-base", "palette", "animation", "duration"],
                ["subid"],
                headerRequired: true));
        _effects = effectTable.Rows
            .Select(effect => new RaftwreckEffectRecord(
                effect.UnsignedDecimal(0), effect.RequiredString(1),
                effect.UnsignedDecimal(2), effect.UnsignedDecimal(3),
                effect.RequiredString(4), effect.UnsignedDecimal(5)))
            .ToDictionary(effect => effect.SubId);

        GeneratedTableRow lightning = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/nayru_intro_effects.tsv",
            new GeneratedTableSchema(
                "PART_LIGHTNING visuals",
                GeneratedTableKeySemantics.Unique,
                [
                    "name", "sprite", "tile-base", "palette", "duration",
                    "speed", "angle", "sway", "velocity-x-fixed",
                    "velocity-y-fixed", "animation"
                ],
                ["name"],
                headerRequired: true)).Rows.Single(row =>
                    row.RequiredString(0) == "Lightning");
        Lightning = new RaftwreckEffectRecord(
            1, lightning.RequiredString(1), lightning.UnsignedDecimal(2),
            lightning.UnsignedDecimal(3), lightning.RequiredString(10),
            lightning.UnsignedDecimal(4));
        Validate();
    }

    internal IReadOnlyList<RaftwreckHelperRecord> Helper(int subId) =>
        _helpers.TryGetValue(subId, out IReadOnlyList<RaftwreckHelperRecord>? rows)
            ? rows
            : throw new InvalidOperationException(
                $"Missing INTERAC_RAFTWRECK_CUTSCENE_HELPER $64:${subId:x2} data.");

    internal RaftwreckEffectRecord Effect(int subId) =>
        _effects.TryGetValue(subId, out RaftwreckEffectRecord effect)
            ? effect
            : throw new InvalidOperationException(
                $"Missing raftwreck wind effect $64:${subId:x2}.");

    private void Validate()
    {
        if (Record is not
            {
                Group: 1, Room: 0xa8, InteractionId: 0x9b, SubId: 0,
                RoomFlag: 0x40, InitialY: 0x76, CenterX: 0x50,
                InitialSpeed: 0x14, FirstFlashWait: 0x78,
                SecondFlashWait: 0x78, FinishWait: 0x14,
                DestinationRoom: 0xaa, DestinationPosition: 0x42,
                DestinationParameter: 0, DestinationTransition: 3
            } || Record.YOscillation is not [0xff, 0xfe, 0xff, 0, 1, 2, 1, 0] ||
            Record.AnglePreset is not
                [(0x15, 0x0c), (0x16, 0x0c), (0x17, 0x12),
                 (0x18, 0x14), (0x19, 0x14), (0x1a, 0x20)] ||
            Record.LightningZ is not [0xc0, 0xd0, 0xe0, 0xf0, 0] ||
            Record.LightningFrames is not
                [(1, 0), (1, 0x10), (1, 0x20), (1, 0x30),
                 (2, 0x40), (2, 0x40), (4, 0xc3), (4, 0xc4),
                 (4, 0x46), (0x7f, 0xff)] ||
            Record.LightningShake != 6 ||
            Record.DebrisOffsets is not
                [(2, 6), (0, 0xfb), (0xff, 7), (0xfd, 0xfc), (0, 5)] ||
            Commands.Count != 44 || _effects.Count != 4 ||
            Helper(3).Count != 5 || Helper(4).Count != 16 ||
            Helper(5).Count != 3 || Lightning.Duration != 20 ||
            Debris.Duration != 20)
        {
            throw new InvalidOperationException(
                "Room 1:a8 raftwreck import diverges from the source contract.");
        }
    }

    private static int[] ParseHexBytes(GeneratedTableRow row, int column) =>
        row.RequiredString(column).Split(',').Select(value =>
            Convert.ToInt32(value, 16)).ToArray();

    private static (int Angle, int Counter)[] ParsePreset(
        GeneratedTableRow row,
        int column) =>
        row.RequiredString(column).Split(',').Select(value =>
        {
            string[] pair = value.Split(':');
            if (pair.Length != 2)
                throw row.Invalid(column, "angle:counter hexadecimal pairs");
            return (Convert.ToInt32(pair[0], 16), Convert.ToInt32(pair[1], 16));
        }).ToArray();
}

internal readonly record struct RaftwreckEventRecord(
    int Group, int Room, int InteractionId, int SubId, byte RoomFlag,
    int InitialY, int CenterX, int InitialSpeed, int FirstFlashWait,
    int SecondFlashWait, int FinishWait, int DestinationRoom,
    int DestinationPosition, int DestinationParameter,
    int DestinationTransition, int[] YOscillation,
    (int Angle, int Counter)[] AnglePreset, int[] LightningZ,
    (int Duration, int Parameter)[] LightningFrames,
    int LightningShake, (int Y, int X)[] DebrisOffsets, string Source);

internal readonly record struct RaftwreckHelperRecord(
    int HelperSubId, int Index, int Y, int X, int EffectSubId, int Counter);

internal readonly record struct RaftwreckEffectRecord(
    int SubId, string Sprite, int TileBase, int Palette, string Animation,
    int Duration = 0);
