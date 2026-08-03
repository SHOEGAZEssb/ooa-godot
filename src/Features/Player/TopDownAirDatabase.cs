using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed runtime view of parentItemCode_feather and the top-down
/// linkUpdateInAir branch.
/// </summary>
internal sealed class TopDownAirDatabase
{
    private readonly Dictionary<string, int> _constants = new();

    internal static TopDownAirDatabase Shared { get; } = new();

    internal TopDownAirParameters Parameters => new(
        Gravity: Constant("gravity"),
        ReducedGravity: Constant("reduced-gravity"),
        MaximumFallSpeed: Constant("maximum-fall-speed"),
        JumpSpeedZ: Constant("jump-speed-z"),
        HoleStandingCounter: Constant("hole-standing-counter"),
        JumpSound: Constant("jump-sound"),
        LandSound: Constant("land-sound"),
        AnimationPhaseDurations:
        [
            Constant("animation-phase-0"),
            Constant("animation-phase-1"),
            Constant("animation-phase-2")
        ],
        CompanionJumpSpeedRaw: Constant("companion-jump-speed-raw"),
        CompanionJumpSpeedZ: Constant("companion-jump-speed-z"),
        CompanionDismountZ: Constant("companion-dismount-z"),
        CompanionDismountAngle: Constant("companion-dismount-angle"));

    private TopDownAirDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/top_down_air_constants.tsv",
            new GeneratedTableSchema(
                "top-down Link air constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value", "source"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));

        TopDownAirParameters parameters = Parameters;
        if (_constants.Count != 14 ||
            parameters.Gravity != 0x20 ||
            parameters.ReducedGravity != 0x0a ||
            parameters.MaximumFallSpeed != 0x0300 ||
            parameters.JumpSpeedZ != -0x01e0 ||
            parameters.HoleStandingCounter != 4 ||
            parameters.JumpSound != OracleSoundEngine.SndJump ||
            parameters.LandSound != OracleSoundEngine.SndLand ||
            parameters.CompanionJumpSpeedRaw != 0x14 ||
            parameters.CompanionJumpSpeedZ != -0x01c0 ||
            parameters.CompanionDismountZ != -8 ||
            parameters.CompanionDismountAngle != 0xff ||
            !parameters.AnimationPhaseDurations.AsSpan().SequenceEqual(
                [9, 9, 6]))
        {
            throw new InvalidOperationException(
                "Imported Ages top-down Link air data is incomplete.");
        }
    }

    private int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Top-down Link air constant '{key}' was not imported.");
}

internal readonly record struct TopDownAirParameters(
    int Gravity,
    int ReducedGravity,
    int MaximumFallSpeed,
    int JumpSpeedZ,
    int HoleStandingCounter,
    int JumpSound,
    int LandSound,
    int[] AnimationPhaseDurations,
    int CompanionJumpSpeedRaw,
    int CompanionJumpSpeedZ,
    int CompanionDismountZ,
    int CompanionDismountAngle);
