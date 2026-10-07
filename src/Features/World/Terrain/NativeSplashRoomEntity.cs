namespace oracleofages;

// breakTileDebris.s subid00 sets enabled bit$80 during state0. The shared
// interaction walk admits it under text, DISABLE_INTERACTIONS $02 and scroll
// mode$08. setInteractionsEnabledTo2 retains that bit on the outgoing owner.
internal sealed class NativeSplashRoomEntity(SplashEffect effect)
    : FixedEffectRoomEntityAdapter<SplashEffect>(effect),
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Entity.UpdateFrame();
}
