using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_TIMEWARP ($dd), PART_TIMEWARP_ANIMATION ($2b), and
/// INTERAC_SPARKLE ($84:$01) records used by CUTSCENE_TIMEWARP.
/// </summary>
public sealed class TimeWarpEffectDatabase
{

    public string TimeWarpSprite { get; }
    public string CommonSprite { get; }
    public string SparkleSprite { get; }
    public int PrimaryTileBase { get; }
    public int PrimaryPalette { get; }
    public int BeamPalette { get; }
    public int TrailTileBase { get; }
    public int TrailPalette { get; }
    public int ParticleTileBase { get; }
    public int ParticlePalette { get; }
    public int SparkleTileBase { get; }
    public int SparklePalette { get; }
    public int PrimaryPriority { get; }
    public int BeamPriority { get; }
    public int TrailPriority { get; }
    public int ParticlePriority { get; }
    public int SparklePriority { get; }
    public int DissolveFrames { get; }
    public int SourceEffectFrames { get; }
    public int SourceTrailFrames { get; }
    public int ArrivalWaitFrames { get; }
    public int ArrivalEffectFrames { get; }
    public int ArrivalFlickerFrames { get; }
    public string ExpandAnimation { get; }
    public string ContractAnimation { get; }
    public string BeamIntroAnimation { get; }
    public string BeamLoopAnimation { get; }
    public string BeamContractAnimation { get; }
    public string TrailAnimation { get; }
    public string SparkleAnimation { get; }
    public IReadOnlyList<ParticleRecord> Particles { get; }
    public Color[] OutdoorBeamPalette { get; }
    public Color[] IndoorBeamPalette { get; }

    public TimeWarpEffectDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/timeWarpEffects.tsv",
            new GeneratedTableSchema(
                "time-warp effects",
                GeneratedTableKeySemantics.Ordered,
                [
                    "timewarp-sprite", "common-sprite", "sparkle-sprite",
                    "primary-tile-base", "primary-palette", "beam-palette",
                    "trail-tile-base", "trail-palette", "particle-tile-base",
                    "particle-palette", "sparkle-tile-base", "sparkle-palette",
                    "primary-priority", "beam-priority", "trail-priority",
                    "particle-priority", "sparkle-priority", "dissolve-frames",
                    "source-effect-frames", "source-trail-frames", "arrival-wait-frames",
                    "arrival-effect-frames", "arrival-flicker-frames", "expand-animation",
                    "contract-animation", "beam-intro-animation", "beam-loop-animation",
                    "beam-contract-animation", "trail-animation", "sparkle-animation",
                    "particles"
                ],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Time-warp effect data should contain one row, got {table.Rows.Count}.");
        }
        GeneratedTableRow row = table.Rows[0];

        TimeWarpSprite = row.RequiredString(0);
        CommonSprite = row.RequiredString(1);
        SparkleSprite = row.RequiredString(2);
        PrimaryTileBase = row.UnsignedDecimal(3);
        PrimaryPalette = row.UnsignedDecimal(4);
        BeamPalette = row.UnsignedDecimal(5);
        TrailTileBase = row.UnsignedDecimal(6);
        TrailPalette = row.UnsignedDecimal(7);
        ParticleTileBase = row.UnsignedDecimal(8);
        ParticlePalette = row.UnsignedDecimal(9);
        SparkleTileBase = row.UnsignedDecimal(10);
        SparklePalette = row.UnsignedDecimal(11);
        PrimaryPriority = row.UnsignedDecimal(12);
        BeamPriority = row.UnsignedDecimal(13);
        TrailPriority = row.UnsignedDecimal(14);
        ParticlePriority = row.UnsignedDecimal(15);
        SparklePriority = row.UnsignedDecimal(16);
        DissolveFrames = row.UnsignedDecimal(17);
        SourceEffectFrames = row.UnsignedDecimal(18);
        SourceTrailFrames = row.UnsignedDecimal(19);
        ArrivalWaitFrames = row.UnsignedDecimal(20);
        ArrivalEffectFrames = row.UnsignedDecimal(21);
        ArrivalFlickerFrames = row.UnsignedDecimal(22);
        ExpandAnimation = row.RequiredString(23);
        ContractAnimation = row.RequiredString(24);
        BeamIntroAnimation = row.RequiredString(25);
        BeamLoopAnimation = row.RequiredString(26);
        BeamContractAnimation = row.RequiredString(27);
        TrailAnimation = row.RequiredString(28);
        SparkleAnimation = row.RequiredString(29);

        var particles = new List<ParticleRecord>();
        foreach (string encoded in row.RequiredString(30).Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = encoded.Split(',');
            if (fields.Length != 3)
                throw new InvalidOperationException($"Malformed time-warp particle record: {encoded}");
            particles.Add(new ParticleRecord(
                int.Parse(fields[0]), int.Parse(fields[1]), int.Parse(fields[2])));
        }
        if (particles.Count != 8)
            throw new InvalidOperationException($"Expected 8 time-warp particle records, got {particles.Count}.");
        Particles = particles;

        byte[] paletteBytes = FileAccess.GetFileAsBytes(
            "res://assets/oracle/metadata/time_warp_palettes.bin");
        if (paletteBytes.Length != 24)
        {
            throw new InvalidOperationException(
                $"Time-warp palettes should contain 24 bytes, got {paletteBytes.Length}.");
        }
        OutdoorBeamPalette = ReadPalette(paletteBytes, 0);
        IndoorBeamPalette = ReadPalette(paletteBytes, 12);
    }

    private static Color[] ReadPalette(byte[] bytes, int offset)
    {
        var palette = new Color[4];
        for (int color = 0; color < palette.Length; color++)
        {
            int source = offset + color * 3;
            palette[color] = new Color(
                bytes[source] / 31.0f,
                bytes[source + 1] / 31.0f,
                bytes[source + 2] / 31.0f);
        }
        return palette;
    }
}
