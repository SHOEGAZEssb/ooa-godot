
namespace Oracles;

/// <summary>
/// Typed runtime view of tileTypesTable@sidescrolling,
/// sidescrollUpdateActiveTile, linkUpdateInAir_sidescroll, and the
/// side-view branches of ITEM_FEATHER and linkUpdateSwimming.
/// </summary>
internal sealed class SideScrollPlayerDatabase
{
    public string AssetRoot { get; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<SideScrollPlayerDatabase>> Packs = new(StringComparer.Ordinal);
    internal static SideScrollPlayerDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<SideScrollPlayerDatabase>(() => new SideScrollPlayerDatabase(root))).Value;

    private readonly TilePropertyDatabase _tiles;
    private readonly Dictionary<string, int> _constants = new();
    private readonly Dictionary<(bool MermaidSuit, int Frame, int Direction),
        SideScrollSwimmingFrame> _swimmingFrames = new();

    internal SideScrollPlayerParameters Parameters => new(
        Gravity: Constant("gravity"),
        ReducedGravity: Constant("reduced-gravity"),
        MaximumFallSpeed: Constant("maximum-fall-speed"),
        JumpSpeedZ: Constant("jump-speed-z"),
        WaterExitSpeedZ: Constant("water-exit-speed-z"),
        NormalSpeed: Constant("normal-speed"),
        PlatformPushSpeed: Constant("platform-push-speed"),
        KnockbackSpeed: Constant("knockback-speed"),
        IceVelocityInterval: Constant("ice-velocity-interval"),
        SwimSpeed: Constant("swim-speed"),
        FastSwimSpeed: Constant("fast-swim-speed"),
        GroundWallMask: Constant("ground-wall-mask"),
        CeilingWallMask: Constant("ceiling-wall-mask"),
        LandingHighMask: Constant("landing-high-mask"),
        LandingHighOffset: Constant("landing-high-offset"),
        BelowTileOffset: Constant("below-tile-offset"),
        BottomBoundary: Constant("bottom-boundary"),
        BottomLadderTransition: Constant("bottom-ladder-transition"),
        SpikeTile: Constant("spike-tile"),
        JumpSound: Constant("jump-sound"),
        LandSound: Constant("land-sound"),
        AnimationPhaseDurations:
        [
            Constant("animation-phase-0"),
            Constant("animation-phase-1"),
            Constant("animation-phase-2")
        ]);

    internal SideScrollPlayerDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        _tiles = TilePropertyDatabase.For(assetRoot);

        GeneratedTable constantTable = GeneratedTable.Load(
            AssetRoot + "metadata/side_scroll_constants.tsv",
            new GeneratedTableSchema(
                "side-scrolling player constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value", "source"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in constantTable.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));

        GeneratedTable swimmingFrameTable = GeneratedTable.Load(
            AssetRoot + "metadata/side_scroll_swim_frames.tsv",
            new GeneratedTableSchema(
                "side-view Link swimming frames",
                GeneratedTableKeySemantics.Unique,
                [
                    "mode", "frame", "direction", "duration", "sprite",
                    "source-offset", "oam-index", "oam", "source"
                ],
                ["mode", "frame", "direction"],
                headerRequired: true));
        foreach (GeneratedTableRow row in swimmingFrameTable.Rows)
        {
            string mode = row.RequiredString(0);
            bool mermaidSuit = mode switch
            {
                "flippers" => false,
                "mermaid" => true,
                _ => throw new InvalidOperationException(
                    $"Unknown side-view swimming animation mode '{mode}'.")
            };
            int frame = row.Decimal(1, 0, 1);
            int direction = row.Decimal(2, 0, 3);
            _swimmingFrames.Add(
                (mermaidSuit, frame, direction),
                new SideScrollSwimmingFrame(
                    MermaidSuit: mermaidSuit,
                    Frame: frame,
                    Direction: direction,
                    Duration: row.Decimal(3, 1, 255),
                    Sprite: row.RequiredString(4),
                    SourceOffset: row.HexWord(5),
                    OamIndex: row.HexByte(6),
                    Oam: row.RequiredString(7)));
        }

        Validate();
    }

    internal SideScrollTileType TileType(byte tile) => _tiles.SideScrollType(tile);

    internal SideScrollSwimmingFrame SwimmingFrame(
        bool mermaidSuit,
        int frame,
        int direction) =>
        _swimmingFrames.TryGetValue(
            (mermaidSuit, frame, direction),
            out SideScrollSwimmingFrame value)
                ? value
                : throw new KeyNotFoundException(
                    $"{AssetRoot}: side-view Link swim frame {frame}, direction " +
                    $"{direction}, mermaid={mermaidSuit} was not imported.");

    private int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"{AssetRoot}: side-scrolling player constant '{key}' was not imported.");

    private void Validate()
    {
        SideScrollPlayerParameters parameters = Parameters;
        bool ages = OracleAssetPack.GameFor(AssetRoot) == OracleGame.Ages;
        if (_constants.Count != 24 || _swimmingFrames.Count != (ages ? 16 : 8) ||
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
            parameters.WaterExitSpeedZ != -0x01a0 ||
            parameters.NormalSpeed != 0x28 ||
            parameters.PlatformPushSpeed != 0x14 ||
            parameters.KnockbackSpeed != 0x32 ||
            parameters.IceVelocityInterval != 0x06 ||
            parameters.SwimSpeed != 0x14 ||
            parameters.FastSwimSpeed != 0x23 ||
            parameters.GroundWallMask != 0x30 ||
            parameters.CeilingWallMask != 0xc0 ||
            parameters.LandingHighMask != 0xf8 ||
            parameters.LandingHighOffset != 0x01 ||
            parameters.BelowTileOffset != 8 ||
            parameters.BottomBoundary != 0xa9 ||
            parameters.BottomLadderTransition != (ages ? 0 : 0x82) ||
            parameters.SpikeTile != 0x02 ||
            parameters.JumpSound != SoundId.SndJump ||
            parameters.LandSound != SoundId.SndLand ||
            !parameters.AnimationPhaseDurations.AsSpan().SequenceEqual(
                [9, 9, 6]))
        {
            throw new InvalidOperationException(
                $"{AssetRoot}: native side-scrolling player data is incomplete or inconsistent.");
        }

        int[,,] offsets =
        {
            {
                { 0x0f80, 0x0f80, 0x0f80, 0x0f80 },
                { 0x0fc0, 0x0fc0, 0x0fc0, 0x0fc0 }
            },
            {
                { 0x1640, 0x1680, 0x1600, 0x1680 },
                { 0x1640, 0x16c0, 0x1600, 0x16c0 }
            }
        };
        int[,,] oamIndices =
        {
            {
                { 0x00, 0x01, 0x00, 0x00 },
                { 0x00, 0x01, 0x00, 0x00 }
            },
            {
                { 0x00, 0x01, 0x00, 0x00 },
                { 0x01, 0x01, 0x01, 0x00 }
            }
        };
        string[] oam =
        [
            "8,0,0,0;8,8,2,0",
            "8,0,2,32;8,8,0,32"
        ];
        for (int mermaid = 0; mermaid < (ages ? 2 : 1); mermaid++)
        for (int frame = 0; frame < 2; frame++)
        for (int direction = 0; direction < 4; direction++)
        {
            SideScrollSwimmingFrame record = SwimmingFrame(
                mermaid != 0, frame, direction);
            if (record.Duration != 9 || record.Sprite != "spr_link" ||
                record.SourceOffset != offsets[mermaid, frame, direction] ||
                record.OamIndex != oamIndices[mermaid, frame, direction] ||
                record.Oam != oam[record.OamIndex])
            {
                throw new InvalidOperationException(
                    $"{AssetRoot}: native side-view Link swimming graphics are inconsistent.");
            }
        }
    }
}

internal readonly record struct SideScrollSwimmingFrame(
    bool MermaidSuit,
    int Frame,
    int Direction,
    int Duration,
    string Sprite,
    int SourceOffset,
    int OamIndex,
    string Oam);

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
    int WaterExitSpeedZ,
    int NormalSpeed,
    int PlatformPushSpeed,
    int KnockbackSpeed,
    int IceVelocityInterval,
    int SwimSpeed,
    int FastSwimSpeed,
    int GroundWallMask,
    int CeilingWallMask,
    int LandingHighMask,
    int LandingHighOffset,
    int BelowTileOffset,
    int BottomBoundary,
    int BottomLadderTransition,
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
