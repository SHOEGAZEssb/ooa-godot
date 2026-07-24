using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed runtime view of tileTypesTable@sidescrolling,
/// sidescrollUpdateActiveTile, linkUpdateInAir_sidescroll, and the
/// side-view branch of ITEM_FEATHER.
/// </summary>
internal sealed class SideScrollPlayerDatabase
{
    private readonly Dictionary<byte, SideScrollTileType> _tiles = new();
    private readonly Dictionary<string, int> _constants = new();

    internal SideScrollPlayerParameters Parameters => new(
        Gravity: Constant("gravity"),
        ReducedGravity: Constant("reduced-gravity"),
        MaximumFallSpeed: Constant("maximum-fall-speed"),
        JumpSpeedZ: Constant("jump-speed-z"),
        GroundWallMask: Constant("ground-wall-mask"),
        CeilingWallMask: Constant("ceiling-wall-mask"),
        LandingHighMask: Constant("landing-high-mask"),
        LandingHighOffset: Constant("landing-high-offset"),
        BelowTileOffset: Constant("below-tile-offset"),
        BottomBoundary: Constant("bottom-boundary"),
        SpikeTile: Constant("spike-tile"),
        JumpSound: Constant("jump-sound"),
        LandSound: Constant("land-sound"),
        AnimationPhaseDurations:
        [
            Constant("animation-phase-0"),
            Constant("animation-phase-1"),
            Constant("animation-phase-2")
        ]);

    internal SideScrollPlayerDatabase()
    {
        GeneratedTable tileTable = GeneratedTable.Load(
            "res://assets/oracle/metadata/side_scroll_tiles.tsv",
            new GeneratedTableSchema(
                "side-scrolling tile types",
                GeneratedTableKeySemantics.Unique,
                ["tile", "flags", "source"],
                ["tile"],
                headerRequired: true));
        foreach (GeneratedTableRow row in tileTable.Rows)
        {
            byte tile = (byte)row.HexByte(0);
            _tiles.Add(tile, (SideScrollTileType)row.HexByte(1));
        }

        GeneratedTable constantTable = GeneratedTable.Load(
            "res://assets/oracle/metadata/side_scroll_constants.tsv",
            new GeneratedTableSchema(
                "side-scrolling player constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value", "source"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in constantTable.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));

        Validate();
    }

    internal SideScrollTileType TileType(byte tile) =>
        _tiles.TryGetValue(tile, out SideScrollTileType type)
            ? type
            : SideScrollTileType.None;

    private int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Side-scrolling player constant '{key}' was not imported.");

    private void Validate()
    {
        SideScrollPlayerParameters parameters = Parameters;
        if (_tiles.Count != 16 ||
            TileType(0x16) != SideScrollTileType.Ladder ||
            TileType(0x18) != SideScrollTileType.Ladder ||
            TileType(0x17) !=
                (SideScrollTileType.Ladder | SideScrollTileType.LadderTop) ||
            TileType(0x19) !=
                (SideScrollTileType.Ladder | SideScrollTileType.LadderTop) ||
            TileType(0x1a) !=
                (SideScrollTileType.Ladder | SideScrollTileType.Water) ||
            TileType(0x1f) != SideScrollTileType.Water ||
            TileType(0x20) != SideScrollTileType.Ice ||
            TileType(0xf4) != SideScrollTileType.Hole ||
            TileType(0x02) != SideScrollTileType.None ||
            parameters.Gravity != 0x24 ||
            parameters.ReducedGravity != 0x0e ||
            parameters.MaximumFallSpeed != 0x0300 ||
            parameters.JumpSpeedZ != -0x0230 ||
            parameters.GroundWallMask != 0x30 ||
            parameters.CeilingWallMask != 0xc0 ||
            parameters.LandingHighMask != 0xf8 ||
            parameters.LandingHighOffset != 0x01 ||
            parameters.BelowTileOffset != 8 ||
            parameters.BottomBoundary != 0xa9 ||
            parameters.SpikeTile != 0x02 ||
            parameters.JumpSound != OracleSoundEngine.SndJump ||
            parameters.LandSound != OracleSoundEngine.SndLand ||
            !parameters.AnimationPhaseDurations.AsSpan().SequenceEqual(
                [9, 9, 6]))
        {
            throw new InvalidOperationException(
                "Imported Ages side-scrolling player data is incomplete or inconsistent.");
        }
    }
}

[Flags]
public enum SideScrollTileType : byte
{
    None = 0x00,
    Hole = 0x01,
    Lava = 0x04,
    Ladder = 0x10,
    Water = 0x20,
    Ice = 0x40,
    LadderTop = 0x80
}

public readonly record struct SideScrollPlayerParameters(
    int Gravity,
    int ReducedGravity,
    int MaximumFallSpeed,
    int JumpSpeedZ,
    int GroundWallMask,
    int CeilingWallMask,
    int LandingHighMask,
    int LandingHighOffset,
    int BelowTileOffset,
    int BottomBoundary,
    int SpikeTile,
    int JumpSound,
    int LandSound,
    int[] AnimationPhaseDurations);

public readonly record struct SideScrollTerrainState(
    byte ActiveTile,
    byte BelowTile,
    SideScrollTileType ActiveType,
    SideScrollTileType BelowType)
{
    internal SideScrollTileType CombinedType => ActiveType | BelowType;
}
