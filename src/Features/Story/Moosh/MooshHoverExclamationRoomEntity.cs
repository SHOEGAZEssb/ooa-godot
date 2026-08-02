using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_EXCLAMATION_MARK $9f:$00 created by Moosh's over-water hover
/// branch. Its copied Z remains fixed while both counters run for $3c updates.
/// </summary>
internal sealed class MooshHoverExclamationRoomEntity(
    NpcCharacter npc,
    int frames)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished { get; private set; }
    internal int Counter { get; private set; } = frames;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        if (Finished)
            return;
        Entity.AnimateAndUpdateDrawPriorityOneUpdate(frame.Player);
        if (--Counter == 0)
            Finished = true;
    }
}
