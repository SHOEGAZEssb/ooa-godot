using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ITEM_28's Ricky form. Its collision follows Ricky for $14 updates and its
/// four diagonal tile probes use BREAKABLETILESOURCE_RICKY_PUNCH $0f.
/// </summary>
internal sealed partial class RickyPunchAttackRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IPlayerProjectileRoomEntity,
    IRoomEntityLifetime
{
    private static readonly Vector2[] BreakOffsets =
    [
        new(8, -8), new(-8, -8), new(8, 8), new(-8, 8)
    ];

    private readonly RickyCompanionRoomEntity _owner;
    private readonly RickyCompanionBehaviorRecord _behavior;
    private readonly RickyAttackTileBreaker _tileBreaker;
    private int _counter;
    private bool _initialized;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool CollisionEnabled => _initialized && !Finished;
    public int Damage => _behavior.PunchDamage;
    internal int Counter => _counter;
    public Rect2 CollisionBounds
    {
        get
        {
            RickyPunchBox box = _behavior.PunchBoxes[_owner.Direction];
            return new Rect2(
                Position - new Vector2(box.RadiusX, box.RadiusY),
                new Vector2(box.RadiusX * 2, box.RadiusY * 2));
        }
    }

    internal RickyPunchAttackRoomEntity(
        RickyPunchAttackSpawn spawn,
        RickyCompanionBehaviorRecord behavior,
        RickyAttackTileBreaker tileBreaker)
    {
        _owner = spawn.Owner;
        _behavior = behavior;
        _tileBreaker = tileBreaker;
        _counter = behavior.PunchLifetime;
        Name = "RickyPunchAttack";
        Visible = false;
        UpdatePosition();
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        if (Finished)
            return;
        UpdatePosition();
        if (!_initialized)
        {
            _initialized = true;
            return;
        }

        foreach (Vector2 offset in BreakOffsets)
        {
            _tileBreaker.TryBreak(
                Position + offset,
                BreakableTileDatabase.SourceRickyPunch,
                spawns);
        }
        if (--_counter == 0)
            Finished = true;
    }

    public void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns) =>
        _ = spawns;

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => _ = offset;

    private void UpdatePosition()
    {
        RickyPunchBox box = _behavior.PunchBoxes[_owner.Direction];
        Position = OracleObjectMath.ToPixelPosition(
            _owner.RidingLinkPosition +
            new Vector2(box.OffsetX, box.OffsetY));
    }
}

internal sealed record RickyPunchAttackSpawn(
    RickyCompanionRoomEntity Owner,
    int Group,
    int Room) : RoomEntitySpawn(UpdateThisFrame: true);
