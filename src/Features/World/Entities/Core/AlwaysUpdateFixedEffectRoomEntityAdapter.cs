namespace Oracles;

// INTERACTION enabled bit 7 admits this effect during text and category masks.
internal sealed class AlwaysUpdateFixedEffectRoomEntityAdapter<T>(T effect)
    : FixedEffectRoomEntityAdapter<T>(effect),
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
    where T : FixedEffectNode2D;
