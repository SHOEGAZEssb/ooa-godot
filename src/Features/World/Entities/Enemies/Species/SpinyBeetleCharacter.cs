using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ENEMY_SPINY_BEETLE $1b:$01 and its uncounted ENEMY_BUSH_OR_ROCK child.
/// The child mimics dungeon metatile $20, protects the hidden parent, and can
/// be destroyed or lifted before the exposed Beetle enters its random walk.
/// </summary>
internal partial class SpinyBeetleCharacter : EnemyCharacter
{
    private readonly SpinyBeetleBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.SpinyBeetle;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private EnemyTerrainMovement _movement = null!;
    private BraceletDatabaseRecord _bracelet;
    private Action<Rect2, int, int, int> _applyThrownObjectHit = null!;
    private Texture2D _coverTexture = null!;
    private SpinyBeetleState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private bool _parentVisible;
    private bool _coverProtects;
    private bool _coverVisualActive;
    private bool _coverHeld;
    private bool _coverThrown;
    private bool _linkContactPending;
    private bool _coverDebrisPending;
    private bool _swordRemovedCover;
    private Vector2 _coverDrawOffset;
    private Vector2 _coverGroundPrecise;
    private Vector2I _coverThrowDirection;
    private int _coverZFixed;
    private int _coverSpeedZ;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal SpinyBeetleState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal bool ParentVisible => _parentVisible;
    internal bool CoverProtects => _coverProtects;
    internal bool CoverVisualActive => _coverVisualActive;
    internal bool CoverHeld => _coverHeld;
    internal bool CoverThrown => _coverThrown;
    internal Vector2 CoverDrawOffset => _coverDrawOffset;
    internal ulong CoverTextureHash =>
        OracleGraphicsCache.PixelHash(_coverTexture.GetImage());

    public override Rect2 CollisionBounds
    {
        get
        {
            if (!_coverProtects)
                return base.CollisionBounds;
            int radius = Record.RadiusX;
            return new Rect2(
                Position - Vector2.One * radius,
                Vector2.One * radius * 2);
        }
    }

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<Rect2, int, int, int> applyThrownObjectHit)
    {
        Record = record;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        _room = room;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _bracelet = new BraceletDatabase().Data;
        _applyThrownObjectHit = applyThrownObjectHit;
        _coverTexture = room.BuildMimickedMetatileTexture(
            (byte)_behavior.DungeonBushTile);
        _coverProtects = true;
        _coverVisualActive = true;
        _parentVisible = false;
        SetCollisionRadii(
            _behavior.CoveredCollisionRadius,
            _behavior.CoveredCollisionRadius);
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true);
        QueueRedraw();
    }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead)
            return;

        // The bush/rock child remains independently active while its parent
        // is recoiling, matching the two-object source implementation.
        UpdateCover(player, spawns);
        if (BeginFrame())
            return;
        if (CheckHazards())
            return;

        if (_state == SpinyBeetleState.Uninitialized)
        {
            _state = SpinyBeetleState.CoveredWaiting;
            return;
        }
        if (!_coverProtects &&
            _state is SpinyBeetleState.CoveredWaiting or
                SpinyBeetleState.CoveredCharging)
        {
            BeginExposedWait();
            return;
        }

        switch (_state)
        {
            case SpinyBeetleState.CoveredWaiting:
                if (_linkContactPending)
                {
                    _linkContactPending = false;
                    _angle = CardinalAngleToward(player.Position);
                    BeginCharge();
                    return;
                }
                if (_counter2 != 0 && --_counter2 != 0)
                    return;
                if (!IsCenteredWithLink(player.Position))
                    return;

                _angle = CardinalAngleToward(player.Position);
                // The proximity path uniquely refuses to charge upward.
                if (_angle == 0 || HasTopDownWallOrHole(_angle))
                    return;
                BeginCharge();
                return;

            case SpinyBeetleState.CoveredCharging:
                if (--_counter1 == 0 ||
                    !_movement.MoveUsingAdjacentWalls(
                        _angle,
                        _behavior.SpeedRaw,
                        allowHoles: false,
                        topDown: true))
                {
                    EndCharge();
                    return;
                }
                AdvanceAnimation();
                return;

            case SpinyBeetleState.ExposedWaiting:
                if (--_counter1 == 0)
                {
                    // stateA increments the zero counter before entering B.
                    _counter1 = 1;
                    _state = SpinyBeetleState.ExposedWandering;
                }
                AdvanceAnimation();
                return;

            case SpinyBeetleState.ExposedWandering:
                if (--_counter1 == 0)
                {
                    _counter1 = _behavior.WanderCounter;
                    _angle = _random.Next().Value & 0x1c;
                }
                else
                {
                    _movement.MoveUsingAdjacentWalls(
                        _angle,
                        _behavior.SpeedRaw,
                        allowHoles: false,
                        topDown: false);
                }
                AdvanceAnimation();
                return;
        }
    }

    internal void RegisterLinkContact(Vector2 linkPosition)
    {
        if (_state == SpinyBeetleState.CoveredWaiting &&
            _coverProtects &&
            OverlapsLink(linkPosition))
        {
            _linkContactPending = true;
        }
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (_coverProtects && _coverVisualActive)
        {
            RemoveProtectiveCover(createDebris: true);
            _swordRemovedCover = true;
            return true;
        }
        return base.TakeSwordHit(sourcePosition, damage);
    }

    internal override bool TakeBurnHit(int damage)
    {
        if (_coverProtects && _coverVisualActive)
        {
            RemoveProtectiveCover(createDebris: true);
            return true;
        }
        return base.TakeBurnHit(damage);
    }

    internal bool TakeCoverDebris(out Vector2 position)
    {
        if (!_coverDebrisPending)
        {
            position = default;
            return false;
        }
        _coverDebrisPending = false;
        position = Position;
        return true;
    }

    internal void ApplyAcceptedSwordResponse(
        Vector2 sourcePosition,
        EnemyKnockbackStrength strength)
    {
        if (_swordRemovedCover)
        {
            _swordRemovedCover = false;
            return;
        }
        ApplySwordKnockback(sourcePosition, strength);
    }

    internal bool TryUseBracelet(Player player)
    {
        if (_coverHeld)
        {
            ThrowCover(player);
            return true;
        }
        if (!_coverProtects || !_coverVisualActive || _coverThrown ||
            player.IsCarryingObject || player.CutsceneControlled)
        {
            return false;
        }

        Vector2 point =
            player.Position + (Vector2)player.FacingVector * 6.0f;
        Vector2 delta = Position - point;
        if (Mathf.Abs(delta.X) >= 13 || Mathf.Abs(delta.Y) >= 13)
            return false;

        _coverHeld = true;
        _coverProtects = false;
        _parentVisible = true;
        player.BeginCarriedObjectPose();
        UpdateHeldCover(player);
        QueueRedraw();
        return true;
    }

    public override void _Draw()
    {
        if (IsDead || !Visible)
            return;
        if (_parentVisible)
            DrawCurrentAnimation();
        if (_coverVisualActive)
        {
            DrawTexture(
                _coverTexture,
                _coverDrawOffset + new Vector2(-8, -8) +
                    TransitionDrawOffset);
        }
    }

    private void BeginCharge()
    {
        _state = SpinyBeetleState.CoveredCharging;
        _counter1 = _behavior.ChargeFrames;
        _parentVisible = true;
        _coverDrawOffset = Vector2.Down * _behavior.ChargeCoverZ;
        QueueRedraw();
    }

    private void EndCharge()
    {
        _state = SpinyBeetleState.CoveredWaiting;
        _counter2 = _behavior.RestFrames;
        _parentVisible = false;
        _coverDrawOffset = Vector2.Zero;
        QueueRedraw();
    }

    private void BeginExposedWait()
    {
        _state = SpinyBeetleState.ExposedWaiting;
        _counter1 = _behavior.RevealFrames;
        _parentVisible = true;
        SetCollisionRadii(
            _behavior.ExposedCollisionRadius,
            _behavior.ExposedCollisionRadius);
        QueueRedraw();
    }

    private void RemoveProtectiveCover(bool createDebris)
    {
        _coverProtects = false;
        _coverVisualActive = false;
        _parentVisible = true;
        _coverHeld = false;
        _coverThrown = false;
        _coverDebrisPending |= createDebris;
        QueueRedraw();
    }

    private void UpdateCover(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_coverHeld)
            UpdateHeldCover(player);
        else if (_coverThrown)
            UpdateThrownCover(spawns);
    }

    private void UpdateHeldCover(Player player)
    {
        Vector2I offset = player.BraceletEntityOffset ??
            new Vector2I(
                0,
                player.CarriedObjectAnimationFrame == 0 &&
                    player.FacingVector.X != 0 ? -14 : -13);
        Vector2 world =
            player.Position + new Vector2(offset.X, offset.Y);
        _coverDrawOffset = world - Position;
        QueueRedraw();
    }

    private void ThrowCover(Player player)
    {
        Vector2I heldOffset = player.BraceletEntityOffset ??
            new Vector2I(
                0,
                player.CarriedObjectAnimationFrame == 0 &&
                    player.FacingVector.X != 0 ? -14 : -13);
        _coverHeld = false;
        _coverThrown = true;
        _coverThrowDirection = player.FacingVector;
        _coverGroundPrecise =
            player.Position + new Vector2(heldOffset.X, 0) +
            _coverThrowDirection;
        _coverZFixed = heldOffset.Y << 8;
        _coverSpeedZ = _bracelet.InitialSpeedZ;
        player.EndCarriedObjectPose();
        SyncThrownCoverDrawOffset();
    }

    private void UpdateThrownCover(ICollection<RoomEntitySpawn> spawns)
    {
        Vector2 ground =
            OracleObjectMath.ToPixelPosition(_coverGroundPrecise);
        if (ground.X < 0 || ground.X >= _room.Width ||
            ground.Y < 0 || ground.Y >= _room.Height)
        {
            DeleteThrownCover();
            return;
        }

        Vector2 front = ground + ThrowCollisionOffset(
            _coverThrowDirection);
        if (front.X < 0 || front.X >= _room.Width ||
            front.Y < 0 || front.Y >= _room.Height ||
            _room.IsSolid(front))
        {
            BreakThrownCover(ground, spawns);
            return;
        }

        OracleObjectMovement.Shared.ApplySpeed(
            ref _coverGroundPrecise,
            _bracelet.SpeedRaw,
            DirectionAngle(_coverThrowDirection));
        if (OracleObjectMath.UpdateSpeedZ(
            ref _coverZFixed,
            ref _coverSpeedZ,
            _bracelet.Gravity))
        {
            BreakThrownCover(
                OracleObjectMath.ToPixelPosition(_coverGroundPrecise),
                spawns);
            return;
        }

        SyncThrownCoverDrawOffset();
        Vector2 center =
            OracleObjectMath.ToPixelPosition(_coverGroundPrecise);
        _applyThrownObjectHit(
            new Rect2(
                center - new Vector2(
                    _bracelet.RadiusX,
                    _bracelet.RadiusY),
                new Vector2(
                    _bracelet.RadiusX * 2,
                    _bracelet.RadiusY * 2)),
            _coverZFixed >> 8,
            _bracelet.CollisionZRadius,
            _bracelet.Damage);
    }

    private void BreakThrownCover(
        Vector2 position,
        ICollection<RoomEntitySpawn> spawns)
    {
        HazardType hazard = _room.GetTerrainInfo(position).Hazard;
        if (hazard is HazardType.Water or HazardType.Lava)
            spawns.Add(new EnemySplashSpawn(position, hazard));
        else if (hazard == HazardType.Hole)
            spawns.Add(new FallingDownHoleSpawn(position));
        else
            spawns.Add(new GrassDebrisSpawn(position));
        DeleteThrownCover();
    }

    private void DeleteThrownCover()
    {
        _coverThrown = false;
        _coverVisualActive = false;
        QueueRedraw();
    }

    private void SyncThrownCoverDrawOffset()
    {
        Vector2 ground =
            OracleObjectMath.ToPixelPosition(_coverGroundPrecise);
        _coverDrawOffset =
            ground + Vector2.Down * (_coverZFixed >> 8) - Position;
        QueueRedraw();
    }

    private bool IsCenteredWithLink(Vector2 linkPosition)
    {
        Vector2 beetle = OracleObjectMath.ToPixelPosition(Position);
        Vector2 link = OracleObjectMath.ToPixelPosition(linkPosition);
        return Mathf.Abs(link.X - beetle.X) <=
                _behavior.ApproachAxisRadius ||
            Mathf.Abs(link.Y - beetle.Y) <=
                _behavior.ApproachAxisRadius;
    }

    private int CardinalAngleToward(Vector2 target) =>
        (OracleObjectMovement.Shared.RelativeAngle(
            OracleObjectMath.ToPixelPosition(Position),
            OracleObjectMath.ToPixelPosition(target)) + 4) & 0x18;

    private bool HasTopDownWallOrHole(int angle) =>
        EnemyAdjacentWallResolver.Shared.ProbeTopDown(
            Position,
            angle,
            point =>
                point.X < 0 || point.X >= _room.Width ||
                point.Y < 0 || point.Y >= _room.Height ||
                _room.IsSolid(point) ||
                _room.GetTerrainInfo(point).Hazard == HazardType.Hole)
            .Bitset != 0;

    private static int DirectionAngle(Vector2I direction) =>
        direction == Vector2I.Up ? 0x00
        : direction == Vector2I.Right ? 0x08
        : direction == Vector2I.Down ? 0x10
        : direction == Vector2I.Left ? 0x18
        : throw new ArgumentOutOfRangeException(nameof(direction));

    private static Vector2 ThrowCollisionOffset(Vector2I direction) =>
        direction == Vector2I.Up ? new Vector2(0, -3)
        : direction == Vector2I.Right ? new Vector2(3, 0)
        : direction == Vector2I.Down ? new Vector2(0, 7)
        : direction == Vector2I.Left ? new Vector2(-3, 0)
        : Vector2.Zero;
}

internal enum SpinyBeetleState
{
    Uninitialized = 0,
    CoveredWaiting = 8,
    CoveredCharging = 9,
    ExposedWaiting = 10,
    ExposedWandering = 11
}
