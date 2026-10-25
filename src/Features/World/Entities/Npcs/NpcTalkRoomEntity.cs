using Godot;

namespace Oracles;

// Event owners advance the actor; this adapter only supplies blocking and talk selection.
internal class NpcTalkRoomEntity(NpcCharacter npc, bool scriptSensitive = false)
    : RoomEntityAdapter<NpcCharacter>(npc, npc.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        (scriptSensitive
            ? Entity.CanScriptTalkTo(player, NpcCharacter.CollisionRadius,
                NpcCharacter.CollisionRadius, NpcCharacter.AButtonPointOffset)
            : Entity.CanTalkTo(player)) ? Entity : null;
}
