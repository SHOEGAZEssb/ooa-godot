
namespace Oracles;

internal sealed class MapleEncounterRoomEntity(MapleEncounter maple, RoomEntityManager owner)
    : RoomEntityAdapter<MapleEncounter>(
        maple, maple.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, IPlayerRestriction,
        IScreenTransitionPreloadRoomEntity, INativeSpecialObjectRoomEntity
{
    int INativeSpecialObjectRoomEntity.NativeSpecialObjectId => SpecialObjectId.Maple;
    Godot.Vector2 INativeSpecialObjectRoomEntity.NativeSpecialObjectPosition
    {
        get => Entity.Stage == MapleEncounterStage.Initializing ? Entity.BirthPosition : Entity.BookTargetPosition;
        set => Entity.SetNativePosition(value);
    }
    // specialObjectCode_maple calls companionRetIfInactiveWithoutStateCheck:
    // wScrollMode & $0e suppresses even state0. Do not consume its RNG or
    // entry delay while preparing the incoming screen; retained visibility stays.
    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) => Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;

    internal MapleEncounter Maple => Entity;
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
        ICollection<RoomEntitySpawn> spawns)
    {
        // commonCode.s: companionRetIfInactiveWithoutStateCheck applies even
        // to state0, independently of the interaction pass containing this adapter.
        if ((owner.RuntimeState.ReadField("wDisabledObjects") & 0xa0) != 0 ||
            owner.PaletteFadeActiveSource() || owner.NonInteractionObjectsDisabledSource())
            return;
        Entity.UpdateFrame(frame.Player, spawns, frame.Counter,
            owner.RuntimeState.ReadField("wMenuDisabled") != 0);
    }

}
