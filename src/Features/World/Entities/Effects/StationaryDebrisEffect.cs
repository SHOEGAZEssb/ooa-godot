
namespace Oracles;

/// <summary>
/// Stationary INTERAC_PUFF $05 / INTERAC_SNOWDEBRIS $09. State 0 initializes graphics and conditionally requests
/// its ID-table sound; later updates run animation 0 and delete one update
/// after its terminal bit-$80 animation parameter becomes active.
/// </summary>
internal partial class StationaryDebrisEffect : FixedEffectNode2D
{

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Root, int Id), List<StationaryDebrisEffectFrameRecord>> Definitions = new();
    static StationaryDebrisEffect() => OracleGraphicsCache.RegisterApplicationCache(Definitions.Clear);
    private List<StationaryDebrisEffectFrameRecord> _animation = null!;
    private Action<int> _playSound = null!;
    private int _sound;
    private int _animationFrame;
    private int _animationCounter;
    private bool _initialized;
    private Func<int>? _frameCounter;
    private int _interactionSlot;

    internal override bool Finished { get; private protected set; }
    internal bool Initialized => _initialized;
    internal int ElapsedUpdates { get; private set; }
    internal bool Flickers { get; private set; }
    internal bool AlwaysUpdates { get; private set; }
    internal int AnimationFrame => Math.Min(_animationFrame, _animation.Count - 1);
    internal int CurrentParameter => _animation[AnimationFrame].Parameter;
    internal int ZHigh { get; private set; }
    internal int InteractionId { get; private set; }

    internal void Initialize(string assetRoot,
        Vector2 position,
        int sound,
        bool flickers = false,
        Action<int>? playSound = null,
        int zHigh = 0,
        bool alwaysUpdates = true,
        int interactionId = Oracles.InteractionId.Puff)
    {
        AssetRoot = assetRoot;
        if (interactionId is not (Oracles.InteractionId.Puff or Oracles.InteractionId.SnowDebris))
            throw new ArgumentOutOfRangeException(nameof(interactionId), $"Stationary debris ${interactionId:x2} has no imported native definition.");
        InteractionId = interactionId;
        Position = position;
        ZHigh = (sbyte)(byte)zHigh;
        _animation = Definitions.GetOrAdd((AssetRoot, interactionId), static key => LoadDefinition(key.Root, key.Id));
        _sound = sound;
        Flickers = flickers;
        AlwaysUpdates = alwaysUpdates;
        _playSound = playSound ?? (static _ => { });
        _animationFrame = 0;
        _animationCounter = _animation[0].Duration;
        Visible = false;
        QueueRedraw();
    }

    internal void BindNativeTiming(int interactionSlot, Func<int> frameCounter)
    {
        _interactionSlot = interactionSlot;
        _frameCounter = frameCounter;
    }

    internal override void UpdateFrame()
    {
        if (Finished)
            return;

        ElapsedUpdates++;
        if (!_initialized)
        {
            _initialized = true;
            Visible = true;
            if (_sound != SoundId.MusNone)
                _playSound(_sound);
            return;
        }

        if ((CurrentParameter & 0x80) != 0)
        {
            Finished = true;
            Visible = false;
            return;
        }

        // Subid bit 0 uses (wFrameCounter XOR the interaction slot page) bit
        // 0. Neither effect age nor the spawning owner's slot sets this phase.
        if (Flickers)
            Visible = (((_frameCounter ?? throw new InvalidOperationException(
                "INTERAC_PUFF $05: flickering state1 requires native interaction timing."))()
                ^ _interactionSlot) & 1) == 0;

        _animationCounter--;
        if (_animationCounter == 0)
        {
            _animationFrame++;
            if (_animationFrame >= _animation.Count)
            {
                Finished = true;
                Visible = false;
                return;
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
                _animation[AnimationFrame].Texture,
                new Vector2(-16, -16 + ZHigh) + TransitionDrawOffset);
        }
    }

    private static List<StationaryDebrisEffectFrameRecord> LoadDefinition(string assetRoot, int interactionId)
    {
        GeneratedTable table = GeneratedTable.Load(
            assetRoot + (interactionId == Oracles.InteractionId.Puff ? "effects/puzzle_puff.tsv" : "effects/snow_debris.tsv"),
            new GeneratedTableSchema(
                "stationary tile debris",
                GeneratedTableKeySemantics.Ordered,
                ["tile-base", "palette", "animation"],
                headerRequired: true));
        GeneratedTableRow row = table.SingleRow();
        int tileBase = row.UnsignedDecimal(0);
        int palette = row.UnsignedDecimal(1);
        if (tileBase != (interactionId == Oracles.InteractionId.Puff ? 0x16 : 0x1e) ||
            palette != (interactionId == Oracles.InteractionId.Puff ? 3 : 1))
            throw new InvalidOperationException($"Imported stationary debris ${interactionId:x2} constants are invalid.");

        Image source = OracleGraphicsCache.LoadImage(
            assetRoot + "gfx/spr_common_sprites.png");
        var animation = new List<StationaryDebrisEffectFrameRecord>();
        foreach (AnimationFrameDefinition frame in
            OracleGraphicsCache.GetAnimationDefinition(row.RequiredString(2)).Frames)
        {
            animation.Add(new StationaryDebrisEffectFrameRecord(
                OracleOamCompositor.BuildOamTexture(
                    source, frame.EncodedOam, tileBase, palette),
                frame.Duration,
                frame.Parameter));
        }
        if (animation.Count < 2 ||
            (animation[^1].Parameter & 0x80) == 0)
        {
            throw new InvalidOperationException(
                "INTERAC_PUFF animation 0 has no terminal parameter.");
        }
        return animation;
    }
}

internal sealed record StationaryDebrisEffectFrameRecord(Texture2D Texture, int Duration, int Parameter);
