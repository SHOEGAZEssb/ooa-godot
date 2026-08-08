using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BabyCuccoRoomEntity(BabyCuccoCharacter cucco)
    : RoomEntityAdapter<BabyCuccoCharacter>(
        cucco, cucco.SetTransitionDrawOffset),
        IFixedRoomEntity, IBraceletInteractableRoomEntity,
        IRoomEntityLifetime, IRoomEnemyCounterEntity
{
    public bool Finished => Entity.IsDead;
    public bool CountsAsEnemy => !Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player, spawns);

    public bool TryUseBracelet(Player player, Vector2I releaseDirection) =>
        Entity.TryUseBracelet(player, releaseDirection);
}
