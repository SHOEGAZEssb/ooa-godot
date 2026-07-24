using System.Collections.Generic;

namespace oracleofages;

internal sealed class MapleDroppedItemRoomEntity(MapleDroppedItem item)
    : RoomEntityAdapter<MapleDroppedItem>(
        item, item.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity, IRoomEntityLifetime,
        ILinkSwordCollectibleRoomEntity
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);

    public void HandleLinkContact(Player player) =>
        Entity.TryCollectByLink(player);

    public bool TryCollectWithSword(Godot.Rect2 hitbox) =>
        Entity.TryCollectWithSword(hitbox);

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
