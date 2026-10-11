using Godot;
using System.Collections.Generic;

namespace oracleofages;

// zora.s selects deletion gates at state zero. Palace Zoras reselect text
// each update; ocean Zoras retain the pollution text selected at entry.
internal sealed class ZoraNpcRoomEntity(NpcCharacter actor)
    : NpcCharacterRoomEntityAdapter(actor,actor.SetTransitionDrawOffset),
      IFixedRoomEntity,IRoomBlocker,ITalkTarget,IOrdinaryNpcEntity,
      IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze,
      IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private bool _initialized;
    public NpcCharacter Npc=>Entity;
    public bool RefreshStoryState=>!_initialized||Entity.BaseRecord.SubId<0x0a;
    public bool RefreshVisibility=>!_initialized;
    public void CompleteStoryInitialization()=>_initialized=true;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>
        Entity.FaceLinkAndAnimateOneUpdate(frame.Player,preventPassing:false);
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)=>Entity.FaceLinkAndAnimateOneUpdate(frame.Player,preventPassing:false);
    public bool BlocksLink(Vector2 center)=>Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player)=>Entity.CanTalkTo(player)?Entity:null;
}
