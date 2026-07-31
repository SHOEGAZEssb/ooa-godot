using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ItemDropRoomEntity(
    ItemDropEffect drop,
    Action<Vector2, HazardType> enteredHazard)
    : RoomEntityAdapter<ItemDropEffect>(drop, drop.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        ILinkSwordCollectibleRoomEntity
{
    public bool Finished => Entity.Finished;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player, frame.Counter);
        HazardType pending = Entity.TakePendingHazardEffect();
        if (pending != HazardType.None)
            enteredHazard(Entity.Position, pending);
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.FinishedHazard is
            HazardType.Water or HazardType.Lava)
        {
            enteredHazard(Entity.Position, Entity.FinishedHazard);
        }
        else if (Entity.FinishedHazard == HazardType.Hole)
        {
            spawns.Add(new FallingDownHoleSpawn(Entity.Position));
        }
    }

    public bool TryCollectWithSword(Rect2 hitbox) =>
        Entity.TryCollectWithSword(hitbox);
}
