using Godot;

namespace oracleofages;

internal sealed class GoronCaveRoomEntity(NpcCharacter actor, bool interactive = true)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity, IRoomEntityLifetime
{
    public NpcCharacter Npc => Entity;
    public bool Finished => !Entity.Active;
    public bool BlocksLink(Vector2 position) => interactive && Entity.ScriptButtonSensitive && Entity.BlocksLinkCenter(position);
    public NpcCharacter? FindTalkTarget(Player player) => interactive && Entity.CanTalkTo(player) ? Entity : null;
}
