using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ITEM_RICKY_TORNADO $2a: a SPEED_300 projectile using sword-level-1 tile
/// breaking and the source two-frame common-item animation.
/// </summary>
internal sealed partial class RickyTornadoRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IPlayerProjectileRoomEntity,
    IRoomEntityLifetime
{
    private readonly RickyCompanionBehaviorRecord _behavior;
    private readonly OracleRoomData _room;
    private readonly RickyAttackTileBreaker _tileBreaker;
    private readonly EnemyAnimationPlayer _animation;
    private Vector2 _precisePosition;
    private readonly int _angle;
    private readonly int _zFixed;
    private bool _initialized;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool CollisionEnabled => _initialized && !Finished;
    public int Damage => _behavior.TornadoDamage;
    public Rect2 CollisionBounds => new(
        Position - new Vector2(
            _behavior.TornadoRadiusX,
            _behavior.TornadoRadiusY),
        new Vector2(
            _behavior.TornadoRadiusX * 2,
            _behavior.TornadoRadiusY * 2));

    internal RickyTornadoRoomEntity(
        RickyTornadoSpawn spawn,
        RickyCompanionBehaviorRecord behavior,
        OracleRoomData room,
        RickyAttackTileBreaker tileBreaker)
    {
        _behavior = behavior;
        _room = room;
        _tileBreaker = tileBreaker;
        _angle = spawn.Direction * 8;
        _zFixed = spawn.ZFixed - 0x0200;
        _precisePosition = spawn.Position +
            behavior.TornadoOffsets[spawn.Direction];
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(
            OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{behavior.TornadoSprite}.png"),
            [behavior.TornadoAnimation],
            behavior.TornadoTileBase,
            behavior.TornadoPalette);
        _animation.SetAnimation(0);
        Name = "RickyTornado";
        ZIndex = NpcCharacter.InFrontOfLinkZIndex;
        Visible = false;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            Visible = true;
            QueueRedraw();
            return;
        }

        OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition,
            _behavior.TornadoSpeed,
            _angle);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        _tileBreaker.TryBreak(
            _precisePosition,
            BreakableTileDatabase.SourceSwordLevel1,
            spawns);
        if (_precisePosition.X < 0 || _precisePosition.X >= _room.Width ||
            _precisePosition.Y < 0 || _precisePosition.Y >= _room.Height ||
            _room.IsSolid(_precisePosition))
        {
            Finished = true;
            Visible = false;
            QueueRedraw();
            return;
        }
        _animation.Advance();
        QueueRedraw();
    }

    public void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns) =>
        _ = spawns;

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (!Finished && Visible)
        {
            DrawTexture(
                _animation.CurrentTexture,
                _animation.CurrentOffset +
                new Vector2(0, _zFixed >> 8) +
                TransitionDrawOffset);
        }
    }
}

internal sealed record RickyTornadoSpawn(
    Vector2 Position,
    int Direction,
    int ZFixed,
    int Group,
    int Room) : RoomEntitySpawn(UpdateThisFrame: true);
