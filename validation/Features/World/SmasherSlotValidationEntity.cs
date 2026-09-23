using System;
using System.Collections.Generic;

namespace oracleofages;

// Allocation-only fixture: all orchestration stays in the validation assembly.
internal sealed class SmasherSlotValidationEntity(SmasherCharacter actor,
    Action<SmasherCharacter, RoomEntityFrame> dispatch)
    : RoomEntityAdapter<SmasherCharacter>(actor, actor.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity
{
    public bool Finished { get; set; }
    public bool CountsAsEnemy => false;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => dispatch(Entity, frame);
}
