using System.Collections.Generic;

namespace oracleofages;

internal sealed class MapleEncounterRoomEntity(MapleEncounter maple)
    : RoomEntityAdapter<MapleEncounter>(
        maple, maple.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, IPlayerRestriction
{
    public bool Finished => Entity.Finished;
    public bool DisablesSword => Entity.ObjectsDisabled;
    public bool DisablesItems => Entity.ObjectsDisabled;
    public bool DisablesMovement => Entity.ObjectsDisabled;
    public bool DisablesMenus => Entity.MenusDisabled;
    public bool DisablesRingTransformations => Entity.ObjectsDisabled;
    public bool DisablesScreenTransitions =>
        Entity.ScreenTransitionsDisabled;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, spawns, frame.Counter);

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
