using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class DimitriMouthRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IPlayerProjectileRoomEntity, IRoomEntityLifetime
{
    private static readonly Vector2[] Offsets = [new(0,-10),new(10,-2),new(0,4),new(-10,-2)];
    private readonly DimitriCompanionRoomEntity _owner;
    private readonly CompanionAttackTileBreaker _tileBreaker;
    private int _counter = 12;
    private bool _enemyCollision;
    public Node2D Node => this;
    public bool Finished => _counter == 0 || _owner.Phase != DimitriPhase.Eating;
    public bool CollisionEnabled => !Finished && !_enemyCollision;
    public int Damage => 7;
    public Rect2 CollisionBounds => new(Position - new Vector2(8,8), new Vector2(16,16));
    internal DimitriMouthRoomEntity(DimitriMouthSpawn spawn, CompanionAttackTileBreaker tileBreaker)
    { _owner = spawn.Owner; _tileBreaker = tileBreaker; Visible = false; UpdatePosition(); }
    private void UpdatePosition() => Position = _owner.PrecisePosition + Offsets[_owner.Direction];
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        UpdatePosition();
        // ITEM_DIMITRI_MOUTH consumes var2a before trying the tile, on its
        // next dispatch after the collision pass writes bit 1.
        if (_enemyCollision || _tileBreaker.TryBreak(Position, 0x12, spawns))
        { _owner.Swallow(); _counter = 0; }
        else _counter--;
    }
    public void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns) => _enemyCollision = true;
    internal bool TrySwallow(IRoomEntity target) => target is IDimitriMouthTarget edible &&
        _owner.AcceptsMouthCollision(edible.DimitriCollisionType) &&
        _owner.CanSwallow(edible.DimitriCollisionMode) && edible.TrySwallow(CollisionBounds);
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) { }
}

internal sealed record DimitriMouthSpawn(DimitriCompanionRoomEntity Owner, int Group, int Room)
    : RoomEntitySpawn(UpdateThisFrame: true);
