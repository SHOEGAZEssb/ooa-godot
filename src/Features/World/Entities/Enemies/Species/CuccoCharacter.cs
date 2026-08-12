using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ENEMY_CUCCO $36:$00. The ordinary adult Cucco idles on a shared-RNG
/// 1-in-64 roll, hops along its chosen 32-step angle, can be carried, and
/// runs away indefinitely after an accepted hit. Sixteen hits enable the
/// source PART_CUCCO_ATTACKER cadence.
/// </summary>
internal partial class CuccoCharacter : EnemyCharacter
{
    private readonly CuccoBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Cucco;
    private readonly GiantCuccoBehaviorProfile _giantBehavior =
        EnemyBehaviorTables.Shared.GiantCucco;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private EnemyTerrainMovement _movement = null!;
    private BraceletDatabaseRecord _bracelet;
    private BombRecord _throwing = null!;
    private Action<int> _soundRequested = null!;
    private Action<int> _screenShakeRequested = null!;
    private Action<Rect2, int, int, int> _applyThrownObjectHit = null!;
    private ImportedEnemyDefinition _giantRecord;
    private CuccoState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _zFixed;
    private int _speedZ;
    private int _hitCount;
    private int _revengeCounter;
    private int _heldSoundCounter;
    private int _transformationCounter;
    private Vector2 _throwPrecise;
    private Vector2I _throwDirection;
    private int _throwSpeedRaw;
    private bool _giant;
    private bool _giantCollisionDisabled;
    private bool _transformToGiant;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal CuccoState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int Z => _zFixed >> 8;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int HitCount => _hitCount;
    internal int RevengeCounter => _revengeCounter;
    internal Vector2I ThrowDirection => _throwDirection;
    internal int ThrowSpeedRaw => _throwSpeedRaw;
    internal bool IsGiant => _giant;
    internal int CurrentAnimationFrame => AnimationFrame;
    internal Vector2 CurrentAnimationDrawOffset => AnimationDrawOffset;
    internal ulong CurrentAnimationPixelHash =>
        OracleGraphicsCache.PixelHash(CurrentAnimationTexture.GetImage());
    internal override bool CollisionEnabled =>
        base.CollisionEnabled &&
        !_giantCollisionDisabled &&
        _state is not (
            CuccoState.Held or CuccoState.Thrown or CuccoState.Transforming);
    protected override Vector2 AnimationDrawOffset =>
        Animation.CurrentOffset + Vector2.Down * (_zFixed >> 8);

    internal void Initialize(
        ImportedEnemyDefinition record,
        ImportedEnemyDefinition giantRecord,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        BraceletDatabaseRecord bracelet,
        BombRecord throwing,
        Action<int> soundRequested,
        Action<int> screenShakeRequested,
        Action<Rect2, int, int, int> applyThrownObjectHit)
    {
        if (record.Id != 0x36 || record.SubId != 0x00 ||
            record.Animations.Length != 2)
        {
            throw new InvalidOperationException(
                $"CuccoCharacter requires ENEMY_CUCCO $36:$00, got " +
                $"${record.Id:x2}:${record.SubId:x2}.");
        }
        if (giantRecord.Id != 0x3b || giantRecord.SubId != 0x00 ||
            giantRecord.Animations.Length != 2)
        {
            throw new InvalidOperationException(
                $"CuccoCharacter requires ENEMY_GIANT_CUCCO $3b:$00, got " +
                $"${giantRecord.Id:x2}:${giantRecord.SubId:x2}.");
        }

        Record = record;
        _giantRecord = giantRecord;
        _room = room;
        _random = random;
        _bracelet = bracelet;
        _throwing = throwing;
        _soundRequested = soundRequested;
        _screenShakeRequested = screenShakeRequested;
        _applyThrownObjectHit = applyThrownObjectHit;
        _movement = new EnemyTerrainMovement(this, room);
        _state = CuccoState.Uninitialized;
        _counter1 = 0;
        _counter2 = 0;
        _angle = 0;
        _zFixed = 0;
        _speedZ = 0;
        _hitCount = 0;
        _revengeCounter = 0;
        _heldSoundCounter = 0;
        _transformationCounter = 0;
        _throwPrecise = position;
        _throwDirection = Vector2I.Zero;
        _throwSpeedRaw = 0;
        _giant = false;
        _giantCollisionDisabled = false;
        _transformToGiant = false;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        Visible = false;
    }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead)
            return;
        if (BeginFrame())
            return;

        UpdateRevengeSpawner(spawns);
        switch (_state)
        {
            case CuccoState.Uninitialized:
                _state = CuccoState.Standing;
                if (_giant)
                    _screenShakeRequested(
                        _giantBehavior.InitialScreenShakeUpdates);
                Visible = true;
                QueueRedraw();
                return;

            case CuccoState.Standing:
                OracleRandomResult roll = _random.Next();
                if ((roll.Value & _behavior.IdleRollMask) != 0)
                    return;
                _state = CuccoState.Hopping;
                _counter1 = 0;
                _counter2 = _behavior.HopCountBase +
                    (roll.High & _behavior.HopCountMask);
                _angle = roll.Low & _behavior.AngleMask;
                SetDirectionalAnimation();
                return;

            case CuccoState.Hopping:
                int zIndex = _counter1 & 0x0f;
                _counter1 = (_counter1 + 1) & 0xff;
                _zFixed = _behavior.HopZValues[zIndex].Value << 8;
                if (_zFixed == 0)
                {
                    _counter2--;
                    if (_counter2 == 0)
                        _state = CuccoState.Standing;
                }
                _angle = EnemyAdjacentWallResolver.Shared.BounceAngle(
                    Position, _angle, IsWallOrHole);
                SetDirectionalAnimation();
                Position += OracleObjectMovement.Shared.Delta(
                    _giant
                        ? _giantBehavior.WanderSpeedRaw
                        : _behavior.WanderSpeedRaw,
                    _angle);
                AdvanceAnimation();
                QueueRedraw();
                return;

            case CuccoState.Runaway:
                _angle = OracleObjectMovement.Shared.RelativeAngle(
                    Position, player.Position) ^ 0x10;
                SetDirectionalAnimation();
                _movement.MoveUsingAdjacentWalls(
                    _angle,
                    _giant
                        ? _giantBehavior.WanderSpeedRaw
                        : _behavior.RunawaySpeedRaw,
                    allowHoles: false,
                    topDown: false);
                AdvanceAnimation();
                return;

            case CuccoState.Held:
                UpdateHeld(player);
                return;

            case CuccoState.Thrown:
                UpdateThrown();
                return;

            case CuccoState.Transforming:
                _transformationCounter--;
                if (_transformationCounter == 0)
                {
                    if (_transformToGiant)
                        TransformIntoGiant();
                    else
                    {
                        spawns.Add(new BabyCuccoReplacementSpawn(Position));
                        Finish();
                    }
                }
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Cucco state {_state}.");
        }
    }

    internal bool TryUseBracelet(Player player, Vector2I releaseDirection)
    {
        if (_state == CuccoState.Held)
        {
            Release(player, releaseDirection);
            return true;
        }
        if (_state is not (
                CuccoState.Standing or CuccoState.Hopping or CuccoState.Runaway) ||
            player.IsCarryingObject || player.CutsceneControlled)
        {
            return false;
        }

        Vector2 point =
            player.Position + (Vector2)player.FacingVector * 6.0f;
        Vector2 delta = Position - point;
        if (Mathf.Abs(delta.X) >= 13 || Mathf.Abs(delta.Y) >= 13)
            return false;

        _state = CuccoState.Held;
        _heldSoundCounter = 0;
        player.BeginCarriedObjectPose();
        RestartAnimation(HeldAnimationIndex(player.FacingVector));
        _soundRequested(OracleSoundEngine.SndChicken);
        UpdateHeld(player, justGrabbed: true);
        return true;
    }

    internal bool TakeHit()
    {
        if (!CollisionEnabled || InvincibilityCounter != 0)
            return false;

        InvincibilityCounter = 0x20;
        _zFixed = 0;
        if (_giant)
        {
            // Ages applies the sword's damage first: health $02 reaches zero
            // and collisionType bit 7 is cleared. enemyCode3b then increments
            // var30 and restores health to $40, but never restores collisions.
            // The Giant Cucco consequently accepts exactly one sword hit and
            // remains forever in state $0a with var30=$01.
            _hitCount = 1;
            Health = _giantBehavior.PostHitHealth;
            _giantCollisionDisabled = true;
            _state = CuccoState.Runaway;
        }
        else
        {
            Health = 0x40;
            _state = CuccoState.Runaway;
            if ((_hitCount & 0x20) == 0)
                _hitCount++;
        }
        _soundRequested(OracleSoundEngine.SndDamageEnemy);
        if (!_giant)
            _soundRequested(OracleSoundEngine.SndChicken);
        QueueRedraw();
        return true;
    }

    internal void BeginMysterySeedTransformation(
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!CollisionEnabled)
            return;
        if (_giant)
            return;

        // INTERAC_PUFF $05:$02 reaches its terminal bit-$80 parameter after
        // 18 animation updates; the parent observes it on the next update.
        _state = CuccoState.Transforming;
        _transformToGiant =
            _hitCount >= _behavior.RevengeHitThreshold;
        _transformationCounter = 19;
        Visible = false;
        spawns.Add(new PuzzlePuffSpawn(Position, Sound: 0));
        QueueRedraw();
    }

    private void UpdateRevengeSpawner(ICollection<RoomEntitySpawn> spawns)
    {
        if (_giant)
            return;
        if (_revengeCounter != 0)
        {
            _revengeCounter--;
            if (_revengeCounter != 0)
                return;
        }
        if (_hitCount < _behavior.RevengeHitThreshold)
            return;

        spawns.Add(new CuccoAttackerSpawn(_hitCount));
        int index = ((_hitCount - _behavior.RevengeHitThreshold) & 0x1e) >> 1;
        _revengeCounter = _behavior.RevengeDelays[index].Value;
    }

    private void UpdateHeld(Player player, bool justGrabbed = false)
    {
        if (!justGrabbed)
        {
            _heldSoundCounter = (_heldSoundCounter - 1) & 0xff;
            if ((_heldSoundCounter & 0x1f) == 0 &&
                InvincibilityCounter == 0)
            {
                _soundRequested(OracleSoundEngine.SndChicken);
            }
        }
        Vector2I offset = HeldOffset(player);
        Position = player.Position + new Vector2(offset.X, 0);
        _zFixed = offset.Y << 8;
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
        Vector2I offset = HeldOffset(player);
        _state = CuccoState.Thrown;
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
            Vector2 edge = _throwPrecise + _throwing.EdgeOffset(_throwDirection);
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

        Position = OracleObjectMath.ToPixelPosition(_throwPrecise);
        if (!WithinRoom(Position))
        {
            Finish();
            return;
        }

        _applyThrownObjectHit(
            new Rect2(
                Position - new Vector2(_bracelet.RadiusX, _bracelet.RadiusY),
                new Vector2(_bracelet.RadiusX * 2, _bracelet.RadiusY * 2)),
            _zFixed >> 8,
            _bracelet.CollisionZRadius,
            _bracelet.Damage);

        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _bracelet.Gravity);
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
            _state = CuccoState.Runaway;
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

    private void SetDirectionalAnimation()
    {
        if ((_angle & 0x0f) == 0)
            return;
        int animation = ((_angle & 0x10) >> 4) ^ 1;
        if (AnimationIndex != animation)
            RestartAnimation(animation);
    }

    private static int HeldAnimationIndex(Vector2I facing) =>
        facing is { Y: < 0 } or { X: > 0 } ? 1 : 0;

    private static Vector2I HeldOffset(Player player) =>
        player.BraceletEntityOffset ??
            new Vector2I(
                0,
                player.CarriedObjectAnimationFrame == 0 &&
                    player.FacingVector.X != 0 ? -14 : -13);

    private bool IsWallOrHole(Vector2I point)
    {
        if (point.X < 0 || point.X >= _room.Width ||
            point.Y < 0 || point.Y >= _room.Height)
        {
            return true;
        }
        Vector2 sample = point;
        return _room.IsSolid(sample) ||
            _room.GetTerrainInfo(sample).Hazard == HazardType.Hole;
    }

    private bool WithinRoom(Vector2 point) =>
        point.X >= 0 && point.X < _room.Width &&
        point.Y >= 0 && point.Y < _room.Height;

    private static int DirectionAngle(Vector2I direction) =>
        direction == Vector2I.Up ? 0x00
        : direction == Vector2I.Right ? 0x08
        : direction == Vector2I.Down ? 0x10
        : direction == Vector2I.Left ? 0x18
        : throw new ArgumentOutOfRangeException(nameof(direction));

    private void TransformIntoGiant()
    {
        Vector2 position = Position;
        Record = _giantRecord;
        _giant = true;
        _giantCollisionDisabled = false;
        _transformToGiant = false;
        _state = CuccoState.Uninitialized;
        _counter1 = 0;
        _counter2 = 0;
        _angle = 0;
        _zFixed = 0;
        _speedZ = 0;
        _hitCount = 0;
        _revengeCounter = 0;
        _heldSoundCounter = 0;
        _transformationCounter = 0;
        _throwDirection = Vector2I.Zero;
        _throwSpeedRaw = 0;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(_giantRecord),
            positionedOam: true);
        Visible = false;
    }
}

internal enum CuccoState
{
    Uninitialized = 0,
    Held = 2,
    Thrown = 3,
    Standing = 8,
    Hopping = 9,
    Runaway = 10,
    Transforming = 11
}
