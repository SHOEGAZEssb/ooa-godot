using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class SeedReflectorChildRoomEntity(RotatableSeedThingRoomEntity parent,
    Vector2 offset,int z) : Node2D, IRoomEntity, IFixedRoomEntity, ISeedBounceTarget,
    ISeedHeightAwareHittableRoomEntity, ISeedPreMovementCollisionTarget,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    internal bool Initialized { get; private set; }
    internal Vector2 CollisionRadii { get; private set; }
    public int SeedBounceOrientation { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Initialized;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        return ScreenTransitionPresentation.Hidden;
    }
    private void Initialize()
    {
        Visible = false;
        if (Initialized) return;
        Initialized = true;
        Position = parent.Position + offset;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        Visible = false;
        if (!Initialized) { Initialize(); return; }
        SeedBounceOrientation = parent.SeedBounceOrientation;
        CollisionRadii = parent.CollisionRadii;
    }
    public bool IntersectsSeed(Rect2 hitbox) => false; // ground-level shooter seeds do not overlap Z=$f2.
    public SeedHitResult ApplySeedHit(Rect2 hitbox,Vector2 sourcePosition,int seedItem,ICollection<RoomEntitySpawn> spawns) =>
        ApplySeedHitAtHeight(hitbox,sourcePosition,0,seedItem,spawns);
    public SeedHitResult ApplySeedHitAtHeight(Rect2 hitbox,Vector2 sourcePosition,int sourceZ,int seedItem,ICollection<RoomEntitySpawn> spawns) =>
        RoomEntityManager.ObjectCollisionZOverlaps(z,sourceZ,7) &&
        RotatableSeedThingRoomEntity.SourceCollisionIntersects(hitbox,Position,CollisionRadii) ? SeedHitResult.Bounce : SeedHitResult.None;
}
