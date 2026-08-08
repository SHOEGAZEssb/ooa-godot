using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TokayShopItemRoomEntity(TokayShopItem item)
    : RoomEntityAdapter<TokayShopItem>(item, item.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomBlocker
{
    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLink(linkCenter);
}
