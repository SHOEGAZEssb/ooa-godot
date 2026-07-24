using Godot;

namespace oracleofages;

/// <summary>PART_ENEMY_ARROW $1a fired by Moblin archers.</summary>
public partial class EnemyArrowProjectile : TransitionOffsetNode2D
{
    private const int BounceSpeedZ = -0xe0;
    private const int BounceGravity = 0x0e;
    private const int BounceFrames = 0x20;
    private static readonly Vector2[] SpawnOffsets =
    [
        new(-5, -8), new(8, 2), new(5, 8), new(-8, 2)
    ];
    private static readonly Vector2[] CollisionRadii =
    [
        new(3, 6), new(6, 3), new(3, 6), new(6, 3)
    ];

    private EnemyArrowRecord _record;
    private OracleRoomData _room = null!;
    private Texture2D _texture = null!;
    private Texture2D _bounceTexture = null!;
    private int _angle;
    private Vector2 _radii;
    private ArrowState _state;
    private int _counter;
    private int _zFixed;
    private int _speedZ;

    public bool Finished { get; private set; }
    public Rect2 CollisionBounds => new(Position - _radii, _radii * 2.0f);
    internal ArrowState State => _state;
    internal int Counter => _counter;
    internal int ZFixed => _zFixed;
    internal int ElapsedFrames { get; private set; }

    internal void Initialize(
        EnemyArrowRecord record,
        OracleRoomData room,
        Vector2 position,
        int angle)
    {
        _record = record;
        _room = room;
        _angle = angle & 0x18;
        int direction = _angle / 8;
        Position = position + SpawnOffsets[direction];
        _radii = CollisionRadii[direction];
        string animation = direction switch
        {
            0 => record.UpAnimation,
            1 => record.RightAnimation,
            2 => record.DownAnimation,
            _ => record.LeftAnimation
        };
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        AnimationFrameDefinition frame =
            OracleGraphicsCache.GetAnimationDefinition(animation).Frames[0];
        _texture = NpcCharacter.BuildOamTexture(
            source, frame.EncodedOam, record.TileBase, record.Palette);
        AnimationFrameDefinition bounceFrame =
            OracleGraphicsCache.GetAnimationDefinition(
                record.BounceAnimation).Frames[0];
        _bounceTexture = NpcCharacter.BuildOamTexture(
            source, bounceFrame.EncodedOam, record.TileBase, record.Palette);
        _state = ArrowState.Initializing;
        ElapsedFrames = 0;
        QueueRedraw();
    }

    internal void UpdateFrame(Player player)
    {
        if (Finished)
            return;
        ElapsedFrames++;
        if (_state == ArrowState.Initializing)
        {
            // PART_ENEMY_ARROW state 0 applies its direction-specific offset,
            // radii, animation, and visibility without moving.
            _state = ArrowState.Flying;
            return;
        }
        if (_state == ArrowState.Bouncing)
        {
            if (--_counter == 0)
            {
                Finish();
                return;
            }
            OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, BounceGravity);
            Position += OracleObjectMath.CardinalVector(_angle) * 0.25f;
            QueueRedraw();
            return;
        }
        if (player.TryBlockWithShield(CollisionBounds))
        {
            BeginBounce();
            return;
        }
        if (CollisionBounds.Intersects(new Rect2(
            player.Position - Vector2.One * 6, Vector2.One * 12)))
        {
            player.ApplyEnemyContactDamage(Position, _record.DamageQuarters);
            Finish();
            return;
        }
        if (!WithinVisibleBoundary(player.Position) ||
            Position.X < 0 || Position.X >= _room.Width ||
            Position.Y < 0 || Position.Y >= _room.Height)
        {
            Finish();
            return;
        }
        if (_room.IsSolid(Position))
        {
            BeginBounce();
            return;
        }
        Position += OracleObjectMath.CardinalVector(_angle) *
            (_record.SpeedRaw / 40.0f);
        QueueRedraw();
    }

    internal bool DeflectWithSword()
    {
        if (Finished)
            return false;
        BeginBounce();
        return true;
    }

    public override void _Draw()
    {
        if (!Finished)
            DrawTexture(
                _state == ArrowState.Bouncing ? _bounceTexture : _texture,
                new Vector2(-16, -16 + (_zFixed >> 8)) + TransitionDrawOffset);
    }

    private void BeginBounce()
    {
        _state = ArrowState.Bouncing;
        _radii = Vector2.Zero;
        _counter = BounceFrames;
        _speedZ = BounceSpeedZ;
        _angle ^= 0x10;
        QueueRedraw();
    }

    private bool WithinVisibleBoundary(Vector2 linkPosition)
    {
        float maxCameraX = Mathf.Max(0.0f,
            _room.Width - OracleRoomData.ViewportWidth);
        float maxCameraY = Mathf.Max(0.0f,
            _room.Height - OracleRoomData.ViewportHeight);
        Vector2 cameraOrigin = new(
            Mathf.Clamp(linkPosition.X - OracleRoomData.ViewportWidth / 2.0f,
                0.0f, maxCameraX),
            Mathf.Clamp(linkPosition.Y - OracleRoomData.ViewportHeight / 2.0f,
                0.0f, maxCameraY));
        return OracleObjectMath.IsInsideOriginalScreenBoundary(
            Position - cameraOrigin);
    }

    private void Finish()
    {
        Finished = true;
        Visible = false;
    }
}

internal enum ArrowState
{
    Initializing,
    Flying,
    Bouncing
}
