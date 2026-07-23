using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class NpcCharacter : TransitionOffsetNode2D
{
    // objectSetPriorityRelativeToLink compares the NPC's yh against
    // w1Link.yh+$0b. Interactions are queued before Link, so matching Link's
    // priority puts the NPC on top; otherwise Link remains on top.
    internal const float LinkPriorityYOffset = 0x0b;
    internal const int FixedLowPriorityZIndex = 8;
    internal const int BehindLinkZIndex = 9;
    internal const int InFrontOfLinkZIndex = 11;

    private enum Facing { Up, Right, Down, Left }

    private sealed record AnimationFrame(
        Texture2D Texture,
        int Duration,
        int Parameter,
        Vector2 Offset);

    private readonly List<AnimationFrame>[] _facingAnimations =
    {
        new(), new(), new(), new()
    };
    private readonly int[] _facingAnimationLoopStarts = new int[4];
    private readonly List<AnimationFrame> _scriptAnimation = new();
    private Image _sourceImage = null!;
    private Facing _facing = Facing.Down;
    private int _animationFrame;
    private double _animationTicks;
    private double _faceCooldownFrames;
    private Vector2 _scriptDrawOffset;
    private bool _scriptAnimationActive;
    private int _scriptAnimationLoopStart;
    private string _scriptAnimationSource = string.Empty;
    private float _animationRate = 1.0f;
    private Color[]? _paletteOverride;
    private bool _sourceGrayscaleInverted = true;
    private bool _blocksLink = true;
    private bool _active = true;
    private bool _flagVisible = true;
    private float _collisionRadiusY = CollisionRadius;
    private float _collisionRadiusX = CollisionRadius;
    private bool _scriptButtonSensitive;
    private int? _fixedDrawPriority;
    private int _textPosition;

    public NpcDatabase.NpcRecord Record { get; private set; }
    public bool Active => _active && _flagVisible;
    public string Message => Record.Message;
    public int TextId => Record.TextId;
    public int TextPosition => _textPosition;
    public const float CollisionRadius = 6.0f;
    public const float LinkCollisionRadius = 6.0f;
    public const float LinkBlockingRadius = CollisionRadius + LinkCollisionRadius;
    public Rect2 SpriteBounds => new(Position + new Vector2(-8, -8), new Vector2(16, 16));
    public int CurrentFrameColumn => GetFrameColumn();
    public int CurrentAnimationFrame => _animationFrame;
    internal int CurrentAnimationParameter => CurrentAnimation.Count == 0
        ? 0
        : CurrentAnimation[_animationFrame % CurrentAnimation.Count].Parameter;
    internal Vector2 CurrentAnimationOffset => CurrentAnimation.Count == 0
        ? Vector2.Zero
        : CurrentAnimation[_animationFrame % CurrentAnimation.Count].Offset;
    internal Vector2 ScriptDrawOffset => _scriptDrawOffset;
    internal Vector2I CurrentAnimationTextureSize => CurrentAnimation.Count == 0
        ? Vector2I.Zero
        : new Vector2I(
            CurrentAnimation[_animationFrame % CurrentAnimation.Count].Texture.GetWidth(),
            CurrentAnimation[_animationFrame % CurrentAnimation.Count].Texture.GetHeight());
    internal int CurrentAnimationOpaquePixels
    {
        get
        {
            if (CurrentAnimation.Count == 0)
                return 0;
            Image image = CurrentAnimation[_animationFrame % CurrentAnimation.Count].Texture.GetImage();
            int count = 0;
            for (int y = 0; y < image.GetHeight(); y++)
            for (int x = 0; x < image.GetWidth(); x++)
            {
                if (image.GetPixel(x, y).A > 0.1f)
                    count++;
            }
            return count;
        }
    }
    internal ulong CurrentAnimationPixelHash
    {
        get
        {
            if (CurrentAnimation.Count == 0)
                return 0;
            byte[] pixels = CurrentAnimation[
                _animationFrame % CurrentAnimation.Count].Texture.GetImage().GetData();
            ulong hash = 14695981039346656037UL;
            foreach (byte value in pixels)
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
    internal bool CurrentAnimationUsesColor(Color expected)
    {
        if (CurrentAnimation.Count == 0)
            return false;
        Image image = CurrentAnimation[_animationFrame % CurrentAnimation.Count].Texture.GetImage();
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
        {
            Color actual = image.GetPixel(x, y);
            if (Mathf.Abs(actual.R - expected.R) <= 1.0f / 255.0f &&
                Mathf.Abs(actual.G - expected.G) <= 1.0f / 255.0f &&
                Mathf.Abs(actual.B - expected.B) <= 1.0f / 255.0f &&
                Mathf.Abs(actual.A - expected.A) <= 1.0f / 255.0f)
                return true;
        }
        return false;
    }
    internal string CurrentScriptAnimationSource => _scriptAnimationSource;
    internal float AnimationRate => _animationRate;
    internal int SourceGraphicsWidth => _sourceImage.GetWidth();
    public Vector2I FacingVector => _facing switch
    {
        Facing.Up => Vector2I.Up,
        Facing.Right => Vector2I.Right,
        Facing.Left => Vector2I.Left,
        _ => Vector2I.Down
    };

    public Rect2 BodyBounds => ObjectCollisionBounds;
    public Rect2 ObjectCollisionBounds => new(
        Position - new Vector2(_collisionRadiusX, _collisionRadiusY),
        new Vector2(_collisionRadiusX * 2.0f, _collisionRadiusY * 2.0f));
    public Rect2 LinkBlockingBounds => new(
        Position - new Vector2(
            _collisionRadiusX + LinkCollisionRadius,
            _collisionRadiusY + LinkCollisionRadius),
        new Vector2(
            (_collisionRadiusX + LinkCollisionRadius) * 2.0f,
            (_collisionRadiusY + LinkCollisionRadius) * 2.0f));
    public Rect2 InteractionBounds => SpriteBounds.Grow(8);

    public void Initialize(NpcDatabase.NpcRecord record)
    {
        Record = record;
        _sourceImage = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        _sourceGrayscaleInverted = true;
        _collisionRadiusY = CollisionRadius;
        _collisionRadiusX = CollisionRadius;
        RebuildFacingAnimations();
        Position = new Vector2(record.X, record.Y);
        QueueRedraw();
    }

    public bool BlocksLinkCenter(Vector2 linkCenter)
    {
        if (!Active || !_blocksLink)
            return false;
        // checkObjectsCollided compares Object.xh/yh, not their 8.8
        // fractional bytes. Using precise movement coordinates here can stop
        // Link one rendered pixel early when approaching an NPC from its left
        // or top side, preventing the later object-side resolver from seeing
        // contact at all.
        Vector2 delta = OracleObjectMath.ToPixelPosition(linkCenter) -
            OracleObjectMath.ToPixelPosition(Position);
        return Mathf.Abs(delta.X) < _collisionRadiusX + LinkCollisionRadius &&
            Mathf.Abs(delta.Y) < _collisionRadiusY + LinkCollisionRadius;
    }

    /// <summary>
    /// Port of preventObjectHFromPassingObjectD for an NPC and Link. Unlike
    /// destination collision, the original helper also separates Link when a
    /// moving object enters his collision box during its own update.
    /// </summary>
    internal bool PreventPlayerPassing(Player player) =>
        PreventPlayerPassing(player, _collisionRadiusY, _collisionRadiusX);

    internal bool PreventPlayerPassing(
        Player player,
        float collisionRadiusY,
        float collisionRadiusX)
    {
        Vector2 link = player.Position;
        float radiusY = collisionRadiusY + LinkCollisionRadius;
        float radiusX = collisionRadiusX + LinkCollisionRadius;
        float differenceY = Mathf.Abs(link.Y - Position.Y);
        float differenceX = Mathf.Abs(link.X - Position.X);
        if (differenceY >= radiusY || differenceX >= radiusX)
            return false;

        // The assembly resolves the axis with less overlap. Its CP tie falls
        // through to horizontal resolution.
        float overlapY = radiusY - differenceY;
        float overlapX = radiusX - differenceX;
        bool horizontal = overlapY >= overlapX;
        float linkCoordinate = horizontal ? link.X : link.Y;
        float obstacleCoordinate = horizontal ? Position.X : Position.Y;
        int side = linkCoordinate > obstacleCoordinate ? 1 : -1;
        int coordinate = Mathf.FloorToInt(obstacleCoordinate) +
            side * Mathf.RoundToInt(horizontal ? radiusX : radiusY);
        player.SetScriptedCoordinateHigh(horizontal, coordinate);
        return true;
    }

    internal void SetCollisionRadii(float radiusY, float radiusX)
    {
        if (radiusY < 0.0f || radiusX < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(radiusY));
        _collisionRadiusY = radiusY;
        _collisionRadiusX = radiusX;
    }

    /// <summary>
    /// Mirrors objectAddToAButtonSensitiveObjectList for scripts whose pressed-A
    /// byte is their interaction trigger instead of an ordinary NPC text ID.
    /// </summary>
    internal void SetScriptButtonSensitive(bool sensitive) =>
        _scriptButtonSensitive = sensitive;

    internal void SetStatePosition(Vector2 position)
    {
        if (Position == position)
            return;
        Position = position;
        QueueRedraw();
    }

    internal void SetDialogue(
        int textId,
        string message,
        bool canFace,
        int textPosition = 0)
    {
        Record = Record with
        {
            TextId = textId,
            Message = message,
            CanFace = canFace
        };
        _textPosition = textPosition;
    }

    public bool CanTalkTo(Player player)
    {
        if (!Active || (!_scriptButtonSensitive &&
            (TextId == 0 || string.IsNullOrEmpty(Message))))
            return false;
        Vector2 talkPoint = player.Position + (Vector2)player.FacingVector * 8.0f;
        return InteractionBounds.HasPoint(talkPoint);
    }

    /// <summary>
    /// Exact linkInteractWithAButtonSensitiveObjects point test for native
    /// script actors. Link probes ten pixels in his facing direction and the
    /// object's collision radii use strict high-byte comparisons.
    /// </summary>
    internal bool CanScriptTalkTo(
        Player player,
        float collisionRadiusY,
        float collisionRadiusX,
        int pointOffset)
    {
        if (!Active || !_scriptButtonSensitive)
            return false;
        Vector2 link = OracleObjectMath.ToPixelPosition(player.Position);
        Vector2 target = OracleObjectMath.ToPixelPosition(Position);
        Vector2 point = link + (Vector2)player.FacingVector * pointOffset;
        return Mathf.Abs(target.Y - point.Y) < collisionRadiusY &&
            Mathf.Abs(target.X - point.X) < collisionRadiusX;
    }

    public void FaceToward(Vector2 target)
    {
        if (!Record.CanFace)
            return;
        SetFacing(GetFacingToward(target));
    }

    public void UpdateNpc(double delta, Vector2 linkPosition)
    {
        if (!Active)
            return;
        UpdateDrawPriority(linkPosition);
        AdvanceAnimation(delta);
        if (!Record.CanFace)
            return;

        _faceCooldownFrames = Math.Max(0.0, _faceCooldownFrames - delta * 60.0);
        if (_faceCooldownFrames > 0.0)
            return;

        Vector2 difference = linkPosition - Position;
        Facing desired = Mathf.Abs(difference.X) + Mathf.Abs(difference.Y) < 0x28
            ? GetFacingToward(linkPosition)
            : Facing.Down;
        if (desired == _facing)
            return;

        SetFacing(desired);
        _faceCooldownFrames = 30.0;
    }

    internal void UpdateDrawPriority(Vector2 linkPosition)
    {
        if (_fixedDrawPriority is int fixedDrawPriority)
        {
            ZIndex = fixedDrawPriority;
            return;
        }

        ZIndex = Position.Y > linkPosition.Y + LinkPriorityYOffset
            ? InFrontOfLinkZIndex
            : BehindLinkZIndex;
    }

    internal void SetFixedDrawPriority(int zIndex)
    {
        _fixedDrawPriority = zIndex;
        ZIndex = zIndex;
    }

    internal void ClearFixedDrawPriority() => _fixedDrawPriority = null;

    internal void SetScriptDrawOffset(Vector2 offset)
    {
        if (_scriptDrawOffset.IsEqualApprox(offset))
            return;

        _scriptDrawOffset = offset;
        QueueRedraw();
    }

    internal void SetScriptAnimation(string encodedAnimation)
    {
        _scriptAnimationSource = encodedAnimation;
        _scriptAnimation.Clear();
        _scriptAnimationLoopStart = AnimationLoopStart(encodedAnimation);
        _scriptAnimation.AddRange(BuildPositionedAnimation(
            _sourceImage, encodedAnimation, Record.TileBase, Record.Palette,
            _paletteOverride, _sourceGrayscaleInverted));
        _scriptAnimationActive = _scriptAnimation.Count > 0;
        _animationFrame = 0;
        _animationTicks = 0.0;
        QueueRedraw();
    }

    internal void SetFacingDirection(Vector2I direction)
    {
        _scriptAnimationSource = string.Empty;
        _scriptAnimationActive = false;
        SetFacing(direction == Vector2I.Up ? Facing.Up
            : direction == Vector2I.Right ? Facing.Right
            : direction == Vector2I.Left ? Facing.Left
            : Facing.Down);
        QueueRedraw();
    }

    internal void SetDirectionalAnimations(
        string upAnimation,
        string rightAnimation,
        string downAnimation,
        string leftAnimation)
    {
        Record = Record with
        {
            UpAnimation = upAnimation,
            RightAnimation = rightAnimation,
            DownAnimation = downAnimation,
            LeftAnimation = leftAnimation
        };
        RebuildFacingAnimations();
        _scriptAnimation.Clear();
        _scriptAnimationSource = string.Empty;
        _scriptAnimationActive = false;
        _scriptAnimationLoopStart = 0;
        _animationFrame = 0;
        _animationTicks = 0.0;
        QueueRedraw();
    }

    internal void SetBlocksLink(bool blocksLink) => _blocksLink = blocksLink;

    internal void SetAnimationRate(float rate)
    {
        _animationRate = Math.Max(0.0f, rate);
    }

    /// <summary>
    /// Advances the animation by an exact number of interactionAnimate calls.
    /// Cutscene interactions use this when their script changes animation or
    /// speed before the animation helper runs later in the same update.
    /// </summary>
    internal void AdvanceAnimationUpdates(int updates)
    {
        if (updates <= 0)
            return;
        AdvanceAnimationTicks(updates);
    }

    internal void ForceNextAnimationFrame()
    {
        List<AnimationFrame> animation = CurrentAnimation;
        if (animation.Count <= 1)
            return;
        _animationFrame++;
        if (_animationFrame >= animation.Count)
            _animationFrame = CurrentAnimationLoopStart;
        _animationTicks = 0.0;
        QueueRedraw();
    }

    internal void SetSourceGrayscaleInverted(bool inverted)
    {
        if (_sourceGrayscaleInverted == inverted)
            return;

        _sourceGrayscaleInverted = inverted;
        RebuildFacingAnimations();
        if (!string.IsNullOrEmpty(_scriptAnimationSource))
        {
            string encodedAnimation = _scriptAnimationSource;
            _scriptAnimation.Clear();
            _scriptAnimationLoopStart = AnimationLoopStart(encodedAnimation);
            _scriptAnimation.AddRange(BuildPositionedAnimation(
                _sourceImage,
                encodedAnimation,
                Record.TileBase,
                Record.Palette,
                _paletteOverride,
                _sourceGrayscaleInverted));
            _scriptAnimationActive = _scriptAnimation.Count > 0;
        }
        QueueRedraw();
    }

    internal void AdjustInitialAnimationCounter(int adjustment)
    {
        List<AnimationFrame> animation = CurrentAnimation;
        if (animation.Count == 0)
            return;
        int duration = animation[_animationFrame].Duration;
        int remaining = (duration + adjustment) & 0xff;
        _animationTicks = duration - remaining;
    }

    internal void SetSpritePalette(Color[] palette)
    {
        if (palette.Length != 4)
            throw new ArgumentException("A GBC OBJ palette must contain four colors.", nameof(palette));
        _paletteOverride = (Color[])palette.Clone();
        RebuildFacingAnimations();
        _scriptAnimation.Clear();
        _scriptAnimationSource = string.Empty;
        _scriptAnimationActive = false;
        _scriptAnimationLoopStart = 0;
        _animationFrame = 0;
        _animationTicks = 0.0;
        QueueRedraw();
    }

    /// <summary>
    /// Applies an interaction's explicit oamFlags palette bits. Most NPCs use
    /// their graphics record's base palette, but native handlers can replace
    /// those bits after interactionInitGraphics (for example $3c:$03).
    /// </summary>
    internal void SetBasePalette(int palette)
    {
        if (palette is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(palette));
        if (Record.Palette == palette)
            return;

        int frame = _animationFrame;
        double ticks = _animationTicks;
        Record = Record with { Palette = palette };
        RebuildFacingAnimations();
        if (_scriptAnimationActive && !string.IsNullOrEmpty(_scriptAnimationSource))
        {
            string encodedAnimation = _scriptAnimationSource;
            _scriptAnimation.Clear();
            _scriptAnimationLoopStart = AnimationLoopStart(encodedAnimation);
            _scriptAnimation.AddRange(BuildPositionedAnimation(
                _sourceImage,
                encodedAnimation,
                Record.TileBase,
                Record.Palette,
                _paletteOverride,
                _sourceGrayscaleInverted));
            _scriptAnimationActive = _scriptAnimation.Count > 0;
            _animationFrame = _scriptAnimation.Count == 0
                ? 0
                : Math.Min(frame, _scriptAnimation.Count - 1);
            _animationTicks = ticks;
        }
        QueueRedraw();
    }

    internal void SetScriptPaletteOverride(Color[]? palette)
    {
        _paletteOverride = palette is null ? null : (Color[])palette.Clone();
        if (!_scriptAnimationActive || string.IsNullOrEmpty(_scriptAnimationSource))
        {
            QueueRedraw();
            return;
        }

        int frame = _animationFrame;
        double ticks = _animationTicks;
        string encodedAnimation = _scriptAnimationSource;
        _scriptAnimation.Clear();
        _scriptAnimationLoopStart = AnimationLoopStart(encodedAnimation);
        _scriptAnimation.AddRange(BuildPositionedAnimation(
            _sourceImage,
            encodedAnimation,
            Record.TileBase,
            Record.Palette,
            _paletteOverride,
            _sourceGrayscaleInverted));
        _scriptAnimationActive = _scriptAnimation.Count > 0;
        _animationFrame = _scriptAnimation.Count == 0
            ? 0
            : Math.Min(frame, _scriptAnimation.Count - 1);
        _animationTicks = ticks;
        QueueRedraw();
    }

    internal void AppendScriptGraphics(string spriteName)
    {
        // Each loaded-object graphics slot occupies $20 8x8 tiles, or 128
        // pixels in the disassembly PNG representation. Short compressed
        // sheets leave the remainder of their VRAM slot blank; the next
        // chained header still begins on the following slot boundary.
        _sourceImage = OracleGraphicsCache.AppendGraphics(
            _sourceImage, $"res://assets/oracle/gfx/{spriteName}.png");
    }

    internal void SetActive(bool active)
    {
        _active = active;
        Visible = Active;
        QueueRedraw();
    }

    internal void SetFlagVisible(bool visible)
    {
        _flagVisible = visible;
        Visible = Active;
        QueueRedraw();
    }

    public override void _Draw()
    {
        List<AnimationFrame> animation = CurrentAnimation;
        if (animation.Count > 0)
        {
            AnimationFrame frame = animation[_animationFrame % animation.Count];
            DrawTexture(frame.Texture, frame.Offset + TransitionDrawOffset + _scriptDrawOffset);
        }
    }

    private void AdvanceAnimation(double delta)
    {
        AdvanceAnimationTicks(delta * 60.0 * _animationRate);
    }

    private void AdvanceAnimationTicks(double ticks)
    {
        List<AnimationFrame> animation = CurrentAnimation;
        if (animation.Count <= 1)
            return;
        _animationTicks += ticks;
        while (_animationTicks >= animation[_animationFrame].Duration)
        {
            _animationTicks -= animation[_animationFrame].Duration;
            _animationFrame++;
            if (_animationFrame >= animation.Count)
                _animationFrame = CurrentAnimationLoopStart;
            QueueRedraw();
        }
    }

    private List<AnimationFrame> CurrentAnimation => _scriptAnimationActive
        ? _scriptAnimation
        : _facingAnimations[(int)_facing];

    private int CurrentAnimationLoopStart => _scriptAnimationActive
        ? Mathf.Clamp(_scriptAnimationLoopStart, 0, Math.Max(0, _scriptAnimation.Count - 1))
        : Mathf.Clamp(_facingAnimationLoopStarts[(int)_facing], 0,
            Math.Max(0, _facingAnimations[(int)_facing].Count - 1));

    private Facing GetFacingToward(Vector2 target)
    {
        Vector2 delta = target - Position;
        if (Mathf.Abs(delta.X) > Mathf.Abs(delta.Y))
            return delta.X > 0 ? Facing.Right : Facing.Left;
        return delta.Y > 0 ? Facing.Down : Facing.Up;
    }

    private void SetFacing(Facing facing)
    {
        if (_facing == facing)
            return;
        _facing = facing;
        _animationFrame = 0;
        _animationTicks = 0.0;
        QueueRedraw();
    }

    private int GetFrameColumn()
    {
        int frameBase = Record.TileBase / 4;
        return frameBase + _facing switch
        {
            Facing.Up => 1,
            Facing.Right => 2,
            Facing.Left => 2,
            _ => 0
        };
    }

    private void RebuildFacingAnimations()
    {
        foreach (List<AnimationFrame> animation in _facingAnimations)
            animation.Clear();
        string up = Record.UpAnimation;
        string right = Record.RightAnimation;
        string down = Record.DownAnimation;
        string left = Record.LeftAnimation;
        _facingAnimationLoopStarts[(int)Facing.Up] = AnimationLoopStart(up);
        _facingAnimationLoopStarts[(int)Facing.Right] = AnimationLoopStart(right);
        _facingAnimationLoopStarts[(int)Facing.Down] = AnimationLoopStart(down);
        _facingAnimationLoopStarts[(int)Facing.Left] = AnimationLoopStart(left);
        _facingAnimations[(int)Facing.Up].AddRange(BuildAnimation(
            _sourceImage, up, Record.TileBase, Record.Palette,
            _paletteOverride, _sourceGrayscaleInverted));
        _facingAnimations[(int)Facing.Right].AddRange(BuildAnimation(
            _sourceImage, right, Record.TileBase, Record.Palette,
            _paletteOverride, _sourceGrayscaleInverted));
        _facingAnimations[(int)Facing.Down].AddRange(BuildAnimation(
            _sourceImage, down, Record.TileBase, Record.Palette,
            _paletteOverride, _sourceGrayscaleInverted));
        _facingAnimations[(int)Facing.Left].AddRange(BuildAnimation(
            _sourceImage, left, Record.TileBase, Record.Palette,
            _paletteOverride, _sourceGrayscaleInverted));
    }

    private static int AnimationLoopStart(string encodedAnimation)
    {
        return OracleGraphicsCache.GetAnimationDefinition(encodedAnimation).LoopStart;
    }

    private static IEnumerable<AnimationFrame> BuildAnimation(
        Image source,
        string encodedAnimation,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride,
        bool sourceGrayscaleInverted)
    {
        OracleGraphicsCache.AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(encodedAnimation);
        foreach (OracleGraphicsCache.AnimationFrameDefinition frame in definition.Frames)
        {
            yield return new AnimationFrame(
                BuildOamTexture(
                    source, frame.EncodedOam, tileBase, basePalette,
                    paletteOverride, sourceGrayscaleInverted),
                frame.Duration,
                frame.Parameter,
                new Vector2(-16, -16));
        }
    }

    private static IEnumerable<AnimationFrame> BuildPositionedAnimation(
        Image source,
        string encodedAnimation,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride,
        bool sourceGrayscaleInverted)
    {
        OracleGraphicsCache.AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(encodedAnimation);
        foreach (OracleGraphicsCache.AnimationFrameDefinition frame in definition.Frames)
        {
            (Texture2D texture, Vector2 offset) = BuildPositionedOamTexture(
                source, frame.EncodedOam, tileBase, basePalette,
                paletteOverride, sourceGrayscaleInverted);
            yield return new AnimationFrame(
                texture, frame.Duration, frame.Parameter, offset);
        }
    }

    internal static Texture2D BuildOamTexture(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride = null,
        bool sourceGrayscaleInverted = true)
    {
        OracleGraphicsCache.OamFrame frame = OracleGraphicsCache.GetOrCreateOamFrame(
            source,
            encodedOam,
            tileBase,
            basePalette,
            paletteOverride,
            paletteOverrides: null,
            sourceGrayscaleInverted,
            OracleGraphicsCache.CompositionMode.Fixed32,
            () => new OracleGraphicsCache.OamFrame(
                BuildOamTextureUncached(
                    source, encodedOam, tileBase, basePalette,
                    paletteOverride, paletteOverrides: null,
                    sourceGrayscaleInverted),
                new Vector2(-16, -16)));
        return frame.Texture;
    }

    internal static Texture2D BuildOamTextureWithPaletteOverrides(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        IReadOnlyDictionary<int, Color[]> paletteOverrides,
        bool sourceGrayscaleInverted = true)
    {
        OracleGraphicsCache.OamFrame frame = OracleGraphicsCache.GetOrCreateOamFrame(
            source,
            encodedOam,
            tileBase,
            basePalette,
            paletteOverride: null,
            paletteOverrides,
            sourceGrayscaleInverted,
            OracleGraphicsCache.CompositionMode.Fixed32,
            () => new OracleGraphicsCache.OamFrame(
                BuildOamTextureUncached(
                    source, encodedOam, tileBase, basePalette,
                    paletteOverride: null, paletteOverrides,
                    sourceGrayscaleInverted),
                new Vector2(-16, -16)));
        return frame.Texture;
    }

    internal static Texture2D BuildOamTextureUncachedForValidation(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride = null,
        bool sourceGrayscaleInverted = true) => BuildOamTextureUncached(
            source, encodedOam, tileBase, basePalette,
            paletteOverride, paletteOverrides: null, sourceGrayscaleInverted);

    private static Texture2D BuildOamTextureUncached(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride,
        IReadOnlyDictionary<int, Color[]>? paletteOverrides,
        bool sourceGrayscaleInverted)
    {
        // Imported NPC animations may suffix their final OAM frame with the
        // nonzero animation-loop target (`~N`). Consumers that render one
        // frame directly do not need that sequence metadata.
        int loopMarker = encodedOam.LastIndexOf('~');
        if (loopMarker >= 0)
            encodedOam = encodedOam[..loopMarker];
        Image output = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
        string[] blocks = encodedOam.Split(';', StringSplitOptions.RemoveEmptyEntries);
        // Lower OAM indices win when sprites overlap. Compose in reverse so
        // the first block has the same priority as it does on the Game Boy.
        for (int blockIndex = blocks.Length - 1; blockIndex >= 0; blockIndex--)
        {
            string block = blocks[blockIndex];
            string[] fields = block.Split(',');
            if (fields.Length != 4)
                continue;

            int oamY = ToSignedByte(int.Parse(fields[0]));
            int oamX = ToSignedByte(int.Parse(fields[1]));
            int tile = int.Parse(fields[2]);
            int flags = int.Parse(fields[3]);
            int cell = ((tileBase + tile) & 0xfe) / 2;
            int cellsPerRow = Math.Max(1, source.GetWidth() / 8);
            int sourceX = cell % cellsPerRow * 8;
            int sourceY = cell / cellsPerRow * 16;
            int destinationX = oamX + 8;
            int destinationY = oamY;
            bool flipX = (flags & 0x20) != 0;
            bool flipY = (flags & 0x40) != 0;
            int palette = basePalette ^ (flags & 0x07);
            Color[]? blockPalette = paletteOverrides is not null &&
                paletteOverrides.TryGetValue(palette, out Color[]? overridePalette)
                    ? overridePalette
                    : paletteOverride;

            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int readX = sourceX + (flipX ? 7 - x : x);
                int readY = sourceY + (flipY ? 15 - y : y);
                int writeX = destinationX + x;
                int writeY = destinationY + y;
                if (readX < 0 || readX >= source.GetWidth() || readY < 0 || readY >= source.GetHeight() ||
                    writeX < 0 || writeX >= output.GetWidth() || writeY < 0 || writeY >= output.GetHeight())
                    continue;
                Color pixel = RecolorSpritePixel(
                    source.GetPixel(readX, readY), palette, blockPalette,
                    sourceGrayscaleInverted);
                if (pixel.A > 0.1f)
                    output.SetPixel(writeX, writeY, pixel);
            }
        }

        return ImageTexture.CreateFromImage(output);
    }

    internal static (Texture2D Texture, Vector2 Offset) BuildPositionedOamTexture(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride,
        bool sourceGrayscaleInverted)
    {
        OracleGraphicsCache.OamFrame frame = OracleGraphicsCache.GetOrCreateOamFrame(
            source,
            encodedOam,
            tileBase,
            basePalette,
            paletteOverride,
            paletteOverrides: null,
            sourceGrayscaleInverted,
            OracleGraphicsCache.CompositionMode.Positioned,
            () =>
            {
                (Texture2D texture, Vector2 offset) = BuildPositionedOamTextureUncached(
                    source, encodedOam, tileBase, basePalette,
                    paletteOverride, sourceGrayscaleInverted);
                return new OracleGraphicsCache.OamFrame(texture, offset);
            });
        return (frame.Texture, frame.Offset);
    }

    internal static (Texture2D Texture, Vector2 Offset)
        BuildPositionedOamTextureUncachedForValidation(
            Image source,
            string encodedOam,
            int tileBase,
            int basePalette,
            Color[]? paletteOverride,
            bool sourceGrayscaleInverted) => BuildPositionedOamTextureUncached(
                source, encodedOam, tileBase, basePalette,
                paletteOverride, sourceGrayscaleInverted);

    private static (Texture2D Texture, Vector2 Offset) BuildPositionedOamTextureUncached(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride,
        bool sourceGrayscaleInverted)
    {
        string[] blocks = encodedOam.Split(';', StringSplitOptions.RemoveEmptyEntries);
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        foreach (string block in blocks)
        {
            string[] fields = block.Split(',');
            if (fields.Length != 4)
                continue;
            int destinationX = ToSignedByte(int.Parse(fields[1])) + 8;
            int destinationY = ToSignedByte(int.Parse(fields[0]));
            minX = Math.Min(minX, destinationX);
            minY = Math.Min(minY, destinationY);
            maxX = Math.Max(maxX, destinationX + 8);
            maxY = Math.Max(maxY, destinationY + 16);
        }
        if (minX == int.MaxValue)
            return (BuildOamTextureUncached(
                source, encodedOam, tileBase, basePalette, paletteOverride,
                paletteOverrides: null, sourceGrayscaleInverted),
                new Vector2(-16, -16));

        Image output = Image.CreateEmpty(maxX - minX, maxY - minY, false, Image.Format.Rgba8);
        // Preserve Game Boy OAM priority: lower indices cover later entries.
        for (int blockIndex = blocks.Length - 1; blockIndex >= 0; blockIndex--)
        {
            string[] fields = blocks[blockIndex].Split(',');
            if (fields.Length != 4)
                continue;
            int oamY = ToSignedByte(int.Parse(fields[0]));
            int oamX = ToSignedByte(int.Parse(fields[1]));
            int tile = int.Parse(fields[2]);
            int flags = int.Parse(fields[3]);
            int cell = ((tileBase + tile) & 0xfe) / 2;
            int cellsPerRow = Math.Max(1, source.GetWidth() / 8);
            int sourceX = cell % cellsPerRow * 8;
            int sourceY = cell / cellsPerRow * 16;
            int destinationX = oamX + 8 - minX;
            int destinationY = oamY - minY;
            bool flipX = (flags & 0x20) != 0;
            bool flipY = (flags & 0x40) != 0;
            int palette = basePalette ^ (flags & 0x07);

            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int readX = sourceX + (flipX ? 7 - x : x);
                int readY = sourceY + (flipY ? 15 - y : y);
                if (readX < 0 || readX >= source.GetWidth() || readY < 0 || readY >= source.GetHeight())
                    continue;
                Color pixel = RecolorSpritePixel(
                    source.GetPixel(readX, readY), palette, paletteOverride,
                    sourceGrayscaleInverted);
                if (pixel.A > 0.1f)
                    output.SetPixel(destinationX + x, destinationY + y, pixel);
            }
        }

        // The legacy 32x32 compositor drew at (-16,-16). Keep that coordinate
        // system while allowing large actors such as the Maku Tree to extend
        // above and to either side of the interaction origin.
        return (ImageTexture.CreateFromImage(output), new Vector2(minX - 16, minY - 16));
    }

    private static int ToSignedByte(int value) => value >= 0x80 ? value - 0x100 : value;

    private static Color RecolorSpritePixel(
        Color source,
        int palette,
        Color[]? paletteOverride,
        bool sourceGrayscaleInverted)
    {
        int color = Mathf.Clamp(Mathf.RoundToInt(
            (sourceGrayscaleInverted ? source.R : 1.0f - source.R) * 3.0f), 0, 3);
        if (source.A < 0.1f || color == 0)
            return Colors.Transparent;
        if (paletteOverride is not null)
            return paletteOverride[color];
        Color[][] palettes = StandardSpritePalettes;
        if (palette < 0 || palette >= palettes.Length)
            palette = 1;
        return palettes[palette][color];
    }

    private static readonly Color[][] StandardSpritePalettes =
    {
        new[] { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x02, 0x15, 0x08), GbcColor(0x1f, 0x1a, 0x11) },
        new[] { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x03, 0x10, 0x1f), GbcColor(0x1f, 0x1a, 0x11) },
        new[] { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x1f, 0x01, 0x05), GbcColor(0x1f, 0x1a, 0x11) },
        new[] { Colors.Transparent, GbcColor(0x00, 0x00, 0x00), GbcColor(0x1f, 0x0f, 0x01), GbcColor(0x1f, 0x1a, 0x11) },
        new[] { Colors.Transparent, GbcColor(0x0e, 0x15, 0x1f), GbcColor(0x00, 0x00, 0x1f), GbcColor(0x00, 0x00, 0x00) },
        new[] { Colors.Transparent, GbcColor(0x1f, 0x16, 0x06), GbcColor(0x1b, 0x00, 0x00), GbcColor(0x00, 0x00, 0x00) }
    };

    internal static Color[] GetStandardSpritePalette(int palette)
    {
        if (palette < 0 || palette >= StandardSpritePalettes.Length)
            palette = 1;
        return (Color[])StandardSpritePalettes[palette].Clone();
    }

    private static Color GbcColor(int red, int green, int blue)
    {
        return new Color(red / 31.0f, green / 31.0f, blue / 31.0f);
    }
}
