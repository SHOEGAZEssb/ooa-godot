namespace Oracles;

// breakTileDebris.s: subid $00 retains enabled bit 7 through text, masks and scrolls.
internal sealed class ShovelDebrisRoomEntity(ShovelDebrisEffect effect)
    : FixedEffectRoomEntityAdapter<ShovelDebrisEffect>(effect),
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IAlwaysUpdateDuringScreenTransitionRoomEntity, IScreenTransitionPreloadRoomEntity
{
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Entity.UpdateFrame();
    public ScreenTransitionPresentation PrepareForScreenTransition(System.Collections.Generic.ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Initialized) Entity.UpdateFrame();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
}
