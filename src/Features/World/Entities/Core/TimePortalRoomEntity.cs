using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TimePortalRoomEntity(TimePortal portal, Action<TimePortal> entered)
    : RoomEntityAdapter<TimePortal>(portal, portal.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity
{
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter);
    public void HandleLinkContact(Player player)
    {
        if (Entity.CheckLinkContact(player.Position))
            entered(Entity);
    }
}
