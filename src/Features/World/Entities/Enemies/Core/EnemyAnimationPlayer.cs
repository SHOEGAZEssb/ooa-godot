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
    private int _basePalette;
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
        bool positionedOam = false,
        IReadOnlyList<int>? paletteVariants = null)
    {
        if (encodedAnimations.Count != _animations.Length)
        {
            throw new InvalidOperationException(
                $"Expected {_animations.Length} enemy animations, got {encodedAnimations.Count}.");
        }
        _basePalette = palette;
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
                Dictionary<int, Texture2D>? variantTextures = null;
                Vector2 offset;
                if (positionedOam)
                {
                    (texture, offset) = paletteOverrides is null
                        ? NpcCharacter.BuildPositionedOamTexture(
                            source,
                            frame.EncodedOam,
                            tileBase,
                            palette,
                            paletteOverride: null,
                            sourceGrayscaleInverted)
                        : NpcCharacter.BuildPositionedOamTextureWithPaletteOverrides(
                            source,
                            frame.EncodedOam,
                            tileBase,
                            palette,
                            paletteOverrides,
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
                if (paletteVariants is not null)
                {
                    foreach (int variantPalette in paletteVariants)
                    {
                        if (variantPalette == palette)
                            continue;
                        variantTextures ??= new Dictionary<int, Texture2D>();
                        if (variantTextures.ContainsKey(variantPalette))
                            continue;

                        Texture2D variantTexture;
                        if (positionedOam)
                        {
                            Vector2 variantOffset;
                            (variantTexture, variantOffset) =
                                paletteOverrides is null
                                    ? NpcCharacter.BuildPositionedOamTexture(
                                        source,
                                        frame.EncodedOam,
                                        tileBase,
                                        variantPalette,
                                        paletteOverride: null,
                                        sourceGrayscaleInverted)
                                    : NpcCharacter.BuildPositionedOamTextureWithPaletteOverrides(
                                        source,
                                        frame.EncodedOam,
                                        tileBase,
                                        variantPalette,
                                        paletteOverrides,
                                        sourceGrayscaleInverted);
                            if (variantOffset != offset)
                            {
                                throw new InvalidOperationException(
                                    "Changing an enemy frame palette changed its positioned OAM origin.");
                            }
                        }
                        else
                        {
                            variantTexture = paletteOverrides is null
                                ? NpcCharacter.BuildOamTexture(
                                    source,
                                    frame.EncodedOam,
                                    tileBase,
                                    variantPalette,
                                    sourceGrayscaleInverted:
                                        sourceGrayscaleInverted)
                                : NpcCharacter.BuildOamTextureWithPaletteOverrides(
                                    source,
                                    frame.EncodedOam,
                                    tileBase,
                                    variantPalette,
                                    paletteOverrides,
                                    sourceGrayscaleInverted);
                        }
                        variantTextures.Add(variantPalette, variantTexture);
                    }
                }
                _animations[index].Add(new EnemyAnimationPlayerAnimationFrame(
                    texture,
                    damageTexture,
                    variantTextures,
                    offset,
                    frame.Duration,
                    frame.Parameter));
            }
        }
    }

    public Texture2D CurrentTextureForPalette(int palette)
    {
        if (palette == _basePalette)
            return CurrentFrame.Texture;
        if (CurrentFrame.PaletteVariants is not null &&
            CurrentFrame.PaletteVariants.TryGetValue(
                palette, out Texture2D? texture))
        {
            return texture;
        }
        throw new InvalidOperationException(
            $"Enemy animation palette ${palette:x2} was not loaded as a variant.");
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

    public void SetFrameCounter(int remaining)
    {
        if (remaining is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(remaining));
        _frameCounter = remaining;
    }
}

internal sealed record EnemyAnimationPlayerAnimationFrame(
    Texture2D Texture,
    Texture2D? DamageTexture,
    IReadOnlyDictionary<int, Texture2D>? PaletteVariants,
    Vector2 Offset,
    int Duration,
    int Parameter);
