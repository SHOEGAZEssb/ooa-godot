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

    private sealed record AnimationFrame(Texture2D Texture, int Duration);

    private readonly List<AnimationFrame>[] _facingAnimations =
    {
        new(), new(), new(), new()
    };
    private Facing _facing = Facing.Down;
    private int _animationFrame;
    private double _animationTicks;
    private double _faceCooldownFrames;
    private Vector2 _transitionDrawOffset;

    public NpcDatabase.NpcRecord Record { get; private set; }
    public string Message => Record.Message;
    public int TextId => Record.TextId;
    public const float CollisionRadius = 6.0f;
    public const float LinkCollisionRadius = 6.0f;
    public const float LinkBlockingRadius = CollisionRadius + LinkCollisionRadius;
    public Rect2 SpriteBounds => new(Position + new Vector2(-8, -8), new Vector2(16, 16));
    public int CurrentFrameColumn => GetFrameColumn();
    public int CurrentAnimationFrame => _animationFrame;
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
        _facingAnimations[(int)Facing.Up].AddRange(BuildAnimation(image, record.UpAnimation, record.TileBase, record.Palette));
        _facingAnimations[(int)Facing.Right].AddRange(BuildAnimation(image, record.RightAnimation, record.TileBase, record.Palette));
        _facingAnimations[(int)Facing.Down].AddRange(BuildAnimation(image, record.DownAnimation, record.TileBase, record.Palette));
        _facingAnimations[(int)Facing.Left].AddRange(BuildAnimation(image, record.LeftAnimation, record.TileBase, record.Palette));
        Position = new Vector2(record.X, record.Y);
        QueueRedraw();
    }

    public bool BlocksLinkCenter(Vector2 linkCenter)
    {
        Vector2 delta = linkCenter - Position;
        return Mathf.Abs(delta.X) < LinkBlockingRadius &&
            Mathf.Abs(delta.Y) < LinkBlockingRadius;
    }

    public bool CanTalkTo(Player player)
    {
        if (TextId == 0 || string.IsNullOrEmpty(Message))
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

    public override void _Draw()
    {
        List<AnimationFrame> animation = _facingAnimations[(int)_facing];
        if (animation.Count > 0)
            DrawTexture(
                animation[_animationFrame % animation.Count].Texture,
                new Vector2(-16, -16) + _transitionDrawOffset);
    }

    private void AdvanceAnimation(double delta)
    {
        List<AnimationFrame> animation = _facingAnimations[(int)_facing];
        if (animation.Count <= 1)
            return;
        _animationTicks += delta * 60.0;
        while (_animationTicks >= animation[_animationFrame].Duration)
        {
            _animationTicks -= animation[_animationFrame].Duration;
            _animationFrame = (_animationFrame + 1) % animation.Count;
            QueueRedraw();
        }
    }

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

    private static IEnumerable<AnimationFrame> BuildAnimation(Image source, string encodedAnimation, int tileBase, int basePalette)
    {
        foreach (string encodedFrame in encodedAnimation.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = encodedFrame.IndexOf('@');
            if (separator < 0 || !int.TryParse(encodedFrame[..separator], out int duration))
                continue;
            yield return new AnimationFrame(
                BuildOamTexture(source, encodedFrame[(separator + 1)..], tileBase, basePalette),
                Math.Max(1, duration));
        }
    }

    internal static Texture2D BuildOamTexture(Image source, string encodedOam, int tileBase, int basePalette)
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
                Color pixel = RecolorSpritePixel(source.GetPixel(readX, readY), palette);
                if (pixel.A > 0.1f)
                    output.SetPixel(writeX, writeY, pixel);
            }
        }

        return ImageTexture.CreateFromImage(output);
    }

    private static int ToSignedByte(int value) => value >= 0x80 ? value - 0x100 : value;

    private static Color RecolorSpritePixel(Color source, int palette)
    {
        if (source.A < 0.1f || source.R < 0.1f)
            return Colors.Transparent;
        int color = source.R < 0.5f ? 1 : source.R < 0.9f ? 2 : 3;
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
