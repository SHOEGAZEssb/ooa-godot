using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class TimePortalRoomEntity(TimePortal portal, Action<TimePortal> entered)
    : RoomEntityAdapter<TimePortal>(portal, portal.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity,
        IUpdatesDuringDialogueRoomEntity, IRoomEntityLifetime,
        IScreenTransitionPreloadRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    // INTERAC $e1 sets enabled bit 7. Temporary INTERAC $de does not;
    // it receives only the ordinary state-zero exception under text/$02.
    public bool UpdatesDuringDialogue => !Entity.Temporary || Entity.NativeInitializationPending;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;
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
