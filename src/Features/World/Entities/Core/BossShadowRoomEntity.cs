using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BossShadowRoomEntity(BossShadowEffect shadow)
    : RoomEntityAdapter<BossShadowEffect>(shadow, shadow.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
