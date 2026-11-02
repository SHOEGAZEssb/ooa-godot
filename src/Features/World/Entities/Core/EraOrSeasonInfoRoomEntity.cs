
namespace Oracles;

internal sealed class EraOrSeasonInfoRoomEntity(EraOrSeasonInfoDisplay display)
    : RoomEntityAdapter<EraOrSeasonInfoDisplay>(
        display,
        display.SetTransitionDrawOffset),
        INativeInteractionRoomEntity,
        IFixedRoomEntity,
        IRoomEntityLifetime,
        IAlwaysUpdateDuringScreenTransitionRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Entity.UpdateFrame();

}
