using System.Collections.Generic;

namespace oracleofages;

internal sealed class FallingBoulderRoomEntity(FallingBoulder boulder)
    : RoomEntityAdapter<FallingBoulder>(boulder, boulder.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity, IUpdatesDuringDialogueRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // updateParts permits state 0 while scrolling; its delay remains frozen.
        if (Entity.State == 0) Entity.UpdateFrame(0);
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame.Counter);
    public void HandleLinkContact(Player player) => Entity.HandleLinkContact(player);
}
