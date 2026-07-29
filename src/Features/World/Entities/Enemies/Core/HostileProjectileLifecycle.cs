using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Shared PART projectile flight, Link contact, terrain response, and bounce
/// lifetime. A profile retains the source-specific ordering around movement
/// and collision; visuals remain owned by the projectile node.
/// </summary>
internal sealed class HostileProjectileLifecycle
{
    private readonly ProjectileBounceBehaviorProfile _bounce =
        EnemyBehaviorTables.Shared.ProjectileBounce;
    private readonly Node2D _entity;
    private readonly OracleRoomData _room;
    private readonly HostileProjectileProfile _profile;
    private Vector2 _collisionRadii;
    private int _speedZ;

    public HostileProjectileLifecycle(
        Node2D entity,
        OracleRoomData room,
        HostileProjectileProfile profile,
        int angle)
    {
        if (profile.SpeedRaw <= 0 ||
            profile.CollisionRadii.X < 0 ||
            profile.CollisionRadii.Y < 0)
        {
            throw new InvalidOperationException(
                $"{profile.Source} has an invalid hostile-projectile profile.");
        }

        _entity = entity;
        _room = room;
        _profile = profile;
        _collisionRadii = profile.CollisionRadii;
        Angle = angle & 0x18;
    }

    public HostileProjectileState State { get; private set; }
    public bool Finished { get; private set; }
    public int Angle { get; private set; }
    public int Counter { get; private set; }
    public int ZFixed { get; private set; }
    public int ElapsedFrames { get; private set; }
    public Rect2 CollisionBounds => new(
        _entity.Position - _collisionRadii,
        _collisionRadii * 2.0f);

    public void UpdateFrame(Player player)
    {
        if (Finished)
            return;
        ElapsedFrames++;

        switch (State)
        {
            case HostileProjectileState.Initializing:
                State = HostileProjectileState.Flying;
                return;
            case HostileProjectileState.CollisionPending:
                BeginBounce();
                return;
            case HostileProjectileState.Bouncing:
                UpdateBounce();
                return;
        }

        if (player.TryBlockWithShield(CollisionBounds))
        {
            BeginBounce();
            return;
        }

        Rect2 linkBounds = new(
            player.Position - Vector2.One * 6,
            Vector2.One * 12);
        if (CollisionBounds.Intersects(linkBounds))
        {
            player.ApplyEnemyContactDamage(
                _entity.Position,
                _profile.DamageQuarters,
                _profile.DamageSource);
            Finish();
            return;
        }

        if (!WithinVisibleBoundary(player.Position))
        {
            Finish();
            return;
        }

        Vector2 movement =
            OracleObjectMovement.Shared.Delta(_profile.SpeedRaw, Angle);
        Vector2 destination = _entity.Position + movement;
        switch (_profile.TileProbe)
        {
            case HostileProjectileTileProbe.CurrentPosition:
                if (!WithinRoom(_entity.Position))
                {
                    Finish();
                    return;
                }
                if (_room.IsSolid(_entity.Position))
                {
                    BeginBounce();
                    return;
                }
                _entity.Position = destination;
                break;
            case HostileProjectileTileProbe.DestinationWithPendingBounce:
                if (!WithinRoom(destination))
                {
                    Finish();
                    return;
                }
                _entity.Position = destination;
                if (_room.IsSolid(destination))
                    State = HostileProjectileState.CollisionPending;
                break;
            default:
                throw new InvalidOperationException(
                    $"{_profile.Source} has unsupported tile-probe policy " +
                    $"{_profile.TileProbe}.");
        }
        _entity.QueueRedraw();
    }

    public bool DeflectWithSword()
    {
        if (Finished)
            return false;
        if (_profile.SwordWindow == HostileProjectileSwordWindow.BeforeBounce &&
            State is not (
                HostileProjectileState.Initializing or
                HostileProjectileState.Flying))
        {
            return false;
        }

        BeginBounce();
        return true;
    }

    private void BeginBounce()
    {
        State = HostileProjectileState.Bouncing;
        if (_profile.ClearCollisionOnBounce)
            _collisionRadii = Vector2.Zero;
        if (_profile.ResetZOnBounce)
            ZFixed = 0;
        Counter = _bounce.Frames;
        _speedZ = _bounce.InitialSpeedZ;
        Angle ^= 0x10;
        _entity.QueueRedraw();
    }

    private void UpdateBounce()
    {
        Counter--;
        if (Counter == 0)
        {
            Finish();
            return;
        }

        int zFixed = ZFixed;
        OracleObjectMath.UpdateSpeedZ(
            ref zFixed, ref _speedZ, _bounce.Gravity);
        ZFixed = zFixed;
        _entity.Position +=
            OracleObjectMovement.Shared.Delta(_bounce.SpeedRaw, Angle);
        _entity.QueueRedraw();
    }

    private bool WithinVisibleBoundary(Vector2 linkPosition)
    {
        float maxCameraX = Mathf.Max(
            0.0f, _room.Width - OracleRoomData.ViewportWidth);
        float maxCameraY = Mathf.Max(
            0.0f, _room.Height - OracleRoomData.ViewportHeight);
        Vector2 cameraOrigin = new(
            Mathf.Clamp(
                linkPosition.X - OracleRoomData.ViewportWidth / 2.0f,
                0.0f,
                maxCameraX),
            Mathf.Clamp(
                linkPosition.Y - OracleRoomData.ViewportHeight / 2.0f,
                0.0f,
                maxCameraY));
        return OracleObjectMath.IsInsideOriginalScreenBoundary(
            _entity.Position - cameraOrigin);
    }

    private bool WithinRoom(Vector2 position) =>
        position.X >= 0 &&
        position.X < _room.Width &&
        position.Y >= 0 &&
        position.Y < _room.Height;

    private void Finish()
    {
        Finished = true;
        _entity.Visible = false;
    }
}

internal readonly record struct HostileProjectileProfile(
    string Source,
    int DamageQuarters,
    int SpeedRaw,
    RingDamageSource DamageSource,
    Vector2 CollisionRadii,
    HostileProjectileTileProbe TileProbe,
    HostileProjectileSwordWindow SwordWindow,
    bool ClearCollisionOnBounce,
    bool ResetZOnBounce);

internal enum HostileProjectileState
{
    Initializing,
    Flying,
    CollisionPending,
    Bouncing
}

internal enum HostileProjectileTileProbe
{
    CurrentPosition,
    DestinationWithPendingBounce
}

internal enum HostileProjectileSwordWindow
{
    AnyActiveState,
    BeforeBounce
}
