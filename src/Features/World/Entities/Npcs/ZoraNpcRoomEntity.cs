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
    private bool _nativeInitialized;
    public NpcCharacter Npc=>Entity;
    public bool RefreshStoryState=>!_initialized||Entity.BaseRecord.SubId<0x0a;
    public bool RefreshVisibility=>!_initialized;
    public void CompleteStoryInitialization()=>_initialized=true;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>
        UpdateNative(frame.Player);

    private void UpdateNative(Player player)
    {
        if (!_nativeInitialized)
        {
            // zora_commonInit returns from state0 through objectSetVisiblec2.
            // The script/facing/collision helper first runs on state1's update.
            _nativeInitialized = true;
            Entity.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex,
                withTerrainEffects: true);
            Entity.ClearFixedDrawPriority();
            return;
        }
        // npcFaceLinkAndAnimate includes objectPreventLinkFromPassing. It
        // resolves arrival overlaps even while Link waits for a palette fade.
        Entity.FaceLinkAndAnimateOneUpdate(player);
    }
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)=>UpdateNative(frame.Player);
    public bool BlocksLink(Vector2 center)=>Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player)=>Entity.CanTalkTo(player)?Entity:null;
}
