using Godot;

namespace oracleofages;

internal sealed class PlenRoomEntity : NpcCharacterRoomEntityAdapter, IRoomBlocker, ITalkTarget
{
    public NpcCharacter Npc => Entity;

    public PlenRoomEntity(NpcCharacter npc) : base(npc, npc.SetTransitionDrawOffset)
    {
        npc.SetDialogue(0, string.Empty, canFace: false);
        npc.SetScriptButtonSensitive(false);
    }

    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
