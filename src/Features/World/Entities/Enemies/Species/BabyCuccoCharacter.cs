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
    private int _zFixed;
    private int _speedZ;
    private Vector2 _throwPrecise;
    private Vector2I _throwDirection;
    private int _throwSpeedRaw;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal BabyCuccoState State => _state;
    internal int Angle => _angle;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal Vector2I ThrowDirection => _throwDirection;
    internal int ThrowSpeedRaw => _throwSpeedRaw;
    internal ulong CurrentAnimationPixelHash =>
        OracleGraphicsCache.PixelHash(CurrentAnimationTexture.GetImage());
    internal override bool CollisionEnabled => false;
    protected override Vector2 AnimationDrawOffset =>
        Animation.CurrentOffset + Vector2.Down * (_zFixed >> 8);

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
        _zFixed = 0;
        _speedZ = 0;
        _throwPrecise = position;
        _throwDirection = Vector2I.Zero;
        _throwSpeedRaw = 0;
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
                        _speedZ = _behavior.HopSpeedZ;
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
                    ref _zFixed, ref _speedZ, _behavior.HopGravity))
                {
                    _zFixed = 0;
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
        SetHeldAnimation(player.FacingVector);
        UpdateHeld(player);
        return true;
    }

    private void UpdateHeld(Player player)
    {
        Vector2I offset = HeldOffset(player);
        Position = player.Position + new Vector2(offset.X, 0);
        _zFixed = offset.Y << 8;
        ZIndex = 11;
        SetHeldAnimation(player.FacingVector);
        QueueRedraw();
    }

    private void Release(Player player, Vector2I releaseDirection)
    {
        Vector2I offset = HeldOffset(player);
        _state = BabyCuccoState.Thrown;
        _throwDirection = releaseDirection;
        _throwPrecise =
            player.Position + new Vector2(offset.X, 0) + player.FacingVector;
        Position = OracleObjectMath.ToPixelPosition(_throwPrecise);
        _zFixed = offset.Y << 8;
        _speedZ = releaseDirection == Vector2I.Zero
            ? 0
            : _bracelet.InitialSpeedZ;
        _throwSpeedRaw = releaseDirection == Vector2I.Zero
            ? 0
            : RingEffects.UsesStrongThrow(player.Inventory)
                ? _bracelet.TossSpeedRaw
                : _bracelet.SpeedRaw;
        player.EndCarriedObjectPose();
        QueueRedraw();
    }

    private void UpdateThrown()
    {
        if (_throwDirection != Vector2I.Zero)
        {
            Vector2 edge = _throwPrecise +
                _throwing.EdgeOffset(_throwDirection);
            if (WithinRoom(edge) && _room.IsSolid(edge))
            {
                _throwDirection = Vector2I.Zero;
                _throwSpeedRaw = 0;
            }
            else
            {
                OracleObjectMovement.Shared.ApplySpeed(
                    ref _throwPrecise,
                    _throwSpeedRaw,
                    DirectionAngle(_throwDirection));
            }
        }

        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _bracelet.Gravity);
        Position = OracleObjectMath.ToPixelPosition(_throwPrecise);
        if (!WithinRoom(Position))
        {
            Finish();
            return;
        }

        _applyThrownObjectHit(
            new Rect2(
                Position - new Vector2(
                    _bracelet.RadiusX,
                    _bracelet.RadiusY),
                new Vector2(
                    _bracelet.RadiusX * 2,
                    _bracelet.RadiusY * 2)),
            _zFixed >> 8,
            _bracelet.CollisionZRadius,
            _bracelet.Damage);

        if (!landed)
        {
            AdvanceAnimation();
            QueueRedraw();
            return;
        }

        _soundRequested(_throwing.LandingSound);
        int rebound = (-_speedZ) >> 1;
        if (rebound > -0x80)
        {
            _zFixed = 0;
            _speedZ = 0;
            _throwSpeedRaw = 0;
            _throwDirection = Vector2I.Zero;
            _state = BabyCuccoState.Following;
            ZIndex = 10;
        }
        else
        {
            _speedZ = rebound;
            _throwSpeedRaw = _throwing.ReducedBounceSpeed(_throwSpeedRaw);
            if (_throwSpeedRaw == 0)
                _throwDirection = Vector2I.Zero;
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

    private void SetHeldAnimation(Vector2I facing) =>
        SetAnimation(facing is { Y: < 0 } or { X: > 0 } ? 1 : 0);

    private static Vector2I HeldOffset(Player player) =>
        player.BraceletEntityOffset ??
            new Vector2I(
                0,
                player.CarriedObjectAnimationFrame == 0 &&
                    player.FacingVector.X != 0 ? -14 : -13);

    private bool WithinRoom(Vector2 point) =>
        point.X >= 0 && point.X < _room.Width &&
        point.Y >= 0 && point.Y < _room.Height;

    private static int DirectionAngle(Vector2I direction) =>
        direction == Vector2I.Up ? 0x00
        : direction == Vector2I.Right ? 0x08
        : direction == Vector2I.Down ? 0x10
        : direction == Vector2I.Left ? 0x18
        : throw new ArgumentOutOfRangeException(nameof(direction));
}

internal enum BabyCuccoState
{
    Uninitialized = 0,
    Held = 2,
    Following = 8,
    Hopping = 9,
    Thrown = 10
}
