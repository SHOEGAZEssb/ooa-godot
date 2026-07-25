using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class EraInfoRoomEntity(EraInfoDisplay display)
    : RoomEntityAdapter<EraInfoDisplay>(
        display,
        display.SetTransitionDrawOffset),
        IFixedRoomEntity,
        IRoomEntityLifetime,
        IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public void UpdateDuringScreenTransition() => Entity.UpdateFrame();

    public void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
    }
}
