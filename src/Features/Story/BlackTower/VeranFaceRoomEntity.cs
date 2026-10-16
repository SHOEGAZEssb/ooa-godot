using Godot;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC $2d owns its always-updating animation/script; the transition owner
// consumes the requested CUTSCENE_03 warp after the normal object pass.
internal sealed class VeranFaceRoomEntity : RoomCutsceneCommandHost,IRoomEntity,IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly VeranFaceDatabase _data;
    private readonly CutsceneCommandRunner _runner;
    private readonly NpcCharacter _actor;
    private bool _ended,_inputLocked;
    public override RoomEventContext Context {get;}
    public Node2D Node=>_actor;
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    internal int State {get;private set;}
    internal NpcCharacter Actor=>_actor;
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    internal VeranFaceRoomEntity(RoomEventContext context,VeranFaceDatabase data,NpcRecord record)
    {
        Context=context;_data=data;_actor=NpcCharacter.CreateFromRecord(record);
        _actor.SetAnimationRate(0);_actor.SetBlocksLink(false);_actor.SetDialogue(0,"",false);
        _actor.SetScriptPaletteOverrides(data.Palettes);_actor.SetScriptAnimation(record.DownAnimation);_runner=new(this);
    }
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;Context.Entities.SetNativeObjectPalette(_data.Palettes);
            _actor.ShowInitializedPresentation();_actor.SetScriptVisible(false);_runner.Start(_data.Commands);return;
        }
        if(State!=1)return;
        _actor.AdvanceAnimationUpdates(1);_runner.AdvanceFrame();
        if(!_ended)return;
        var warp=_data.Warp;
        Context.Transitions.RequestNativeFadeOutWarp(new(Context.Rooms.ActiveGroup,Context.Rooms.CurrentRoom.Id,-1,0,0,
            warp[0]&7,warp[1],warp[3],(warp[2]>>4)&7,warp[2]&15));
        Context.Entities.RuntimeState.SetWramByte(0xcc50,0);State=2;
    }
    public override void SetInputEnabled(bool enabled)
    {
        _inputLocked=!enabled;
        Context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects,(byte)(enabled?0:0x81));
        Context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled,(byte)(enabled?0:0x80));
        if(!enabled)Context.Player.ClearNativeItemParents();
    }
    internal void Cancel()
    {
        // Normal completion intentionally hands the native menu byte to the
        // boss. An interrupted script has no successor to release that byte.
        if(State==2||!_inputLocked)return;
        var runtime=Context.Entities.RuntimeState;
        if(runtime.ReadWramByte(WramAddress.wDisabledObjects)==0x81)runtime.SetWramByte(WramAddress.wDisabledObjects,0);
        if(runtime.ReadWramByte(WramAddress.wMenuDisabled)==0x80)runtime.SetWramByte(WramAddress.wMenuDisabled,0);
        _inputLocked=false;
    }
    public override bool GateOpen(string gate)=>gate=="PaletteDone"?!Context.Transitions.PaletteFadeActive:throw UnsupportedCommand(gate);
    public override void WriteMemory(string binding,int value)
    {
        if(binding!="LinkDirection"||value!=0)throw UnsupportedCommand(binding);
        Context.Player.Face(Vector2I.Up);
    }
    public override void WriteObjectByte(string actor,int address,int value)
    {
        if(address!=0x5a||value!=0x80)throw UnsupportedCommand($"object ${address:x2}=${value:x2}");
        _actor.SetScriptVisible(true);_actor.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex);
    }
    public override void ShowText(int id,string message,int? position)=>Context.ShowDialogue(message,position,sourceTextId:id);
    public override void ScriptEnded()=>_ended=true;
}
