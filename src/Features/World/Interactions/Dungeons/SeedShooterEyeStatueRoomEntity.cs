using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class SeedShooterEyeStatueRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, ISeedCollisionTarget, IObjectCollisionHeightRoomEntity,
    IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, INativePartHealthRoomEntity
{
    private readonly SeedShooterEyeStatueRecord _record;
    private readonly SeedShooterEyeStatueDatabase _data;
    private readonly SeedShooterEyeStatueState _state;
    private int _invincibility;
    private bool _pendingHit;
    private bool _dead;

    public Node2D Node => this;
    public int CollisionZ => 0;
    public bool UpdatesDuringDialogue => !_state.Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_state.Initialized;
    internal int Counter => _state.Counter;
    internal int Invincibility => _invincibility;
    internal bool PendingHit => _pendingHit;
    internal Rect2 CollisionBounds => new(Position - new Vector2(_record.RadiusX,_record.RadiusY),
        new Vector2(_record.RadiusX * 2,_record.RadiusY * 2));

    internal SeedShooterEyeStatueRoomEntity(SeedShooterEyeStatueRecord record,
        SeedShooterEyeStatueDatabase data,DungeonInteractionVisual visual,Action<int,bool> setTrigger)
    {
        _record = record; _data = data;
        _state = new(record.Subid,record.ActiveCounter,setTrigger);
        Name = $"SeedShooterEyeStatue_{record.Subid:x2}_{record.Order}";
        InitializeVisual(visual,new((record.PackedPosition & 15) * 16 + 8,(record.PackedPosition >> 4) * 16 + 8));
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        Visible = false;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_state.Initialized) _state.Update(false);
        Visible = _state.Visible;
        return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (!_state.Initialized) { PrepareForScreenTransition(spawns); return; }
        if (_invincibility < 0) _invincibility++;
        else if (_invincibility > 0) _invincibility--;
        _state.Update(_pendingHit || _dead);
        _pendingHit = false;
        Visible = _state.Visible;
        QueueRedraw();
    }

    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox,Vector2 sourcePosition,
        SeedRecord seed,int collisionType,ICollection<RoomEntitySpawn> spawns)
    {
        int? lockout = _data.HitLockout(collisionType);
        if (!_state.Initialized || _dead || _pendingHit || _invincibility != 0 || lockout is null ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds,hitbox)) return default;
        _pendingHit = true;
        _invincibility = lockout.Value;
        // Effect $31 changes only the part. The flying seed remains live.
        return new(true,SeedHitResult.None,false);
    }

    public SeedHitResult ApplySeedHit(Rect2 hitbox,Vector2 sourcePosition,int seedItem,ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("PART $46 requires the seed's native collision index, not only its item ID.");
    public void ClearHealthAndCollision() => _dead = true;
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
