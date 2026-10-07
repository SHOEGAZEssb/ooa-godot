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
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IDugTileRoomEntity,
        IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    public bool Finished => Entity.Finished;
    bool IUpdatesDuringDialogueRoomEntity.UpdatesDuringDialogue =>
        Entity.UpdatesDuringDialogue;
    bool IUpdatesDuringRoomEntityFreeze.UpdatesDuringRoomEntityFreeze => Entity.UpdatesDuringDialogue;

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeGraphicsState();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (Entity.UpdatesDuringDialogue) Entity.UpdateFrame(frame.Player);
    }

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
