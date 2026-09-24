using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ItemDropRoomEntity(
    ItemDropEffect drop,
    Action<Vector2, HazardType> enteredHazard)
    : RoomEntityAdapter<ItemDropEffect>(drop, drop.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        ILinkSwordCollectibleRoomEntity, INativePartHealthRoomEntity, IBoomerangCollisionRoomEntity
{
    internal void AttachToItem(Func<ItemDropCarrier?> carrier) => Entity.AttachToItem(carrier);
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns)
    {
        var data = BoomerangCollisionDatabase.Shared;
        if (!data.PartEnabled(0x01) || !item.CollisionEnabled || !Entity.CanAttachToItem ||
            !RoomEntityManager.ObjectCollisionZOverlaps(Entity.ZFixed >> 8, item.ZHigh, 7) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, item.CollisionBounds)) return default;
        if (data.Effect(0x01) != 0x24)
            throw new InvalidOperationException("PART_ITEM_DROP mode$01 column$17 must select collisionEffect24.");
        return new(true, true);
    }
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player, frame.Counter);
        HazardType pending = Entity.TakePendingHazardEffect();
        if (pending != HazardType.None)
            enteredHazard(Entity.Position, pending);
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.FinishedHazard is
            HazardType.Water or HazardType.Lava)
        {
            enteredHazard(Entity.Position, Entity.FinishedHazard);
        }
        else if (Entity.FinishedHazard == HazardType.Hole)
        {
            spawns.Add(new FallingDownHoleSpawn(Entity.Position));
        }
    }

    public bool TryCollectWithSword(Rect2 hitbox) =>
        Entity.TryCollectWithSword(hitbox);
}
