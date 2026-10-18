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
        IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private bool _initialized;
    public bool Finished { get; private set; }
    internal int Counter { get; private set; } = frames;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        if (Finished)
            return;
        // INTERAC $9f state 0 sets enabled bit 7 and returns at counter $3c.
        if (!_initialized)
        {
            _initialized = true;
            Entity.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex);
            return;
        }
        if (--Counter == 0)
            Finished = true;
        else
            Entity.AdvanceAnimationUpdates(1);
    }
}
