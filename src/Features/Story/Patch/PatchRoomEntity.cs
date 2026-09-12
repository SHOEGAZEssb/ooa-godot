using Godot;

namespace oracleofages;

internal sealed class PatchRoomEntity(NpcCharacter npc)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset), IRoomBlocker, ITalkTarget,
      IRoomEntityUpdateFreeze
{
    private PatchEvent? _owner;
    internal NpcCharacter Npc => Entity;
    internal void Bind(PatchEvent owner) => _owner = owner;
    public bool FreezesRoomEntities => _owner?.FreezesObjects == true;
    public bool BlocksLink(Vector2 center) => Entity.Record.SubId < 2 && Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.Record.SubId < 2 && Entity.CanTalkTo(player) ? Entity : null;
}
