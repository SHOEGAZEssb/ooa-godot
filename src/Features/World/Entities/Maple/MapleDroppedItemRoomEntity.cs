using System.Collections.Generic;
using System;

namespace oracleofages;

internal sealed class MapleDroppedItemRoomEntity(MapleDroppedItem item, RoomEntityManager owner)
    : RoomEntityAdapter<MapleDroppedItem>(
        item, item.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity, IRoomEntityLifetime,
        ILinkSwordCollectibleRoomEntity, IBoomerangCollisionRoomEntity, IAttachableCollectibleRoomEntity,
        ISwitchHookHittableRoomEntity, IObjectCollisionHeightRoomEntity
{
    public int CollisionZ => Entity.ZFixed >> 8;
    public void AttachToItem(Func<ItemDropCarrier?> carrier) => Entity.AttachToItem(carrier);
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns)
    {
        var data = BoomerangCollisionDatabase.Shared;
        int id = Entity.ItemIndex < 5 ? 0x15 : 0x14;
        if (!data.PartEnabled(id) || !item.CollisionEnabled || !Entity.CanAttachToItem ||
            !RoomEntityManager.ObjectCollisionZOverlaps(Entity.ZFixed >> 8, item.ZHigh, 7) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, item.CollisionBounds)) return default;
        if (data.Effect(EnemyCollisionMode.Item) != 0x24)
            throw new InvalidOperationException($"itemFromMaple.s PART${id:x2} mode$01 column$17 must select collisionEffect24.");
        return new(true, true);
    }
    public bool ApplySwitchHookHit(SwitchHookItem hook, Godot.Vector2 linkPosition)
    {
        if (!hook.CollisionEnabled || !Entity.CanAttachToItem ||
            !RoomEntityManager.ObjectCollisionZOverlaps(Entity.ZFixed >> 8, hook.ZHigh, 7) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, hook.CollisionBounds)) return false;
        Entity.AttachToItem(() => owner.SwitchHook?.Item is { Finished: false } live
            ? new ItemDropCarrier(ItemId.SwitchHook, live.Position, live.ZHigh) : null);
        hook.NotifyObjectCollision(); return true;
    }
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);

    public void HandleLinkContact(Player player) =>
        Entity.TryCollectByLink(player);

    public bool TryCollectWithSword(Godot.Rect2 hitbox) =>
        Entity.TryCollectWithSword(hitbox);

}
