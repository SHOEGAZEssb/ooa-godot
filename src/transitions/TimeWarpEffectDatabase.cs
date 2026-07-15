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
    public readonly record struct ParticleRecord(int SpeedFixed, int XOffset, int SubId);

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
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/timeWarpEffects.tsv");
        string? row = null;
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
            {
                if (row is not null)
                    throw new InvalidOperationException("Time-warp effect data contains multiple rows.");
                row = line;
            }
        }
        if (row is null)
            throw new InvalidOperationException("Time-warp effect data is empty.");

        string[] columns = row.Split('\t');
        if (columns.Length != 31)
        {
            throw new InvalidOperationException(
                $"Time-warp effect row should contain 31 columns, got {columns.Length}.");
        }

        TimeWarpSprite = columns[0];
        CommonSprite = columns[1];
        SparkleSprite = columns[2];
        PrimaryTileBase = int.Parse(columns[3]);
        PrimaryPalette = int.Parse(columns[4]);
        BeamPalette = int.Parse(columns[5]);
        TrailTileBase = int.Parse(columns[6]);
        TrailPalette = int.Parse(columns[7]);
        ParticleTileBase = int.Parse(columns[8]);
        ParticlePalette = int.Parse(columns[9]);
        SparkleTileBase = int.Parse(columns[10]);
        SparklePalette = int.Parse(columns[11]);
        PrimaryPriority = int.Parse(columns[12]);
        BeamPriority = int.Parse(columns[13]);
        TrailPriority = int.Parse(columns[14]);
        ParticlePriority = int.Parse(columns[15]);
        SparklePriority = int.Parse(columns[16]);
        DissolveFrames = int.Parse(columns[17]);
        SourceEffectFrames = int.Parse(columns[18]);
        SourceTrailFrames = int.Parse(columns[19]);
        ArrivalWaitFrames = int.Parse(columns[20]);
        ArrivalEffectFrames = int.Parse(columns[21]);
        ArrivalFlickerFrames = int.Parse(columns[22]);
        ExpandAnimation = columns[23];
        ContractAnimation = columns[24];
        BeamIntroAnimation = columns[25];
        BeamLoopAnimation = columns[26];
        BeamContractAnimation = columns[27];
        TrailAnimation = columns[28];
        SparkleAnimation = columns[29];

        var particles = new List<ParticleRecord>();
        foreach (string encoded in columns[30].Split('|', StringSplitOptions.RemoveEmptyEntries))
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
