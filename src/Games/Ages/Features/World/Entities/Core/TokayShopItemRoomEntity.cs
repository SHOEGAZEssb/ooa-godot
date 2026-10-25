using Godot;
using System.Collections.Generic;

namespace Oracles;

internal sealed class TokayShopItemRoomEntity(TokayShopItem item)
    : RoomEntityAdapter<TokayShopItem>(item, item.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomBlocker, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public bool Finished=>Entity.Removed;
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLink(linkCenter);
}
