using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class LynnaShopItemRoomEntity(LynnaShopItem item)
    : RoomEntityAdapter<LynnaShopItem>(item, item.SetTransitionDrawOffset),
        IFixedRoomEntity
{
    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame.Player);
}
