
namespace Oracles;

/// <summary>
/// INTERAC_GRASSDEBRIS ($00) and INTERAC_REDGRASSDEBRIS ($01). State 0
/// initializes the common-sprite graphic and requests SND_CUTGRASS. Their
/// eight four-update OAM frames scatter four grass pieces, then expose the
/// terminal $ff parameter for one update before deletion.
/// </summary>
internal partial class GrassDebrisEffect : TransitionOffsetNode2D
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyDictionary<int, GrassDebrisDefinition>> Definitions = new(System.StringComparer.Ordinal);
    static GrassDebrisEffect() => OracleGraphicsCache.RegisterApplicationCache(Definitions.Clear);

    private GrassDebrisDefinition _record = null!;
    private IReadOnlyList<GrassDebrisEffectFrameRecord> _animation = null!;
    private int _animationFrame;
    private int _animationCounter;
    private bool _initialized;
    private Action<int> _playSound = null!;
    private Func<int> _grassModifier = null!;
    private int _nativeSlot;
    private bool _underwater;
    internal int PaletteIndex { get; private set; }
    internal void BindNativeSlot(int slot) => _nativeSlot = slot;

    internal bool Finished { get; private set; }
    internal bool Flickers { get; private set; }
    internal bool UpdatesDuringDialogue { get; private set; }
    internal int InteractionId => _record.InteractionId;
    internal int ElapsedUpdates { get; private set; }
    internal int AnimationFrame =>
        Math.Min(_animationFrame, _animation.Count - 1);
    internal int CurrentParameter =>
        _animation[AnimationFrame].Parameter;
    internal Texture2D CurrentTexture =>
        _animation[AnimationFrame].Texture;

    internal void Initialize(string assetRoot,
        Vector2 position,
        int interactionId = Oracles.InteractionId.GrassDebris,
        bool flickers = false,
        bool underwater = false,
        Action<int>? playSound = null,
        Func<int>? grassModifier = null)
    {
        AssetRoot = assetRoot;
        Position = position;
        IReadOnlyDictionary<int, GrassDebrisDefinition> definitions =
            Definitions.GetOrAdd(AssetRoot, LoadDefinitions);
        if (!definitions.TryGetValue(
                interactionId, out GrassDebrisDefinition? record))
        {
            throw new InvalidOperationException(
                $"Unsupported grass-debris interaction ${interactionId:x2}.");
        }

        _record = record;
        _underwater = underwater && interactionId == Oracles.InteractionId.GrassDebris &&
            OracleAssetPack.GameFor(assetRoot) == OracleGame.Ages;
        _animation = _underwater
            ? record.UnderwaterAnimation
            : record.Animation;
        PaletteIndex = _underwater ? 6 : 0;
        _grassModifier = grassModifier ?? (static () => 0);
        Flickers = flickers;
        // breakTileDebris.s leaves enabled bit 7 clear only when subid bit 1
        // selects the underwater variant.
        UpdatesDuringDialogue = !underwater;
        _playSound = playSound ?? (static _ => { });
        _animationFrame = 0;
        _animationCounter = _animation[0].Duration;
        QueueRedraw();
    }

    internal void UpdateFrame(int globalFrameCounter)
    {
        if (Finished)
            return;

        ElapsedUpdates++;
        if (!_initialized)
        {
            _initialized = true;
            // breakTileDebris.@interac00 samples the live grass modifier at
            // state0, after allocation; only Ages selects underwater palette 6.
            if (!_underwater && InteractionId == Oracles.InteractionId.GrassDebris)
            {
                PaletteIndex = _grassModifier() & 3;
                _animation = _record.SeasonAnimations[PaletteIndex];
            }
            _playSound(_record.Sound);
            return;
        }

        if ((CurrentParameter & 0x80) != 0)
        {
            Finished = true;
            Visible = false;
            return;
        }

        // Native interaction pages $d2-$df have the slot index's low parity.
        if (Flickers)
            Visible = ((globalFrameCounter ^ _nativeSlot) & 1) == 0;

        _animationCounter--;
        if (_animationCounter == 0)
        {
            _animationFrame++;
            if (_animationFrame >= _animation.Count)
            {
                throw new InvalidOperationException(
                    $"INTERAC_GRASSDEBRIS ${InteractionId:x2} animation " +
                    "ended without a terminal frame.");
            }
            _animationCounter = _animation[_animationFrame].Duration;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }

    private static IReadOnlyDictionary<int, GrassDebrisDefinition>
        LoadDefinitions(string assetRoot)
    {
        GeneratedTable table = GeneratedTable.Load(
            assetRoot + "effects/grass_debris.tsv",
            new GeneratedTableSchema(
                "grass debris",
                GeneratedTableKeySemantics.Unique,
                [
                    "interaction-id", "sprite", "tile-base", "palette",
                    "underwater-palette", "sound", "animation"
                ],
                ["interaction-id"],
                headerRequired: true));
        if (table.Rows.Count != 2)
        {
            throw new InvalidOperationException(
                "INTERAC_GRASSDEBRIS $00/$01 should have two rows, got " +
                $"{table.Rows.Count}.");
        }

        var definitions = new Dictionary<int, GrassDebrisDefinition>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int interactionId = row.HexByte(0);
            string sprite = row.RequiredString(1);
            int tileBase = row.UnsignedDecimal(2);
            int palette = row.UnsignedDecimal(3);
            int underwaterPalette = row.UnsignedDecimal(4);
            int sound = row.HexByte(5);
            if (interactionId is not (Oracles.InteractionId.GrassDebris or Oracles.InteractionId.RedGrassDebris) ||
                sprite != "spr_common_sprites" ||
                tileBase != 0x00 ||
                palette != 0 ||
                underwaterPalette != (interactionId == Oracles.InteractionId.GrassDebris &&
                    OracleAssetPack.GameFor(assetRoot) == OracleGame.Ages ? 6 : 0) ||
                sound != SoundId.SndCutGrass)
            {
                throw new InvalidOperationException(
                    $"Imported INTERAC_GRASSDEBRIS ${interactionId:x2} " +
                    "constants are invalid.");
            }

            Image source = OracleGraphicsCache.LoadImage(
                assetRoot + $"gfx/{sprite}.png");
            AnimationDefinition animationDefinition =
                OracleGraphicsCache.GetAnimationDefinition(
                    row.RequiredString(6));
            List<GrassDebrisEffectFrameRecord> animation =
                BuildAnimation(animationDefinition, source, tileBase, palette);
            List<GrassDebrisEffectFrameRecord> underwaterAnimation =
                BuildAnimation(
                    animationDefinition, source, tileBase, underwaterPalette);
            var seasonAnimations = new List<GrassDebrisEffectFrameRecord>[4];
            seasonAnimations[0] = animation;
            for (int modifier = 1; modifier < 4; modifier++)
                seasonAnimations[modifier] = interactionId == Oracles.InteractionId.GrassDebris
                    ? BuildAnimation(animationDefinition,source,tileBase,modifier) : animation;

            bool validAnimation = animation.Count == 9 &&
                animation[^1].Duration == 0x7f &&
                (animation[^1].Parameter & 0x80) != 0;
            for (int frame = 0;
                 validAnimation && frame < animation.Count - 1;
                 frame++)
            {
                validAnimation = animation[frame].Duration == 4 &&
                    animation[frame].Parameter == 0;
            }
            if (!validAnimation)
            {
                throw new InvalidOperationException(
                    $"INTERAC_GRASSDEBRIS ${interactionId:x2} animation 0 " +
                    "is not its eight 4-update frames plus terminal sequence.");
            }

            definitions.Add(
                interactionId,
                new GrassDebrisDefinition(
                    interactionId, sound, animation, underwaterAnimation,seasonAnimations));
        }
        return definitions;
    }

    private static List<GrassDebrisEffectFrameRecord> BuildAnimation(
        AnimationDefinition definition,
        Image source,
        int tileBase,
        int palette)
    {
        var animation = new List<GrassDebrisEffectFrameRecord>();
        foreach (AnimationFrameDefinition frame in definition.Frames)
        {
            animation.Add(new GrassDebrisEffectFrameRecord(
                OracleOamCompositor.BuildOamTexture(
                    source, frame.EncodedOam, tileBase, palette),
                frame.Duration,
                frame.Parameter));
        }
        return animation;
    }
}

internal sealed record GrassDebrisEffectFrameRecord(
    Texture2D Texture,
    int Duration,
    int Parameter);

internal sealed record GrassDebrisDefinition(
    int InteractionId,
    int Sound,
    List<GrassDebrisEffectFrameRecord> Animation,
    List<GrassDebrisEffectFrameRecord> UnderwaterAnimation,
    List<GrassDebrisEffectFrameRecord>[] SeasonAnimations);

internal sealed class GrassDebrisRoomEntity(GrassDebrisEffect debris)
    : RoomEntityAdapter<GrassDebrisEffect>(
        debris, debris.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity
{
    public bool Finished => Entity.Finished;
    bool IUpdatesDuringDialogueRoomEntity.UpdatesDuringDialogue =>
        Entity.UpdatesDuringDialogue;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter);

}
