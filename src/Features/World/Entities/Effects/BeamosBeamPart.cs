using Godot;

namespace oracleofages;

/// <summary>PART_BEAM $29: a separately allocated segment, not a continuous ray.</summary>
internal sealed partial class BeamosBeamPart : TransitionOffsetNode2D
{
    private readonly BeamosBehaviorProfile _behavior = EnemyBehaviorTables.Shared.Beamos;
    private readonly BeamosBeamDatabase _data;
    private readonly OracleRoomData _room;
    private readonly EnemyAnimationPlayer _animation;
    private OracleObjectPosition _position;
    private OracleObjectVelocity _velocity;
    private bool _shieldCollision;
    private bool _collisionCleared;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; }
    internal int SubId { get; }
    internal bool Finished { get; private set; }
    internal bool CollisionEnabled => State != 0 && !Finished && !_collisionCleared;
    internal Rect2 CollisionBounds => new(Position - new Vector2(_data.RadiusX, _data.RadiusY),
        new Vector2(_data.RadiusX * 2, _data.RadiusY * 2));

    internal BeamosBeamPart(BeamosBeamSpawn spawn, BeamosBeamDatabase data, OracleRoomData room)
    {
        _data = data;
        _room = room;
        _position = OracleObjectMovement.Shared.PositionFromPixels(spawn.Position);
        Position = _position.PixelPosition;
        Angle = spawn.Angle;
        SubId = spawn.SubId;
        _animation = new EnemyAnimationPlayer(this, data.Animations.Length);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{data.Sprite}.png"),
            data.Animations, data.TileBase, data.Palette);
        _animation.SetAnimation(0);
        Visible = false;
    }

    // partCode29 ignores PARTSTATUS_DEAD; clearing health only clears collisions.
    internal void ClearHealthAndCollision() => _collisionCleared = true;

    internal void UpdateFrame(int frame)
    {
        if (Finished) return;
        if (_shieldCollision) { Delete(); return; }
        if (State == 0)
        {
            _collisionCleared = false; // partLoadGraphicsAndProperties.
            State = 1;
            Counter = _behavior.BeamCollisionDelay;
            _velocity = OracleObjectMovement.Shared.Velocity(_behavior.BeamSpeed, Angle);
            _animation.SetAnimation(_behavior.BeamAngleAnimations[Angle & 0x0f].Value);
            return;
        }
        if (State == 1 && --Counter == 0) State = 2;
        _position = _position.Add(_velocity.YFixed * _behavior.BeamVelocityScale,
            _velocity.XFixed * _behavior.BeamVelocityScale);
        Position = _position.PixelPosition;
        Visible = (frame & SubId) == 0;
        ZIndex = NpcCharacter.InFrontOfLinkZIndex; // objectSetVisible81.
        if (State == 2 && (Position.X >= _room.Width || Position.Y >= _room.Height ||
            _room.IsSolidForEnemyMovement(Position, holesAreWalls: false))) Delete();
        QueueRedraw();
    }

    internal void HandleLinkContact(Player player)
    {
        if (!CollisionEnabled || _shieldCollision || !player.EnemyContactHeightOverlaps(0)) return;
        // partActiveCollisions[$29] exposes only Link ($00) and L3 shield ($03).
        if (player.CanAcceptShieldCollision && player.TryBlockWithShield(CollisionBounds, minimumLevel: 3))
        {
            _shieldCollision = true; // $83 is observed by the next partCode29 pass.
            return;
        }
        if (player.OverlapsEnemyCollision(CollisionBounds))
            player.ApplyEnemyContactDamage(Position, _data.Damage, RingDamageSource.Beam);
    }

    private void Delete() { Finished = true; Visible = false; }
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(_animation.CurrentTexture,
            _animation.CurrentOffset + TransitionDrawOffset);
    }
}

internal sealed record BeamosBeamSpawn(Vector2 Position, int Angle, int SubId) : RoomEntitySpawn;
