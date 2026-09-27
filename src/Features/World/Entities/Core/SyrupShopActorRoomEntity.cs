using Godot;

namespace oracleofages;

// syrup.s calls interactionAnimateAsNpc; syrupCucco.s only sets visible80.
// Their native updates are ordered by SyrupShopEvent, including during text.
internal sealed class SyrupShopActorRoomEntity(NpcCharacter npc)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity, IPlayerRestriction
{
    public NpcCharacter Npc => Entity;
    public bool DisablesSword => false;
    public bool DisablesItems => true;
    public bool DisablesRingTransformations => true;
    public bool BlocksLink(Vector2 center) => Entity.Record.Id == 0x5f && Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
