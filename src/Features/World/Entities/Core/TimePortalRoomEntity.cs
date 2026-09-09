using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TimePortalRoomEntity(TimePortal portal, Action<TimePortal> entered)
    : RoomEntityAdapter<TimePortal>(portal, portal.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity,
        IUpdatesDuringDialogueRoomEntity, IRoomEntityLifetime,
        IScreenTransitionPreloadRoomEntity
{
    public bool Finished => Entity.Entered || Entity.Expired;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter, frame.Player);
    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.PrepareForScreenTransition();
    public void HandleLinkContact(Player player)
    {
        if (player.AcceptsTimePortalContact && Entity.CheckLinkContact(player.Position))
            entered(Entity);
    }
}
