using Godot;
using System;

namespace Oracles;

internal sealed class DimitriDepartureScriptHost : CutsceneCommandHost
{
    private readonly DimitriDepartureRoomEntity _slot;
    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly DimitriDepartureDatabase _data;
    private readonly NativeScriptInputControl _control;
    private readonly CutsceneCommandRunner _runner;
    private Player? _player;
    internal DimitriDepartureScriptHost(DimitriDepartureRoomEntity slot,RoomSession rooms,RoomEntityManager entities,DimitriDepartureDatabase data)
    {_slot=slot;_rooms=rooms;_entities=entities;_data=data;_control=new(slot.Memory);_runner=new(this);}
    internal void Begin()=>_runner.Start(CutsceneCommandCatalog.Load(AgesGameContent.AssetRoot+"cutscenes/dimitri_departure_commands.tsv"));
    internal void Advance(Player player){_player=player;_runner.AdvanceFrame();}
    internal void SetTransitions(byte value)=>_control.SetScreenTransitionsDisabled(value);
    internal void Relinquish()=>_control.Relinquish();
    internal void Cancel(){_control.Cancel();_runner.Clear();}
    public override bool DialogueOpen=>_entities.IsTextActive();
    public override bool ScriptExecutionBlocked=>_entities.IsScriptTextActive();
    public override bool IsLinkedGame=>_rooms.SaveData.IsLinkedGame;
    public override int FrameCounter=>_entities.FrameCounter;
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override int ReadMemory(string binding)=>binding switch
    {
        "LinkObject"=>_slot.Memory.ReadField("wLinkObjectIndex"),
        // This source command only tests zero; Link owns the active air mode.
        "LinkInAir"=>Player.TopDownAirborne?1:0,
        _=>throw UnsupportedCommand($"read '{binding}'")
    };
    public override void WriteMemory(string binding,int value)
    {
        switch(binding,value)
        {
            case ("SimulatedInput",0):_slot.Memory.SetField("wUseSimulatedInput",0);break;
            case ("CompanionVar03",3):_slot.Memory.SetWramByte(0xd103,3);break;
            case ("ScreenTransitions",0):_control.SetScreenTransitionsDisabled(0);break;
            default:throw UnsupportedCommand($"write '{binding}'=${value:x2}");
        }
    }
    public override void SetDisabledObjects(int value)=>_control.SetDisabledObjects(checked((byte)value));
    public override void ShowText(int id,string message)=>_entities.OnRoomEntityDialogueRequested(id,message,Player.Position);
    public override void RunNativeHandler(string handler)
    {
        if(handler=="DisableMenu")_control.SetMenuEnabled(Player,false);
        else if(handler=="FaceLink")
        {
            Vector2 target=_entities.ActiveScentSeedTarget()??Player.Position;
            int direction=((OracleObjectMovement.Shared.RelativeAngle(_slot.NativePosition,target)+4)&0x18)>>3;
            _slot.SetFacing(_data.Facing(direction));
        }
        else throw UnsupportedCommand(handler);
    }
    public override void ScriptEnded()=>_slot.EndScript();
    private Player Player=>_player??throw new InvalidOperationException("$71:$06 script has no current Link visit.");
}
