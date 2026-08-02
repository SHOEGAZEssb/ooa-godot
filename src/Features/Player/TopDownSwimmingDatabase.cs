using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed runtime view of the non-side-view linkUpdateSwimming Flippers path,
/// normal-water linkUpdateDiving, and their animation data.
/// </summary>
internal sealed class TopDownSwimmingDatabase
{
    private readonly Dictionary<string, int> _constants = new();
    private readonly Dictionary<(int Frame, int Direction),
        TopDownSwimmingFrame> _frames = new();
    private readonly Dictionary<int, TopDownDivingFrame> _diveFrames = new();

    internal static TopDownSwimmingDatabase Shared { get; } = new();

    internal TopDownSwimmingParameters Parameters => new(
        BaseSpeed: Constant("base-speed"),
        FastSpeed: Constant("fast-speed"),
        EntryUpdates: Constant("entry-updates"),
        VelocityInterval: Constant("velocity-interval"),
        BurstTurnUpdates: Constant("burst-turn-updates"),
        BurstAccelerateUpdates: Constant("burst-accelerate-updates"),
        BurstDecelerateUpdates: Constant("burst-decelerate-updates"),
        BurstSpeedStep: Constant("burst-speed-step"),
        SwimSound: Constant("swim-sound"),
        AnimationFrameDurations:
        [
            Constant("animation-frame-0"),
            Constant("animation-frame-1")
        ],
        DiveUpdates: Constant("dive-updates"),
        DiveAnimationFrameDurations:
        [
            Constant("dive-animation-frame-0"),
            Constant("dive-animation-frame-1")
        ]);

    private TopDownSwimmingDatabase()
    {
        GeneratedTable constantTable = GeneratedTable.Load(
            "res://assets/oracle/metadata/top_down_swim_constants.tsv",
            new GeneratedTableSchema(
                "top-down Link swimming constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value", "source"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in constantTable.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));

        GeneratedTable frameTable = GeneratedTable.Load(
            "res://assets/oracle/metadata/top_down_swim_frames.tsv",
            new GeneratedTableSchema(
                "top-down Link swimming frames",
                GeneratedTableKeySemantics.Unique,
                [
                    "frame", "direction", "duration", "sprite",
                    "source-offset", "oam-index", "oam", "source"
                ],
                ["frame", "direction"],
                headerRequired: true));
        foreach (GeneratedTableRow row in frameTable.Rows)
        {
            int frame = row.Decimal(0, 0, 1);
            int direction = row.Decimal(1, 0, 3);
            _frames.Add(
                (frame, direction),
                new TopDownSwimmingFrame(
                    Frame: frame,
                    Direction: direction,
                    Duration: row.Decimal(2, 1, 255),
                    Sprite: row.RequiredString(3),
                    SourceOffset: row.HexWord(4),
                    OamIndex: row.HexByte(5),
                    Oam: row.RequiredString(6)));
        }

        GeneratedTable diveFrameTable = GeneratedTable.Load(
            "res://assets/oracle/metadata/top_down_dive_frames.tsv",
            new GeneratedTableSchema(
                "top-down Link diving frames",
                GeneratedTableKeySemantics.Unique,
                [
                    "frame", "duration", "sprite", "source-offset",
                    "oam-index", "oam", "source"
                ],
                ["frame"],
                headerRequired: true));
        foreach (GeneratedTableRow row in diveFrameTable.Rows)
        {
            int frame = row.Decimal(0, 0, 1);
            _diveFrames.Add(
                frame,
                new TopDownDivingFrame(
                    Frame: frame,
                    Duration: row.Decimal(1, 1, 255),
                    Sprite: row.RequiredString(2),
                    SourceOffset: row.HexWord(3),
                    OamIndex: row.HexByte(4),
                    Oam: row.RequiredString(5)));
        }

        Validate();
    }

    internal TopDownSwimmingFrame Frame(int frame, int direction) =>
        _frames.TryGetValue((frame, direction), out TopDownSwimmingFrame value)
            ? value
            : throw new KeyNotFoundException(
                $"Top-down Link swim frame {frame}, direction {direction} was not imported.");

    internal TopDownDivingFrame DiveFrame(int frame) =>
        _diveFrames.TryGetValue(frame, out TopDownDivingFrame value)
            ? value
            : throw new KeyNotFoundException(
                $"Top-down Link dive frame {frame} was not imported.");

    private int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Top-down Link swimming constant '{key}' was not imported.");

    private void Validate()
    {
        TopDownSwimmingParameters parameters = Parameters;
        if (_constants.Count != 14 || _frames.Count != 8 ||
            _diveFrames.Count != 2 ||
            parameters.BaseSpeed != 0x14 ||
            parameters.FastSpeed != 0x23 ||
            parameters.EntryUpdates != 0x0a ||
            parameters.VelocityInterval != 0x03 ||
            parameters.BurstTurnUpdates != 0x08 ||
            parameters.BurstAccelerateUpdates != 0x0d ||
            parameters.BurstDecelerateUpdates != 0x0c ||
            parameters.BurstSpeedStep != 0x05 ||
            parameters.SwimSound != OracleSoundEngine.SndLinkSwim ||
            !parameters.AnimationFrameDurations.AsSpan().SequenceEqual([6, 6]) ||
            parameters.DiveUpdates != 0x78 ||
            !parameters.DiveAnimationFrameDurations.AsSpan()
                .SequenceEqual([0x10, 0x10]))
        {
            throw new InvalidOperationException(
                "Imported Ages top-down Link swimming data is incomplete.");
        }

        int[,] offsets =
        {
            { 0x0e00, 0x0ec0, 0x0e80, 0x0ec0 },
            { 0x0e40, 0x0f00, 0x0ea0, 0x0f00 }
        };
        int[,] oamIndices =
        {
            { 0x10, 0x11, 0x12, 0x10 },
            { 0x10, 0x11, 0x12, 0x10 }
        };
        string[] oam =
        [
            "12,0,0,0;12,8,2,0",
            "12,0,2,32;12,8,0,32",
            "12,0,0,0;12,8,0,32"
        ];
        for (int frame = 0; frame < 2; frame++)
        for (int direction = 0; direction < 4; direction++)
        {
            TopDownSwimmingFrame record = Frame(frame, direction);
            if (record.Duration != 6 || record.Sprite != "spr_link" ||
                record.SourceOffset != offsets[frame, direction] ||
                record.OamIndex != oamIndices[frame, direction] ||
                record.Oam != oam[record.OamIndex - 0x10])
            {
                throw new InvalidOperationException(
                    "Imported Ages LINK_ANIM_MODE_SWIM graphics are inconsistent.");
            }
        }

        int[] diveOffsets = [0x0f40, 0x0f60];
        for (int frame = 0; frame < 2; frame++)
        {
            TopDownDivingFrame record = DiveFrame(frame);
            if (record.Duration != 0x10 || record.Sprite != "spr_link" ||
                record.SourceOffset != diveOffsets[frame] ||
                record.OamIndex != 0x12 || record.Oam != oam[2])
            {
                throw new InvalidOperationException(
                    "Imported Ages LINK_ANIM_MODE_DIVE graphics are inconsistent.");
            }
        }
    }
}

internal readonly record struct TopDownSwimmingParameters(
    int BaseSpeed,
    int FastSpeed,
    int EntryUpdates,
    int VelocityInterval,
    int BurstTurnUpdates,
    int BurstAccelerateUpdates,
    int BurstDecelerateUpdates,
    int BurstSpeedStep,
    int SwimSound,
    int[] AnimationFrameDurations,
    int DiveUpdates,
    int[] DiveAnimationFrameDurations);

internal readonly record struct TopDownSwimmingFrame(
    int Frame,
    int Direction,
    int Duration,
    string Sprite,
    int SourceOffset,
    int OamIndex,
    string Oam);

internal readonly record struct TopDownDivingFrame(
    int Frame,
    int Duration,
    string Sprite,
    int SourceOffset,
    int OamIndex,
    string Oam);
