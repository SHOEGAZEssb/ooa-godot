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
    private sealed record AnimationFrame(
        Texture2D Texture,
        Texture2D? DamageTexture,
        int Duration,
        int Parameter);

    private readonly Node2D _entity;
    private readonly List<AnimationFrame>[] _animations;
    private readonly int[] _loopStarts;
    private int _animationIndex;
    private int _frameIndex;
    private int _frameCounter;

    public EnemyAnimationPlayer(Node2D entity, int animationCount)
    {
        if (animationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(animationCount));
        _entity = entity;
        _animations = new List<AnimationFrame>[animationCount];
        _loopStarts = new int[animationCount];
        for (int index = 0; index < animationCount; index++)
            _animations[index] = new List<AnimationFrame>();
    }

    public int AnimationIndex => _animationIndex;
    public int FrameIndex => _frameIndex;
    public int CurrentParameter => CurrentFrame.Parameter;
    public Texture2D CurrentTexture => CurrentFrame.Texture;
    public Texture2D DamageTexture =>
        CurrentFrame.DamageTexture ?? CurrentFrame.Texture;
    public bool HasFrames => _animations[_animationIndex].Count > 0;

    private AnimationFrame CurrentFrame => _animations[_animationIndex][_frameIndex];

    public void Load(
        Image source,
        IReadOnlyList<string> encodedAnimations,
        int tileBase,
        int palette,
        int? damagePalette = null,
        IReadOnlyDictionary<int, Color[]>? paletteOverrides = null,
        bool sourceGrayscaleInverted = true)
    {
        if (encodedAnimations.Count != _animations.Length)
        {
            throw new InvalidOperationException(
                $"Expected {_animations.Length} enemy animations, got {encodedAnimations.Count}.");
        }

        for (int index = 0; index < encodedAnimations.Count; index++)
        {
            OracleGraphicsCache.AnimationDefinition definition =
                OracleGraphicsCache.GetAnimationDefinition(encodedAnimations[index]);
            _loopStarts[index] = Mathf.Clamp(
                definition.LoopStart, 0, Math.Max(0, definition.Frames.Length - 1));
            foreach (OracleGraphicsCache.AnimationFrameDefinition frame in
                definition.Frames)
            {
                Texture2D? damageTexture = damagePalette.HasValue
                    ? NpcCharacter.BuildOamTexture(
                        source,
                        frame.EncodedOam,
                        tileBase,
                        palette,
                        NpcCharacter.GetStandardSpritePalette(damagePalette.Value),
                        sourceGrayscaleInverted)
                    : null;
                _animations[index].Add(new AnimationFrame(
                    paletteOverrides is null
                        ? NpcCharacter.BuildOamTexture(
                            source, frame.EncodedOam, tileBase, palette,
                            sourceGrayscaleInverted: sourceGrayscaleInverted)
                        : NpcCharacter.BuildOamTextureWithPaletteOverrides(
                            source, frame.EncodedOam, tileBase, palette,
                            paletteOverrides, sourceGrayscaleInverted),
                    damageTexture,
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
        List<AnimationFrame> animation = _animations[_animationIndex];
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
