using System;
using System.Collections.Generic;

namespace oracleofages;

// Uses a real INTERAC_PUFF node for native allocation; dispatch orchestration
// stays in the validation assembly and does not change production handlers.
internal sealed class InteractionSlotValidationEntity(PuzzlePuffEffect actor,
    Action<InteractionSlotValidationEntity> dispatch)
    : RoomEntityAdapter<PuzzlePuffEffect>(actor, actor.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity
{
    internal int Updates { get; private set; }
    public bool Finished { get; set; }
    public bool UpdatesDuringDialogue => Updates == 0;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Updates++;
        dispatch(this);
    }
}
