using Godot;

namespace Oracles;

internal sealed class SymmetryRoomEntity : NpcCharacterRoomEntityAdapter, IRoomBlocker, ITalkTarget,
    IRoomEntityUpdateFreeze
{
    private SymmetryEvent? _owner;
    public NpcCharacter Npc => Entity;
    public bool FreezesRoomEntities => _owner?.BlocksGameplay == true;
    // Scripts use$91 and the nut uses$01; neither disables INTERACTIONs.
    public bool FreezesInteractions => false;
    internal void Bind(SymmetryEvent owner) => _owner = owner;

    public SymmetryRoomEntity(NpcCharacter npc, SymmetryDatabase database, OracleSaveData? save)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        if (npc.Record.SubId == 0x0c && save?.HasGlobalFlag(database.Constant("placed-flag")) != true)
            npc.SetActive(false);
        npc.SetDialogue(0, string.Empty, canFace: true);
        npc.SetScriptButtonSensitive(false);
        // symmetryNpc.s state 0: objectSetVisible82, independently of the
        // script's later execution. The subid $0c deletion still wins.
        npc.ShowInitializedPresentation();
    }

    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
