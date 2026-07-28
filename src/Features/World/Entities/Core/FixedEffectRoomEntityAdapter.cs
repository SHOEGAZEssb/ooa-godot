using System.Collections.Generic;

namespace oracleofages;

internal class FixedEffectRoomEntityAdapter<T>(T effect)
    : RoomEntityAdapter<T>(effect, effect.SetTransitionDrawOffset),
        IFixedRoomEntity,
        IRoomEntityLifetime
    where T : FixedEffectNode2D
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();
}

/// <summary>
/// Source objects carrying the original always-update-during-text capability.
/// The marker interface's default value is intentionally the only extra policy.
/// </summary>
internal sealed class DialogueFixedEffectRoomEntityAdapter<T>(T effect)
    : FixedEffectRoomEntityAdapter<T>(effect),
        IUpdatesDuringDialogueRoomEntity
    where T : FixedEffectNode2D;
