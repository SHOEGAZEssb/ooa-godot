using System.Collections.Generic;

namespace oracleofages;

internal sealed class BoomerangRoomEntity(BoomerangItem item)
    : RoomEntityAdapter<BoomerangItem>(item, item.SetTransitionDrawOffset), IFixedRoomEntity, IRoomEntityLifetime
{
    internal BoomerangItem Item => Entity;
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        // Only state4 follows wLinkObjectIndex. Homing targets w1Link even
        // while a cart/raft owns the mounted object's position.
        bool copying = Entity.State == 4 && Entity.Counter > 1;
        Entity.UpdateFrame(frame.Player.Position,
            copying ? frame.Player.ActiveLinkObjectPosition : frame.Player.Position,
            copying ? frame.Player.EnemyContactZ : 0);
    }
}
