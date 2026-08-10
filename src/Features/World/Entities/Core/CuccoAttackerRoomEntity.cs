using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CuccoAttackerRoomEntity(
    CuccoAttackerCharacter attacker)
    : RoomEntityAdapter<CuccoAttackerCharacter>(
        attacker, attacker.SetTransitionDrawOffset),
        IFixedRoomEntity, ILinkContactEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.IsDead;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position);

    public void HandleLinkContact(Player player)
    {
        if (Entity.OverlapsLink(player.Position))
            player.ApplyEnemyContactDamage(Entity.Position, 4);
    }
}
