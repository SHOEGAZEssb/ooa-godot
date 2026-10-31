using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed partial class DimitriMouthRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IPlayerProjectileRoomEntity, IRoomEntityLifetime
{
    private string AssetRoot => _data.AssetRoot;

    private static readonly Vector2[] Offsets = [new(0,-10),new(10,-2),new(0,4),new(-10,-2)];
    private readonly DimitriDatabase _data;
    private readonly OracleRuntimeState _memory;
    private readonly CompanionAttackTileBreaker _tileBreaker;
    private int _counter = 12;
    private bool _enemyCollision;
    internal bool InitializationPending { get; private set; } = true;
    public Node2D Node => this;
    public bool Finished => _counter == 0;
    public bool CollisionEnabled => !Finished && !_enemyCollision;
    public int Damage => CompanionWeaponDatabase.For(AssetRoot).Weapon(0x2b).Damage;
    public Rect2 CollisionBounds => new(Position - new Vector2(8,8), new Vector2(16,16));
    internal DimitriMouthRoomEntity(CompanionAttackTileBreaker tileBreaker,DimitriDatabase data,OracleRuntimeState memory)
    { _data=data;_memory=memory;_tileBreaker = tileBreaker; Visible = false; UpdatePosition(); }
    // objectTakePositionWithOffset copies the coordinate high bytes only.
    private void UpdatePosition()
    {
        int direction=_memory.ReadWramByte(0xd108);
        if(direction>=Offsets.Length)throw new NotSupportedException($"dimitriMouth.s: ITEM $2b reads unsupported D1 direction${direction:x2} at $d108.");
        Position=new Vector2(_memory.ReadWramByte(0xd10d),_memory.ReadWramByte(0xd10b))+Offsets[direction];
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        InitializationPending = false;
        UpdatePosition();
        // ITEM_DIMITRI_MOUTH consumes var2a before trying the tile, on its
        // next dispatch after the collision pass writes bit 1.
        if (_enemyCollision || _tileBreaker.TryBreak(Position, 0x12, spawns))
        { _memory.SetWramByte(0xd135,1); _counter = 0; }
        else _counter--;
    }
    public void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns) => _enemyCollision = true;
    internal bool TrySwallow(IRoomEntity target) => target is IDimitriMouthTarget edible &&
        _data.AcceptsMouthCollision(edible.DimitriCollisionType) &&
        _data.CanSwallow(edible.DimitriCollisionMode) && edible.TrySwallow(CollisionBounds);
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) { }
}

internal sealed record DimitriMouthSpawn(int Group, int Room)
    : RoomEntitySpawn(UpdateThisFrame: true);
