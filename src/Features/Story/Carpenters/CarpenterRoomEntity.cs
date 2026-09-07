using Godot;

namespace oracleofages;

/// <summary>The event advances this original interaction slot, including native NPC work.</summary>
internal sealed class CarpenterRoomEntity : NpcCharacterRoomEntityAdapter, IRoomBlocker, ITalkTarget,
    IRoomEntityUpdateFreeze
{
    private CarpenterEvent? _event;
    public int ScriptSubid { get; }
    public bool Returned { get; }
    public NpcCharacter Npc => Entity;
    // disableinput writes $81 (Link and all non-interaction objects). The
    // event remains the authoritative owner even after the boss departs.
    public bool FreezesRoomEntities => _event?.BlocksGameplay == true;
    internal void BindEvent(CarpenterEvent owner) => _event = owner;

    public CarpenterRoomEntity(NpcCharacter npc, CarpenterDatabase database,
        OracleRuntimeState runtime, OracleSaveData? save, OracleRoomData room, long tick)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        int found = runtime.ReadWramByte(database.Constant("found-address"));
        int subid = npc.Record.SubId & 0x0f;
        if (save?.HasGlobalFlag(database.Constant("bridge-flag")) == true ||
            (save?.IsLinkedGame == true && !save.HasGlobalFlag(database.Constant("zelda-flag"))))
            npc.SetActive(false);
        ScriptSubid = subid;
        if (subid >= 2)
        {
            bool foundWorker = (found & (1 << subid)) != 0;
            Returned = room.Id == 0x25 && foundWorker;
            if (room.Id == 0x25)
            {
                if (!Returned) npc.SetActive(false);
                else if (found == database.Constant("found-mask")) ScriptSubid += 4;
            }
            else if (foundWorker || save?.ReadWramByte(0xc610) != npc.Record.SubId >> 4)
                npc.SetActive(false);
        }
        else if (subid == 0 && found == database.Constant("found-mask"))
        {
            ScriptSubid = 5;
            npc.SetStatePosition(new(database.Constant("boss-complete-x"), npc.Position.Y));
        }
        npc.SetCollisionRadii(6, 6);
        npc.SetDialogue(0, string.Empty, canFace: false);
        npc.SetScriptButtonSensitive(false);
        if (subid == 1)
        {
            // objectMakeTileSolid changes collision; the following WRAM write
            // changes the logical tile only, retaining the original picture.
            if (npc.Active)
                room.SetPositionTileAndCollision(npc.Position, 0, 0x0f, tick, preserveRenderedTile: true);
            npc.SetBlocksLink(false);
        }
        npc.SetScriptAnimation(database.Script(ScriptSubid).Animation);
    }

    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
