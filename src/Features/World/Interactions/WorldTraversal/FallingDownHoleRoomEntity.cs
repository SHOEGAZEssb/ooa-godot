namespace oracleofages;

/// <summary>
/// INTERAC_FALLDOWNHOLE $0f sets interactionSetAlwaysUpdateBit in state 0.
/// Its animation continues through text and DISABLE_ALL_BUT_INTERACTIONS,
/// including the interval after its hole-event signal triggers Patch's win.
/// </summary>
internal sealed class FallingDownHoleRoomEntity(FallingDownHoleEffect effect)
    : FixedEffectRoomEntityAdapter<FallingDownHoleEffect>(effect),
        IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze;
