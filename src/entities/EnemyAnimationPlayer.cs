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
    private sealed record AnimationFrame(Texture2D Texture, int Duration, int Parameter);

    private readonly Node2D _entity;
    private readonly List<AnimationFrame>[] _animations;
    private int _animationIndex;
    private int _frameIndex;
    private int _frameCounter;

    public EnemyAnimationPlayer(Node2D entity, int animationCount)
    {
        if (animationCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(animationCount));
        _entity = entity;
        _animations = new List<AnimationFrame>[animationCount];
        for (int index = 0; index < animationCount; index++)
            _animations[index] = new List<AnimationFrame>();
    }

    public int AnimationIndex => _animationIndex;
    public int FrameIndex => _frameIndex;
    public int CurrentParameter => CurrentFrame.Parameter;
    public Texture2D CurrentTexture => CurrentFrame.Texture;
    public bool HasFrames => _animations[_animationIndex].Count > 0;

    private AnimationFrame CurrentFrame => _animations[_animationIndex][_frameIndex];

    public void Load(
        Image source,
        IReadOnlyList<string> encodedAnimations,
        int tileBase,
        int palette)
    {
        if (encodedAnimations.Count != _animations.Length)
        {
            throw new InvalidOperationException(
                $"Expected {_animations.Length} enemy animations, got {encodedAnimations.Count}.");
        }

        for (int index = 0; index < encodedAnimations.Count; index++)
        {
            foreach (OracleGraphicsCache.AnimationFrameDefinition frame in
                OracleGraphicsCache.GetAnimationDefinition(encodedAnimations[index]).Frames)
            {
                _animations[index].Add(new AnimationFrame(
                    NpcCharacter.BuildOamTexture(
                        source, frame.EncodedOam, tileBase, palette),
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

    public void Advance()
    {
        List<AnimationFrame> animation = _animations[_animationIndex];
        if (animation.Count <= 1)
            return;
        _frameCounter--;
        if (_frameCounter > 0)
            return;
        _frameIndex = (_frameIndex + 1) % animation.Count;
        _frameCounter = animation[_frameIndex].Duration;
        _entity.QueueRedraw();
    }
}
