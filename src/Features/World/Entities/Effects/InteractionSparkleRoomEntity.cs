using System.Collections.Generic;

namespace oracleofages;

// INTERAC$84:$00 state0 sets enabled bit$80 and angle-dependent visible81/82. Its handler
// continues during text, disabled-object updates and scrolling.
internal sealed class InteractionSparkleRoomEntity(InteractionSparkleEffect effect) :
    FixedEffectRoomEntityAdapter<InteractionSparkleEffect>(effect),
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity, IScreenTransitionPreloadRoomEntity
{
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Entity.UpdateFrame();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.ElapsedUpdates == 0) Entity.UpdateFrame();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
}
