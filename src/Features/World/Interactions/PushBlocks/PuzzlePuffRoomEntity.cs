using System.Collections.Generic;

namespace oracleofages;

// breakTileDebris.s sets the always-update bit for INTERAC$05 subids with
// bit 1 clear. Spark/Whisp use subid$02, which has ordinary eligibility. Outgoing
// enabled=$02 does not delete this interaction before its terminal frame.
internal sealed class PuzzlePuffRoomEntity(PuzzlePuffEffect effect)
    : FixedEffectRoomEntityAdapter<PuzzlePuffEffect>(effect),
        IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze,
        IAlwaysUpdateDuringScreenTransitionRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    public bool UpdatesDuringDialogue => !Entity.Initialized || Entity.AlwaysUpdates;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;
    // smog.s can write Interaction.counter2 ($47) on this puff's page.
    // breakTileDebris.s never reads it: lifetime uses animParameter instead.
    internal byte Counter2Alias { get; private set; }
    internal void WriteCounter2Alias(int value) => Counter2Alias = unchecked((byte)value);

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (UpdatesDuringDialogue) Entity.UpdateFrame();
    }

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
