using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Immutable OAM frames plus the original update-count animation clock.
/// Frame parameters remain available for state machines such as green Zol.
/// </summary>
internal sealed class EnemyAnimationPlayer
{

    private readonly Node2D _entity;
    private readonly List<EnemyAnimationPlayerAnimationFrame>[] _animations;
    private readonly int[] _loopStarts;
    private int _animationIndex;
    private int _frameIndex;
    private int _frameCounter;

    public EnemyAnimationPlayer(Node2D entity, int animationCount)
    {
        if (animationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(animationCount));
        _entity = entity;
        _animations = new List<EnemyAnimationPlayerAnimationFrame>[animationCount];
        _loopStarts = new int[animationCount];
        for (int index = 0; index < animationCount; index++)
            _animations[index] = new List<EnemyAnimationPlayerAnimationFrame>();
    }

    public int AnimationIndex => _animationIndex;
    public int FrameIndex => _frameIndex;
    public int CurrentParameter => CurrentFrame.Parameter;
    public Texture2D CurrentTexture => CurrentFrame.Texture;
    public Vector2 CurrentOffset => CurrentFrame.Offset;
    public Texture2D DamageTexture =>
        CurrentFrame.DamageTexture ?? CurrentFrame.Texture;
    public bool HasFrames => _animations[_animationIndex].Count > 0;

    private EnemyAnimationPlayerAnimationFrame CurrentFrame => _animations[_animationIndex][_frameIndex];

    public void Load(
        Image source,
        IReadOnlyList<string> encodedAnimations,
        int tileBase,
        int palette,
        int? damagePalette = null,
        IReadOnlyDictionary<int, Color[]>? paletteOverrides = null,
        bool sourceGrayscaleInverted = true,
        bool positionedOam = false)
    {
        if (encodedAnimations.Count != _animations.Length)
        {
            throw new InvalidOperationException(
                $"Expected {_animations.Length} enemy animations, got {encodedAnimations.Count}.");
        }
        if (positionedOam && paletteOverrides is not null)
        {
            throw new InvalidOperationException(
                "Positioned OAM does not support per-cell palette overrides.");
        }

        for (int index = 0; index < encodedAnimations.Count; index++)
        {
            AnimationDefinition definition =
                OracleGraphicsCache.GetAnimationDefinition(encodedAnimations[index]);
            _loopStarts[index] = Mathf.Clamp(
                definition.LoopStart, 0, Math.Max(0, definition.Frames.Length - 1));
            foreach (AnimationFrameDefinition frame in
                definition.Frames)
            {
                Texture2D texture;
                Texture2D? damageTexture = null;
                Vector2 offset;
                if (positionedOam)
                {
                    (texture, offset) =
                        NpcCharacter.BuildPositionedOamTexture(
                            source,
                            frame.EncodedOam,
                            tileBase,
                            palette,
                            paletteOverride: null,
                            sourceGrayscaleInverted);
                    if (damagePalette.HasValue)
                    {
                        Vector2 damageOffset;
                        (damageTexture, damageOffset) =
                            NpcCharacter.BuildPositionedOamTexture(
                                source,
                                frame.EncodedOam,
                                tileBase,
                                palette,
                                NpcCharacter.GetStandardSpritePalette(
                                    damagePalette.Value),
                                sourceGrayscaleInverted);
                        if (damageOffset != offset)
                        {
                            throw new InvalidOperationException(
                                "Positioned damage OAM changed the frame origin.");
                        }
                    }
                }
                else
                {
                    offset = new Vector2(-16, -16);
                    texture = paletteOverrides is null
                        ? NpcCharacter.BuildOamTexture(
                            source, frame.EncodedOam, tileBase, palette,
                            sourceGrayscaleInverted: sourceGrayscaleInverted)
                        : NpcCharacter.BuildOamTextureWithPaletteOverrides(
                            source, frame.EncodedOam, tileBase, palette,
                            paletteOverrides, sourceGrayscaleInverted);
                    damageTexture = damagePalette.HasValue
                        ? NpcCharacter.BuildOamTexture(
                            source,
                            frame.EncodedOam,
                            tileBase,
                            palette,
                            NpcCharacter.GetStandardSpritePalette(
                                damagePalette.Value),
                            sourceGrayscaleInverted)
                        : null;
                }
                _animations[index].Add(new EnemyAnimationPlayerAnimationFrame(
                    texture,
                    damageTexture,
                    offset,
                    frame.Duration,
                    frame.Parameter));
            }
        }
    }

    public void SetAnimation(int index)
    {
        _animationIndex = index;
        _frameIndex = 0;
        _frameCounter = _animations[index].Count > 0
            ? _animations[index][0].Duration
            : 1;
        _entity.QueueRedraw();
    }

    public void Advance(int decrement = 1)
    {
        if (decrement <= 0)
            throw new ArgumentOutOfRangeException(nameof(decrement));
        List<EnemyAnimationPlayerAnimationFrame> animation = _animations[_animationIndex];
        if (animation.Count <= 1)
            return;
        // Routines such as rope_animate reduce animCounter by three, clamp it
        // at zero, then call enemyAnimate. Crossing a frame boundary discards
        // any excess decrement rather than carrying it into the next frame.
        _frameCounter = Math.Max(0, _frameCounter - decrement);
        if (_frameCounter > 0)
            return;
        _frameIndex++;
        if (_frameIndex >= animation.Count)
            _frameIndex = _loopStarts[_animationIndex];
        _frameCounter = animation[_frameIndex].Duration;
        _entity.QueueRedraw();
    }
}

internal sealed record EnemyAnimationPlayerAnimationFrame(
    Texture2D Texture,
    Texture2D? DamageTexture,
    Vector2 Offset,
    int Duration,
    int Parameter);
