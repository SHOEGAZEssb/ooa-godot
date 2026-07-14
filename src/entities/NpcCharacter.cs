using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class NpcCharacter : Node2D
{
    // objectSetPriorityRelativeToLink compares the NPC's yh against
    // w1Link.yh+$0b. Interactions are queued before Link, so matching Link's
    // priority puts the NPC on top; otherwise Link remains on top.
    internal const float LinkPriorityYOffset = 0x0b;
    internal const int BehindLinkZIndex = 9;
    internal const int InFrontOfLinkZIndex = 11;

    private enum Facing { Up, Right, Down, Left }

    private sealed record AnimationFrame(Texture2D Texture, int Duration, Vector2 Offset);

    private readonly List<AnimationFrame>[] _facingAnimations =
    {
        new(), new(), new(), new()
    };
    private readonly List<AnimationFrame> _scriptAnimation = new();
    private Image _sourceImage = null!;
    private Facing _facing = Facing.Down;
    private int _animationFrame;
    private double _animationTicks;
    private double _faceCooldownFrames;
    private Vector2 _transitionDrawOffset;
    private bool _scriptAnimationActive;
    private float _animationRate = 1.0f;
    private Color[]? _paletteOverride;
    private bool _blocksLink = true;
    private bool _active = true;
    private bool _flagVisible = true;

    public NpcDatabase.NpcRecord Record { get; private set; }
    public bool Active => _active && _flagVisible;
    public string Message => Record.Message;
    public int TextId => Record.TextId;
    public const float CollisionRadius = 6.0f;
    public const float LinkCollisionRadius = 6.0f;
    public const float LinkBlockingRadius = CollisionRadius + LinkCollisionRadius;
    public Rect2 SpriteBounds => new(Position + new Vector2(-8, -8), new Vector2(16, 16));
    public int CurrentFrameColumn => GetFrameColumn();
    public int CurrentAnimationFrame => _animationFrame;
    internal Vector2 CurrentAnimationOffset => CurrentAnimation.Count == 0
        ? Vector2.Zero
        : CurrentAnimation[_animationFrame % CurrentAnimation.Count].Offset;
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
    public Vector2 TransitionDrawOffset => _transitionDrawOffset;
    public Vector2I FacingVector => _facing switch
    {
        Facing.Up => Vector2I.Up,
        Facing.Right => Vector2I.Right,
        Facing.Left => Vector2I.Left,
        _ => Vector2I.Down
    };

    public Rect2 BodyBounds => ObjectCollisionBounds;
    public Rect2 ObjectCollisionBounds => new(
        Position - new Vector2(CollisionRadius, CollisionRadius),
        new Vector2(CollisionRadius * 2.0f, CollisionRadius * 2.0f));
    public Rect2 LinkBlockingBounds => new(
        Position - new Vector2(LinkBlockingRadius, LinkBlockingRadius),
        new Vector2(LinkBlockingRadius * 2.0f, LinkBlockingRadius * 2.0f));
    public Rect2 InteractionBounds => SpriteBounds.Grow(8);

    public void Initialize(NpcDatabase.NpcRecord record)
    {
        Record = record;
        byte[] bytes = FileAccess.GetFileAsBytes($"res://assets/oracle/gfx/{record.SpriteName}.png");
        Image image = new();
        image.LoadPngFromBuffer(bytes);
        _sourceImage = image;
        RebuildFacingAnimations();
        Position = new Vector2(record.X, record.Y);
        QueueRedraw();
    }

    public bool BlocksLinkCenter(Vector2 linkCenter)
    {
        if (!Active || !_blocksLink)
            return false;
        Vector2 delta = linkCenter - Position;
        return Mathf.Abs(delta.X) < LinkBlockingRadius &&
            Mathf.Abs(delta.Y) < LinkBlockingRadius;
    }

    public bool CanTalkTo(Player player)
    {
        if (!Active || TextId == 0 || string.IsNullOrEmpty(Message))
            return false;
        Vector2 talkPoint = player.Position + (Vector2)player.FacingVector * 8.0f;
        return InteractionBounds.HasPoint(talkPoint);
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
        ZIndex = Position.Y > linkPosition.Y + LinkPriorityYOffset
            ? InFrontOfLinkZIndex
            : BehindLinkZIndex;
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        if (_transitionDrawOffset.IsEqualApprox(offset))
            return;

        _transitionDrawOffset = offset;
        QueueRedraw();
    }

    internal void SetScriptAnimation(string encodedAnimation)
    {
        _scriptAnimation.Clear();
        _scriptAnimation.AddRange(BuildPositionedAnimation(
            _sourceImage, encodedAnimation, Record.TileBase, Record.Palette, _paletteOverride));
        _scriptAnimationActive = _scriptAnimation.Count > 0;
        _animationFrame = 0;
        _animationTicks = 0.0;
        QueueRedraw();
    }

    internal void SetFacingDirection(Vector2I direction)
    {
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
        _scriptAnimationActive = false;
        _animationFrame = 0;
        _animationTicks = 0.0;
        QueueRedraw();
    }

    internal void SetBlocksLink(bool blocksLink) => _blocksLink = blocksLink;

    internal void SetAnimationRate(float rate)
    {
        _animationRate = Math.Max(0.0f, rate);
    }

    internal void SetSpritePalette(Color[] palette)
    {
        if (palette.Length != 4)
            throw new ArgumentException("A GBC OBJ palette must contain four colors.", nameof(palette));
        _paletteOverride = (Color[])palette.Clone();
        RebuildFacingAnimations();
        _scriptAnimation.Clear();
        _scriptAnimationActive = false;
        _animationFrame = 0;
        _animationTicks = 0.0;
        QueueRedraw();
    }

    internal void AppendScriptGraphics(string spriteName)
    {
        byte[] bytes = FileAccess.GetFileAsBytes($"res://assets/oracle/gfx/{spriteName}.png");
        Image extra = new();
        extra.LoadPngFromBuffer(bytes);
        _sourceImage.Convert(Image.Format.Rgba8);
        extra.Convert(Image.Format.Rgba8);
        Image combined = Image.CreateEmpty(
            _sourceImage.GetWidth() + extra.GetWidth(),
            Math.Max(_sourceImage.GetHeight(), extra.GetHeight()),
            false,
            Image.Format.Rgba8);
        combined.BlitRect(
            _sourceImage,
            new Rect2I(0, 0, _sourceImage.GetWidth(), _sourceImage.GetHeight()),
            Vector2I.Zero);
        combined.BlitRect(
            extra,
            new Rect2I(0, 0, extra.GetWidth(), extra.GetHeight()),
            new Vector2I(_sourceImage.GetWidth(), 0));
        _sourceImage = combined;
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
            DrawTexture(frame.Texture, frame.Offset + _transitionDrawOffset);
        }
    }

    private void AdvanceAnimation(double delta)
    {
        List<AnimationFrame> animation = CurrentAnimation;
        if (animation.Count <= 1)
            return;
        _animationTicks += delta * 60.0 * _animationRate;
        while (_animationTicks >= animation[_animationFrame].Duration)
        {
            _animationTicks -= animation[_animationFrame].Duration;
            _animationFrame = (_animationFrame + 1) % animation.Count;
            QueueRedraw();
        }
    }

    private List<AnimationFrame> CurrentAnimation => _scriptAnimationActive
        ? _scriptAnimation
        : _facingAnimations[(int)_facing];

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
        _facingAnimations[(int)Facing.Up].AddRange(BuildAnimation(
            _sourceImage, Record.UpAnimation, Record.TileBase, Record.Palette, _paletteOverride));
        _facingAnimations[(int)Facing.Right].AddRange(BuildAnimation(
            _sourceImage, Record.RightAnimation, Record.TileBase, Record.Palette, _paletteOverride));
        _facingAnimations[(int)Facing.Down].AddRange(BuildAnimation(
            _sourceImage, Record.DownAnimation, Record.TileBase, Record.Palette, _paletteOverride));
        _facingAnimations[(int)Facing.Left].AddRange(BuildAnimation(
            _sourceImage, Record.LeftAnimation, Record.TileBase, Record.Palette, _paletteOverride));
    }

    private static IEnumerable<AnimationFrame> BuildAnimation(
        Image source,
        string encodedAnimation,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride)
    {
        foreach (string encodedFrame in encodedAnimation.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = encodedFrame.IndexOf('@');
            if (separator < 0 || !int.TryParse(encodedFrame[..separator], out int duration))
                continue;
            yield return new AnimationFrame(
                BuildOamTexture(
                    source, encodedFrame[(separator + 1)..], tileBase, basePalette, paletteOverride),
                Math.Max(1, duration),
                new Vector2(-16, -16));
        }
    }

    private static IEnumerable<AnimationFrame> BuildPositionedAnimation(
        Image source,
        string encodedAnimation,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride)
    {
        foreach (string encodedFrame in encodedAnimation.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = encodedFrame.IndexOf('@');
            if (separator < 0 || !int.TryParse(encodedFrame[..separator], out int duration))
                continue;
            (Texture2D texture, Vector2 offset) = BuildPositionedOamTexture(
                source, encodedFrame[(separator + 1)..], tileBase, basePalette, paletteOverride);
            yield return new AnimationFrame(texture, Math.Max(1, duration), offset);
        }
    }

    internal static Texture2D BuildOamTexture(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride = null)
    {
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
            int sourceX = (((tileBase + tile) & 0xfe) / 2) * 8;
            int destinationX = oamX + 8;
            int destinationY = oamY;
            bool flipX = (flags & 0x20) != 0;
            bool flipY = (flags & 0x40) != 0;
            int palette = basePalette ^ (flags & 0x07);

            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int readX = sourceX + (flipX ? 7 - x : x);
                int readY = flipY ? 15 - y : y;
                int writeX = destinationX + x;
                int writeY = destinationY + y;
                if (readX < 0 || readX >= source.GetWidth() || readY < 0 || readY >= source.GetHeight() ||
                    writeX < 0 || writeX >= output.GetWidth() || writeY < 0 || writeY >= output.GetHeight())
                    continue;
                Color pixel = RecolorSpritePixel(
                    source.GetPixel(readX, readY), palette, paletteOverride);
                if (pixel.A > 0.1f)
                    output.SetPixel(writeX, writeY, pixel);
            }
        }

        return ImageTexture.CreateFromImage(output);
    }

    private static (Texture2D Texture, Vector2 Offset) BuildPositionedOamTexture(
        Image source,
        string encodedOam,
        int tileBase,
        int basePalette,
        Color[]? paletteOverride)
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
            return (BuildOamTexture(
                source, encodedOam, tileBase, basePalette, paletteOverride), new Vector2(-16, -16));

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
            int sourceX = (((tileBase + tile) & 0xfe) / 2) * 8;
            int destinationX = oamX + 8 - minX;
            int destinationY = oamY - minY;
            bool flipX = (flags & 0x20) != 0;
            bool flipY = (flags & 0x40) != 0;
            int palette = basePalette ^ (flags & 0x07);

            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int readX = sourceX + (flipX ? 7 - x : x);
                int readY = flipY ? 15 - y : y;
                if (readX < 0 || readX >= source.GetWidth() || readY < 0 || readY >= source.GetHeight())
                    continue;
                Color pixel = RecolorSpritePixel(
                    source.GetPixel(readX, readY), palette, paletteOverride);
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
        Color[]? paletteOverride)
    {
        if (source.A < 0.1f || source.R < 0.1f)
            return Colors.Transparent;
        int color = source.R < 0.5f ? 1 : source.R < 0.9f ? 2 : 3;
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

    private static Color GbcColor(int red, int green, int blue)
    {
        return new Color(red / 31.0f, green / 31.0f, blue / 31.0f);
    }
}
