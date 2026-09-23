using System.Collections.Generic;

namespace oracleofages;

// breakTileDebris.s sets the always-update bit for INTERAC$05 subids with
// bit 1 clear (all variants represented by PuzzlePuffSpawn). Outgoing
// enabled=$02 does not delete this interaction before its terminal frame.
internal sealed class PuzzlePuffRoomEntity(PuzzlePuffEffect effect)
    : FixedEffectRoomEntityAdapter<PuzzlePuffEffect>(effect),
        IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze,
        IAlwaysUpdateDuringScreenTransitionRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    public void UpdateDuringScreenTransition() => Entity.UpdateFrame();

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Initialized)
            Entity.UpdateFrame();
        return Entity.Visible
            ? ScreenTransitionPresentation.Visible
            : ScreenTransitionPresentation.Hidden;
    }
}
