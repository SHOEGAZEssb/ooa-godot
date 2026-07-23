using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Renders the object state machines created by CUTSCENE_TIMEWARP and
/// TRANSITION_DEST_TIMEWARP. Timing remains update-based at 60 Hz.
/// </summary>
public partial class TimeWarpEffect : Node2D
{

    private readonly TimeWarpEffectDatabase _data;
    private readonly List<TimeWarpEffectFrame> _expand;
    private readonly List<TimeWarpEffectFrame> _contract;
    private readonly List<TimeWarpEffectFrame> _beamIntro;
    private readonly List<TimeWarpEffectFrame> _beamLoop;
    private readonly List<TimeWarpEffectFrame> _beamContract;
    private readonly List<TimeWarpEffectFrame> _trail;
    private readonly List<TimeWarpEffectFrame> _sparkle;
    private readonly Texture2D _particleTexture;
    private readonly Vector2 _particleOffset;
    private readonly TimeWarpEffectLayer _backgroundLayer;
    private readonly TimeWarpEffectLayer _foregroundLayer;
    private readonly bool _usesIndoorBeamPalette;
    private readonly List<Particle> _particles = new();
    private readonly List<Sparkle> _sparkles = new();

    private Animator? _primary;
    private Animator? _beam;
    private Animator? _trailAnimator;
    private Vector2 _linkPosition;
    private Vector2 _primaryPosition;
    private Vector2 _trailPosition;
    private bool _source;
    private bool _primaryExpanded;
    private bool _primaryContracting;
    private bool _beamContracting;
    private bool _trailMoving;
    private bool _sourceExpandedThisFrame;
    private bool _skipTrailUpdate;
    private bool _skipBeamUpdate;
    private bool _autoAdvance;
    private int _sourceCounter = 120;
    private int _trailSparkleCounter = 6;
    private int _globalFrame;
    private double _ticks;

    internal int ParticleSpawnCount { get; private set; }
    internal int SparkleSpawnCount { get; private set; }
    internal bool PrimaryVisible => _primary is not null;
    internal bool BeamVisible => _beam is not null;
    internal bool TrailVisible => _trailAnimator is not null || _trailMoving;
    internal int ActiveParticleCount => _particles.Count;
    internal int ActiveSparkleCount => _sparkles.Count;
    internal bool BeamContracting => _beamContracting;
    internal int SourceCounter => _sourceCounter;
    internal int PrimaryFrameIndex => _primary?.Index ?? -1;
    internal int BeamFrameIndex => _beam?.Index ?? -1;
    internal int BackgroundZIndex => _backgroundLayer.ZIndex;
    internal int ForegroundZIndex => _foregroundLayer.ZIndex;
    internal bool UsesIndoorBeamPalette => _usesIndoorBeamPalette;

    public TimeWarpEffect(
        TimeWarpEffectDatabase data,
        Vector2 linkPosition,
        bool source,
        bool usesIndoorBeamPalette)
    {
        _data = data;
        _linkPosition = linkPosition;
        _primaryPosition = linkPosition + Vector2.Down * 8;
        _source = source;
        _usesIndoorBeamPalette = usesIndoorBeamPalette;

        if (data.PrimaryPriority != 3 || data.BeamPriority != 2 ||
            data.TrailPriority != 1 || data.ParticlePriority != 3 ||
            data.SparklePriority != 1)
        {
            throw new InvalidOperationException(
                "Imported time-warp ground/beam/trail/particle/sparkle priorities " +
                "must remain 3/2/1/3/1.");
        }
        _backgroundLayer = new TimeWarpEffectLayer
        {
            Name = "Priority3GroundAndParticles",
            ZIndex = NpcCharacter.BehindLinkZIndex,
            ZAsRelative = false,
            DrawContents = DrawBackground
        };
        _foregroundLayer = new TimeWarpEffectLayer
        {
            Name = "Priority1And2BeamTrailSparkles",
            ZIndex = NpcCharacter.InFrontOfLinkZIndex,
            ZAsRelative = false,
            DrawContents = DrawForeground
        };
        AddChild(_backgroundLayer);
        AddChild(_foregroundLayer);

        Image timeWarpImage = LoadImage(data.TimeWarpSprite);
        Image commonImage = LoadImage(data.CommonSprite);
        Image sparkleImage = LoadImage(data.SparkleSprite);
        Color[] beamPalette = usesIndoorBeamPalette
            ? data.IndoorBeamPalette
            : data.OutdoorBeamPalette;

        // Animations $00/$01/$04 end in a $7f duplicate frame whose $ff
        // parameter changes object state immediately. Keep the visible frames
        // and discard that non-waiting sentinel.
        _expand = WithoutTerminal(BuildFrames(
            timeWarpImage, data.ExpandAnimation,
            data.PrimaryTileBase, data.PrimaryPalette));
        _contract = WithoutTerminal(BuildFrames(
            timeWarpImage, data.ContractAnimation,
            data.PrimaryTileBase, data.PrimaryPalette));
        List<TimeWarpEffectFrame> beamIntro = BuildFrames(
            timeWarpImage, data.BeamIntroAnimation, 0, data.BeamPalette, beamPalette);
        if (beamIntro.Count != 13)
            throw new InvalidOperationException($"Time-warp beam intro should contain 13 frames, got {beamIntro.Count}.");
        _beamIntro = beamIntro.Take(12).ToList();
        _beamLoop = BuildFrames(
            timeWarpImage, data.BeamLoopAnimation, 0, data.BeamPalette, beamPalette);
        _beamContract = WithoutTerminal(BuildFrames(
            timeWarpImage, data.BeamContractAnimation, 0, data.BeamPalette, beamPalette));
        _trail = BuildFrames(
            commonImage, data.TrailAnimation, data.TrailTileBase, data.TrailPalette);
        _sparkle = BuildFrames(
            sparkleImage, data.SparkleAnimation, data.SparkleTileBase, data.SparklePalette);
        if (_trail.Count != 4 || _sparkle.Count != 5)
        {
            throw new InvalidOperationException(
                $"Time-warp trail/sparkle should contain 4/5 frames, got {_trail.Count}/{_sparkle.Count}.");
        }

        (_particleTexture, _particleOffset) = NpcCharacter.BuildPositionedOamTexture(
            timeWarpImage, "8,4,0,0", data.ParticleTileBase, data.ParticlePalette,
            paletteOverride: null, sourceGrayscaleInverted: true);
        _primary = new Animator(_expand);
        SetProcess(false);
        QueueEffectRedraw();
    }

    internal void AdvanceFrame(int globalFrame)
    {
        _globalFrame = globalFrame;
        _sourceExpandedThisFrame = false;
        UpdatePrimary();
        UpdateBeam();
        UpdateParticles();
        UpdateTrail();
        UpdateSparkles();
        QueueEffectRedraw();
    }

    internal void BeginSourceTrail()
    {
        if (!_source)
            throw new InvalidOperationException("Only the source effect owns INTERAC_TIMEWARP subid $02.");
        // CUTSCENE_TIMEWARP's independent 120-count timer creates $dd:$02
        // while the $dd:$00 interaction still has 24 counts left after its
        // six 4-update expansion frames. Keep the ground and purple child
        // alive: $dd:$00 will select animation $01 and advance the child into
        // horizontal-fold animation $04 when its own counter reaches zero.
        _trailPosition = _linkPosition;
        _trailAnimator = new Animator(_trail.Take(3).ToList());
        _trailMoving = false;
        _skipTrailUpdate = true;
        _trailSparkleCounter = 6;
        QueueEffectRedraw();
    }

    internal void ContinueAfterTransition(int globalFrame)
    {
        _globalFrame = globalFrame;
        _autoAdvance = true;
        SetProcess(true);
    }

    internal void StopImmediately()
    {
        _autoAdvance = false;
        SetProcess(false);
        QueueFree();
    }

    public override void _Process(double delta)
    {
        if (!_autoAdvance)
            return;
        _ticks += delta * 60.0;
        while (_ticks >= 1.0)
        {
            _ticks -= 1.0;
            AdvanceFrame(++_globalFrame);
        }
        if (_primary is null && _beam is null && _particles.Count == 0 &&
            _trailAnimator is null && !_trailMoving && _sparkles.Count == 0)
        {
            _autoAdvance = false;
            QueueFree();
        }
    }

    public override void _ExitTree()
    {
        // Break the layer callbacks explicitly before their native CanvasItems
        // are released; they otherwise keep this effect reachable until the
        // managed finalizer pass during headless shutdown.
        _backgroundLayer.DrawContents = null;
        _foregroundLayer.DrawContents = null;
    }

    private void DrawBackground(TimeWarpEffectLayer layer)
    {
        if (_primary is not null)
            layer.DrawTexture(_primary.Texture, _primaryPosition + CurrentOffset(_primary));
        foreach (Particle particle in _particles)
        {
            if (((_globalFrame ^ particle.SubId) & 1) == 0)
            {
                layer.DrawTexture(
                    _particleTexture,
                    OracleObjectMath.ToPixelPosition(particle.PrecisePosition) + _particleOffset);
            }
        }
    }

    private void DrawForeground(TimeWarpEffectLayer layer)
    {
        if (_beam is not null)
            layer.DrawTexture(_beam.Texture, _linkPosition + CurrentOffset(_beam));
        if (_trailAnimator is not null)
            layer.DrawTexture(
                _trailAnimator.Texture,
                _trailPosition + CurrentOffset(_trailAnimator));
        else if (_trailMoving)
            layer.DrawTexture(_trail[^1].Texture, _trailPosition + _trail[^1].Offset);
        foreach (Sparkle sparkle in _sparkles)
            layer.DrawTexture(
                sparkle.Animator.Texture,
                sparkle.Position + CurrentOffset(sparkle.Animator));
    }

    private void QueueEffectRedraw()
    {
        _backgroundLayer.QueueRedraw();
        _foregroundLayer.QueueRedraw();
    }

    private void UpdatePrimary()
    {
        if (_primary is null)
            return;

        int previousFrame = _primary.Index;
        _primary.Advance();
        if (!_primaryContracting && _beam is null &&
            previousFrame == 0 && _primary.Index == 1)
        {
            _beam = new Animator(_beamIntro);
            // The newly spawned $dd:$03/$04 child initializes but does not
            // animate until its following object update.
            _skipBeamUpdate = true;
        }
        if (!_primary.Finished)
            return;

        if (_primaryContracting)
        {
            _primary = null;
            return;
        }

        if (!_primaryExpanded)
        {
            _primaryExpanded = true;
            if (_source)
            {
                // counter1 starts at 120 but is not decremented while animation
                // $00 is expanding.
                _sourceCounter = 120;
                _sourceExpandedThisFrame = true;
            }
            else
            {
                BeginContraction();
            }
            return;
        }

        if (_source)
        {
            _sourceCounter--;
            if (_sourceCounter == 0)
                BeginContraction();
        }
    }

    private void BeginContraction()
    {
        _primary = new Animator(_contract);
        _primaryContracting = true;
        if (_beam is null)
            return;

        _beam = new Animator(_beamContract);
        _beamContracting = true;
        // The parent advances the child to its contraction state; that child
        // selects animation $04 later in the same object pass, leaving its
        // first horizontal-fold frame visible for this update.
        _skipBeamUpdate = true;
    }

    private void UpdateBeam()
    {
        if (_beam is null)
            return;
        if (_skipBeamUpdate)
        {
            _skipBeamUpdate = false;
            return;
        }
        _beam.Advance();
        if (!_beam.Finished)
            return;
        if (_beamContracting)
        {
            _beam = null;
            return;
        }
        _beam = new Animator(_beamLoop, loop: true);
    }

    private void UpdateParticles()
    {
        if (_source && _primaryExpanded && !_primaryContracting && _primary is not null)
        {
            if (!_sourceExpandedThisFrame)
            {
                if (_sourceCounter >= 36 && (_sourceCounter & 0x07) == 0)
                {
                    int index = (_sourceCounter & 0x38) >> 3;
                    ParticleRecord record = _data.Particles[index];
                    _particles.Add(new Particle(
                        _primaryPosition + Vector2.Right * record.XOffset,
                        record.SpeedFixed,
                        record.SubId));
                    ParticleSpawnCount++;
                }
            }
        }

        for (int index = _particles.Count - 1; index >= 0; index--)
        {
            Particle particle = _particles[index];
            particle.PrecisePosition.Y -= particle.SpeedFixed / 256.0f;
            if (OracleObjectMath.ToPixelPosition(particle.PrecisePosition).Y <= -5)
                _particles.RemoveAt(index);
        }
    }

    private void UpdateTrail()
    {
        if (_skipTrailUpdate)
        {
            _skipTrailUpdate = false;
            return;
        }
        if (_trailAnimator is not null)
        {
            _trailAnimator.Advance();
            if (_trailAnimator.Finished)
            {
                _trailAnimator = null;
                _trailMoving = true;
            }
            return;
        }
        if (!_trailMoving)
            return;

        _trailPosition.Y -= 4;
        if (_trailPosition.Y <= -5)
        {
            _trailMoving = false;
            return;
        }
        _trailSparkleCounter--;
        if (_trailSparkleCounter != 0)
            return;
        _trailSparkleCounter = 6;
        _sparkles.Add(new Sparkle(
            _trailPosition,
            new Animator(_sparkle.Take(4).ToList())));
        SparkleSpawnCount++;
    }

    private void UpdateSparkles()
    {
        for (int index = _sparkles.Count - 1; index >= 0; index--)
        {
            _sparkles[index].Animator.Advance();
            if (_sparkles[index].Animator.Finished)
                _sparkles.RemoveAt(index);
        }
    }

    private static List<TimeWarpEffectFrame> BuildFrames(
        Image source,
        string encodedAnimation,
        int tileBase,
        int palette,
        Color[]? paletteOverride = null)
    {
        var result = new List<TimeWarpEffectFrame>();
        foreach (AnimationFrameDefinition frame in
            OracleGraphicsCache.GetAnimationDefinition(encodedAnimation).Frames)
        {
            (Texture2D texture, Vector2 offset) = NpcCharacter.BuildPositionedOamTexture(
                source, frame.EncodedOam, tileBase, palette,
                paletteOverride, sourceGrayscaleInverted: true);
            result.Add(new TimeWarpEffectFrame(texture, offset, frame.Duration));
        }
        if (result.Count == 0)
            throw new InvalidOperationException("Imported time-warp animation contains no frames.");
        return result;
    }

    private static List<TimeWarpEffectFrame> WithoutTerminal(List<TimeWarpEffectFrame> frames)
    {
        if (frames.Count < 2 || frames[^1].Duration != 0x7f)
            throw new InvalidOperationException("Time-warp animation lost its $7f terminal frame.");
        return frames.Take(frames.Count - 1).ToList();
    }

    private static Vector2 CurrentOffset(Animator animator) =>
        animator.Offset;

    private static Image LoadImage(string sprite)
    {
        return OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{sprite}.png");
    }
}

internal sealed record TimeWarpEffectFrame(Texture2D Texture, Vector2 Offset, int Duration);
