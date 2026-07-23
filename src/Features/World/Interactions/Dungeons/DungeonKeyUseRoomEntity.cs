using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class DungeonKeyUseRoomEntity(DungeonKeyUseEffect effect)
    : RoomEntityAdapter<DungeonKeyUseEffect>(effect, effect.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
