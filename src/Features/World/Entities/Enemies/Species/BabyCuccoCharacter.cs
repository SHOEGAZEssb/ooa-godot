using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ENEMY_BABY_CUCCO $33:$00. These non-combat enemies follow Link until
/// close, consume the shared RNG once per near update for their 1-in-64 hop,
/// and use the common weight-0 bracelet proxy while carried and thrown.
/// </summary>
internal partial class BabyCuccoCharacter : EnemyCharacter
{
    private readonly BabyCuccoBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.BabyCucco;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private EnemyTerrainMovement _movement = null!;
    private BraceletDatabaseRecord _bracelet;
    private BombRecord _throwing = null!;
    private Action<int> _soundRequested = null!;
    private Action<Rect2, int, int, int> _applyThrownObjectHit = null!;
    private BabyCuccoState _state;
    private int _angle;
    private CarriedObjectMotion _carried;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal BabyCuccoState State => _state;
    internal int Angle => _angle;
    internal int ZFixed => _carried.ZFixed;
    internal int SpeedZ => _carried.SpeedZ;
    internal Vector2I ThrowDirection => _carried.Direction;
    internal int ThrowSpeedRaw => _carried.SpeedRaw;
    internal int CurrentAnimationFrame => AnimationFrame;
    internal ulong CurrentAnimationPixelHash =>
        OracleGraphicsCache.PixelHash(CurrentAnimationTexture.GetImage());
    internal override bool CollisionEnabled => false;
    protected override Vector2 AnimationDrawOffset =>
        Animation.CurrentOffset + Vector2.Down * (_carried.ZFixed >> 8);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        BraceletDatabaseRecord bracelet,
        BombRecord throwing,
        Action<int> soundRequested,
        Action<Rect2, int, int, int> applyThrownObjectHit)
    {
        if (record.Id != 0x33 || record.SubId != 0x00 ||
            record.Animations.Length != 2)
        {
            throw new InvalidOperationException(
                $"BabyCuccoCharacter requires ENEMY_BABY_CUCCO $33:$00, " +
                $"got ${record.Id:x2}:${record.SubId:x2}.");
        }
        Record = record;
        _room = room;
        _random = random;
        _bracelet = bracelet;
        _throwing = throwing;
        _soundRequested = soundRequested;
        _applyThrownObjectHit = applyThrownObjectHit;
        _movement = new EnemyTerrainMovement(this, room);
        _state = BabyCuccoState.Uninitialized;
        _angle = 0;
        _carried = new CarriedObjectMotion(position);
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        Visible = false;
    }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        if (IsDead)
            return;

        switch (_state)
        {
            case BabyCuccoState.Uninitialized:
                _state = BabyCuccoState.Following;
                Visible = true;
                QueueRedraw();
                return;

            case BabyCuccoState.Following:
                UpdatePriority(player);
                _angle = OracleObjectMovement.Shared.RelativeAngle(
                    OracleObjectMath.ToPixelPosition(Position),
                    OracleObjectMath.ToPixelPosition(player.Position));
                SetAnimation(_angle < _behavior.AnimationAngleThreshold ? 1 : 0);
                if (ManhattanDistance(player.Position) <
                    _behavior.ProximityDistance)
                {
                    if ((_random.Next().Value & _behavior.RandomHopMask) == 0)
                    {
                        _state = BabyCuccoState.Hopping;
                        _carried.SpeedZ = _behavior.HopSpeedZ;
                    }
                    return;
                }
                _movement.MoveUsingAdjacentWalls(
                    _angle,
                    _behavior.SpeedRaw,
                    allowHoles: false,
                    topDown: false);
                AdvanceAnimation();
                return;

            case BabyCuccoState.Hopping:
                if (OracleObjectMath.UpdateSpeedZ(
                    ref _carried.ZFixed,
                    ref _carried.SpeedZ,
                    _behavior.HopGravity))
                {
                    _carried.ZFixed = 0;
                    _state = BabyCuccoState.Following;
                    QueueRedraw();
                    return;
                }
                AdvanceAnimation();
                QueueRedraw();
                return;

            case BabyCuccoState.Held:
                UpdateHeld(player);
                return;

            case BabyCuccoState.Thrown:
                UpdateThrown();
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Baby Cucco state {_state}.");
        }
    }

    internal bool TryUseBracelet(Player player, Vector2I releaseDirection)
    {
        if (_state == BabyCuccoState.Held)
        {
            Release(player, releaseDirection);
            return true;
        }
        if (_state != BabyCuccoState.Following ||
            player.IsCarryingObject || player.CutsceneControlled)
        {
            return false;
        }

        Vector2 point =
            player.Position + (Vector2)player.FacingVector * 6.0f;
        Vector2 delta = Position - point;
        if (Mathf.Abs(delta.X) >= 13 || Mathf.Abs(delta.Y) >= 13)
            return false;

        _state = BabyCuccoState.Held;
        player.BeginCarriedObjectPose();
        RestartAnimation(HeldAnimationIndex(player.FacingVector));
        UpdateHeld(player, justGrabbed: true);
        return true;
    }

    private void UpdateHeld(Player player, bool justGrabbed = false)
    {
        _carried.Hold(player);
        Position = _carried.GroundPosition;
        ZIndex = 11;
        int animation = HeldAnimationIndex(player.FacingVector);
        if (AnimationIndex != animation)
            RestartAnimation(animation);
        else if (!justGrabbed)
            AdvanceAnimation();
        QueueRedraw();
    }

    private void Release(Player player, Vector2I releaseDirection)
    {
        _state = BabyCuccoState.Thrown;
        _carried.Release(player, releaseDirection, _bracelet);
        Position = OracleObjectMath.ToPixelPosition(_carried.GroundPosition);
        QueueRedraw();
    }

    private void UpdateThrown()
    {
        _carried.AdvanceHorizontal(
            _throwing,
            edge => WithinRoom(edge) && _room.IsSolid(edge));
        bool landed = _carried.AdvanceVertical(_bracelet);
        Position = OracleObjectMath.ToPixelPosition(_carried.GroundPosition);
        if (!WithinRoom(Position))
        {
            Finish();
            return;
        }

        _applyThrownObjectHit(
            CarriedObjectMotion.CollisionBounds(Position, _bracelet),
            _carried.ZFixed >> 8,
            _bracelet.CollisionZRadius,
            _bracelet.Damage);

        if (!landed)
        {
            AdvanceAnimation();
            QueueRedraw();
            return;
        }

        _soundRequested(_throwing.LandingSound);
        if (!_carried.Bounce(_throwing))
        {
            _state = BabyCuccoState.Following;
            ZIndex = 10;
        }
        else
        {
            AdvanceAnimation();
        }
        QueueRedraw();
    }

    private void UpdatePriority(Player player) =>
        ZIndex = Position.Y > player.Position.Y + 0x0b ? 11 : 9;

    private int ManhattanDistance(Vector2 target)
    {
        Vector2 source = OracleObjectMath.ToPixelPosition(Position);
        target = OracleObjectMath.ToPixelPosition(target);
        return Mathf.Abs((int)target.X - (int)source.X) +
            Mathf.Abs((int)target.Y - (int)source.Y);
    }

    private static int HeldAnimationIndex(Vector2I facing) =>
        facing is { Y: < 0 } or { X: > 0 } ? 1 : 0;

    private bool WithinRoom(Vector2 point) =>
        point.X >= 0 && point.X < _room.Width &&
        point.Y >= 0 && point.Y < _room.Height;

}

internal enum BabyCuccoState
{
    Uninitialized = 0,
    Held = 2,
    Following = 8,
    Hopping = 9,
    Thrown = 10
}
