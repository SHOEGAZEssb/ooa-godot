using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GroundTreasureRoomEntity(
    GroundTreasurePickup treasure,
    Func<bool> collectionAllowed,
    Action<GroundTreasurePickup, Player> collected)
    : RoomEntityAdapter<GroundTreasurePickup>(
        treasure, treasure.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IDugTileRoomEntity
{
    public bool Finished => Entity.Finished;
    bool IUpdatesDuringDialogueRoomEntity.UpdatesDuringDialogue =>
        Entity.UpdatesDuringDialogue;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);

    public void HandleLinkContact(Player player)
    {
        if (collectionAllowed() && Entity.TryCollect(player))
            collected(Entity, player);
    }

    public void NotifyTileDug(int packedPosition) =>
        Entity.NotifyTileDug(packedPosition);

}
