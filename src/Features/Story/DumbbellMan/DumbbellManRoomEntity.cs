using Godot;

namespace oracleofages;

internal sealed class DumbbellManRoomEntity(DumbbellManCharacter man)
    : RoomEntityAdapter<DumbbellManCharacter>(
        man, man.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
