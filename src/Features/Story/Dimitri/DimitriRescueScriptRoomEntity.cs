using Godot;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_COMPANION_SCRIPTS $71:$07 is an independent ordinary interaction,
// not part of Dimitri's earlier SPECIALOBJECT update or either Tokay slot.
internal sealed class DimitriRescueScriptRoomEntity(OracleSaveData save)
    : RoomEntityAdapter<Node2D>(new Node2D { Name="DimitriRescueScript_71_07",Visible=false },static _=>{}),
      IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze,
      IScreenTransitionPreloadRoomEntity
{
    private bool _initialized;
    internal DimitriRescueScriptHost Script { get; set; } = null!;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue=>!_initialized;
    public bool UpdatesDuringRoomEntityFreeze=>!_initialized;
    internal void End()=>Finished=true;
    private void Initialize(){_initialized=true;if((save.ReadWramByte(WramAddress.wDimitriState)&0x20)!=0)Finished=true;}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(frame.Player.IsDying){Script.LinkDied();Finished=true;return;}
        if(!_initialized){Initialize();return;}
        Script.Advance(frame.Player);
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {if(!_initialized)Initialize();return ScreenTransitionPresentation.Hidden;}
}
