using Godot;

namespace oracleofages;

internal sealed class OldZoraRoomEntity(OldZoraCharacter actor)
    : RoomEntityAdapter<OldZoraCharacter>(actor, actor.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;
    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
