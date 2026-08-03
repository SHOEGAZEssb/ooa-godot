using Godot;

namespace oracleofages;

internal sealed class RalphAfterRaftonRoomEntity(
    RalphAfterRaftonCharacter ralph)
    : NpcCharacterRoomEntityAdapter(ralph, ralph.SetTransitionDrawOffset),
        IRoomBlocker,
        IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);
}
