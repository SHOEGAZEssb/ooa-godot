using Godot;

namespace oracleofages;

internal sealed class SymmetryRoomEntity : NpcCharacterRoomEntityAdapter, IRoomBlocker, ITalkTarget,
    IRoomEntityUpdateFreeze
{
    private SymmetryEvent? _owner;
    public NpcCharacter Npc => Entity;
    public bool FreezesRoomEntities => _owner?.BlocksGameplay == true;
    internal void Bind(SymmetryEvent owner) => _owner = owner;

    public SymmetryRoomEntity(NpcCharacter npc, SymmetryDatabase database, OracleSaveData? save)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        if (npc.Record.SubId == 0x0c && save?.HasGlobalFlag(database.Constant("placed-flag")) != true)
            npc.SetActive(false);
        npc.SetDialogue(0, string.Empty, canFace: true);
        npc.SetScriptButtonSensitive(false);
    }

    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
