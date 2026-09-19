using System.Collections.Generic;

namespace oracleofages;

internal sealed class KingMoblinMinionRoomEntity(KingMoblinMinion actor)
    : RoomEntityAdapter<KingMoblinMinion>(actor,actor.SetTransitionDrawOffset), IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity
{
    public bool Finished => Entity.IsDead;
    public bool CountsAsEnemy => false;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame,spawns);
}
