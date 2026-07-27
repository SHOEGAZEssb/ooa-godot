using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// PART_BALL $38: one shared-RNG speed decision, sword knockback reflection,
/// two side probes for target collisions, and the original 8.8 speed bytes.
/// </summary>
internal sealed partial class ShootingGalleryBall : TransitionOffsetNode2D
{
    private ShootingGalleryEventDatabase _database = null!;
    private ShootingGalleryEventRecord _record;
    private ShootingGallerySession _session = null!;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private Action<int> _playSound = null!;
    private Func<long> _animationTick = null!;
    private Texture2D _texture = null!;
    private ShootingGalleryBallState _state;
    private int _angle;
    private int _speed;
    private int _collisionCounter;

    internal bool Finished { get; private set; }
    internal ShootingGalleryBallState State => _state;
    internal int Angle => _angle;
    internal int Speed => _speed;
    internal int CollisionCounter => _collisionCounter;
    internal int ElapsedUpdates { get; private set; }
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(_record.BallRadiusX, _record.BallRadiusY),
        new Vector2(_record.BallRadiusX * 2, _record.BallRadiusY * 2));

    internal void Initialize(
        ShootingGalleryEventDatabase database,
        ShootingGallerySession session,
        OracleRoomData room,
        OracleRandom random,
        Vector2 position,
        Action<int> playSound,
        Func<long> animationTick)
    {
        _database = database;
        _record = database.Record;
        _session = session;
        _room = room;
        _random = random;
        _playSound = playSound;
        _animationTick = animationTick;
        Position = position;
        _angle = _record.BallAngle;

        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{_record.BallSprite}.png");
        AnimationFrameDefinition[] frames =
            OracleGraphicsCache.GetAnimationDefinition(
                _record.BallAnimation).Frames;
        if (frames.Length != 1)
            throw new InvalidOperationException(
                "PART_BALL $38 requires its one-frame imported animation.");
        _texture = NpcCharacter.BuildOamTexture(
            source,
            frames[0].EncodedOam,
            _record.BallTileBase,
            _record.BallPalette);
        QueueRedraw();
    }

    internal void UpdateFrame(ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        ElapsedUpdates++;

        if (_collisionCounter > 0)
        {
            _collisionCounter--;
            return;
        }

        switch (_state)
        {
            case ShootingGalleryBallState.Initializing:
                _state = ShootingGalleryBallState.Incoming;
                bool slow = (_random.Next().Value & 0x0f) < 4;
                _speed = slow
                    ? _record.BallSlowSpeed
                    : _record.BallFastSpeed;
                _playSound(slow
                    ? _record.SlowSound
                    : _record.ThrowSound);
                return;

            case ShootingGalleryBallState.Incoming:
                UpdateIncoming();
                return;

            case ShootingGalleryBallState.DeflectionPending:
                _state = ShootingGalleryBallState.Reflected;
                _speed = _record.BallReflectedSpeed;
                _collisionCounter = 0;
                _playSound(_record.ClinkSound);
                return;

            case ShootingGalleryBallState.Reflected:
                UpdateReflected(spawns);
                return;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    internal bool Deflect(Rect2 hitbox, Vector2 sourcePosition)
    {
        if (Finished ||
            _state is ShootingGalleryBallState.DeflectionPending or
                ShootingGalleryBallState.Reflected ||
            !hitbox.Intersects(CollisionBounds))
        {
            return false;
        }

        _state = ShootingGalleryBallState.DeflectionPending;
        _angle = OracleObjectMath.AngleToward(sourcePosition, Position);
        return true;
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _texture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }

    private void UpdateIncoming()
    {
        if (!WithinScreen(Position))
        {
            Finish(strike: false);
            return;
        }

        // @func_6b00 accepts only collision value $0f. The launcher tile uses
        // partial collision $0a: the ball occupies a solid quarter while
        // emerging, but the source deliberately ignores that shape and keeps
        // moving. The closed entrance uses $0f and produces the strike.
        if (WithinRoom(Position) &&
            _room.GetTerrainInfo(Position).Collision == 0x0f)
        {
            _playSound(_record.StrikeSound);
            Finish(strike: true);
            return;
        }

        Position += OracleObjectMath.CardinalVector(_angle) *
            (_speed / 40.0f);
        QueueRedraw();
    }

    private void UpdateReflected(ICollection<RoomEntitySpawn> spawns)
    {
        if (!WithinScreen(Position))
        {
            Finish(strike: false);
            return;
        }

        bool hit = TryHitTarget(Position + Vector2.Left, spawns);
        hit |= TryHitTarget(Position + Vector2.Right, spawns);
        if (hit)
            _collisionCounter = 3;

        Vector2 destination = Position +
            OracleObjectMath.VectorFromAngle32(_angle) * (_speed / 40.0f);
        if (!WithinScreen(destination) ||
            (WithinRoom(destination) && _room.IsSolid(destination)))
        {
            Position = destination;
            QueueRedraw();
            return;
        }

        Position = destination;
        QueueRedraw();
    }

    private bool TryHitTarget(
        Vector2 point,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_session.HitCount >= 2 || !WithinRoom(point))
            return false;
        byte tile = _room.GetMetatile(point);
        int type = _record.TargetType(tile);
        if (type < 0)
            return false;

        int previousHits = _session.HitCount;
        _session.RecordTargetHit(type);
        _room.SetPositionTileAndCollision(
            PackedPositionCenter(_room.GetPackedPosition(point)),
            (byte)_record.FloorTile,
            null,
            _animationTick());
        if (previousHits == 0)
            _playSound(_record.SwitchSound);
        for (int angle = 0; angle < _database.Debris.Count; angle++)
        {
            spawns.Add(new ShootingGalleryTargetDebrisSpawn(
                PackedPositionCenter(_room.GetPackedPosition(point)),
                type,
                angle));
        }
        return true;
    }

    private void Finish(bool strike)
    {
        if (Finished)
            return;
        if (!strike && _session.HitCount == 0)
            _playSound(_record.ErrorSound);
        _session.FinishBall(strike);
        Finished = true;
        Visible = false;
        QueueRedraw();
    }

    private bool WithinScreen(Vector2 position) =>
        position.X >= 0 &&
        position.X < OracleRoomData.ViewportWidth &&
        position.Y >= 0 &&
        position.Y < OracleRoomData.ViewportHeight;

    private bool WithinRoom(Vector2 position) =>
        position.X >= 0 &&
        position.X < _room.Width &&
        position.Y >= 0 &&
        position.Y < _room.Height;

    private static Vector2 PackedPositionCenter(int packed) => new(
        (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packed >> 4) * OracleRoomData.MetatileSize + 8);
}

internal sealed class ShootingGalleryBallRoomEntity(
    ShootingGalleryBall ball)
    : RoomEntityAdapter<ShootingGalleryBall>(
        ball, ball.SetTransitionDrawOffset),
        IFixedRoomEntity, ISwordHittableRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        Entity.UpdateFrame(spawns);
    }

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = damage;
        _ = knockbackStrength;
        _ = spawns;
        return Entity.Deflect(hitbox, sourcePosition);
    }
}

internal enum ShootingGalleryBallState
{
    Initializing,
    Incoming,
    DeflectionPending,
    Reflected
}
