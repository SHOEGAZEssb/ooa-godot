using System;
using System.Linq;

namespace oracleofages;

internal sealed class DimitriRescueScriptHost : CutsceneCommandHost
{
    private readonly DimitriRescueScriptRoomEntity _slot;
    private readonly RoomSession _rooms;
    private readonly RoomEntityManager _entities;
    private readonly DialogueBox _dialogue;
    private readonly CutsceneCommandRunner _runner;
    private Player? _player;
    private bool _ownsInput;
    internal int NativeCounter1=>_runner.NativeCounter1;
    internal int CurrentCommandIndex=>_runner.CurrentCommand?.Source.CommandIndex??-1;
    internal DimitriRescueScriptHost(DimitriRescueScriptRoomEntity slot,RoomSession rooms,RoomEntityManager entities,DialogueBox dialogue)
    {
        _slot=slot;_rooms=rooms;_entities=entities;_dialogue=dialogue;
        _runner=new(this);_runner.Start(CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/dimitri_rescue_commands.tsv"));
    }
    private DimitriCompanionRoomEntity? Companion=>_entities.Entities<DimitriCompanionRoomEntity>().SingleOrDefault();
    internal void Advance(Player player){_player=player;_runner.AdvanceFrame();}
    public override bool DialogueOpen=>_dialogue.IsOpen;
    public override bool ScriptExecutionBlocked=>_dialogue.IsOpen&&!_dialogue.AllowsNativeScriptCommands||_player?.IsDying==true;
    public override bool IsLinkedGame=>_rooms.SaveData.IsLinkedGame;
    public override int FrameCounter=>_entities.FrameCounter;
    public override int ReadMemory(string binding)
    {
        if(binding.StartsWith("DimitriMask:",StringComparison.Ordinal))return (_rooms.SaveData.ReadWramByte(WramAddress.wDimitriState)&Convert.ToInt32(binding.AsSpan(12).ToString(),16))!=0?1:0;
        if(binding.StartsWith("CompanionMask:",StringComparison.Ordinal))return (CompanionRuntimeState.ReadScriptSignals(_entities.RuntimeState)&Convert.ToInt32(binding.AsSpan(14).ToString(),16))!=0?1:0;
        return binding switch
        {
            "CompanionButton"=>CompanionRuntimeState.ReadScriptButton(_entities.RuntimeState),
            "LinkMounted"=>Companion?.LinkRiding==true?1:0,
            "AnimalCompanion"=>_rooms.SaveData.ReadWramByte(WramAddress.wAnimalCompanion),
            _=>throw UnsupportedCommand($"read '{binding}'")
        };
    }
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override void WriteMemory(string binding,int value)
    {
        if(binding!="CompanionButton"||value!=0)throw UnsupportedCommand($"write '{binding}'=${value:x2}");
        Companion?.AcknowledgeNativeRescueInput();
        CompanionRuntimeState.WriteScriptButton(_entities.RuntimeState,0);
    }
    public override void ShowText(int id,string message)=>ShowText(id,message,null);
    public override void ShowText(int id,string message,int? position)=>_dialogue.ShowGameplayMessage(message,
        _entities.WorldToScreen((_player??throw UnsupportedCommand("show text without Link")).Position).Y,position??0);
    public override void SetInputEnabled(bool enabled)
    {
        Companion?.AcknowledgeNativeRescueInput();
        NativeScriptInputControl.SetEnabled(_entities.RuntimeState,_player??throw UnsupportedCommand("write input without Link"),enabled);
        _ownsInput=!enabled;
    }
    public override void SetDisabledObjects(int value)=>_entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects,checked((byte)value));
    public override void RunNativeHandler(string handler)
    {
        if(handler.StartsWith("OrDimitri:",StringComparison.Ordinal))
        {
            int mask=Convert.ToInt32(handler.AsSpan(10).ToString(),16);
            _rooms.SaveData.WriteWramByte(WramAddress.wDimitriState,(byte)(_rooms.SaveData.ReadWramByte(WramAddress.wDimitriState)|mask));return;
        }
        if(handler=="ForceMount")
        {
            (Companion??throw UnsupportedCommand("force mount without SPECIALOBJECT_DIMITRI $0c")).ForceForestMount();return;
        }
        throw UnsupportedCommand($"run '{handler}'");
    }
    public override void ScriptEnded(){_ownsInput=false;_slot.End();}
    internal void LinkDied()
    {
        _entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects,0);_ownsInput=false;
        Companion?.ReleaseNativeRescueInput();_runner.Clear();
    }
    internal void Cancel()
    {
        if(_ownsInput&&_player is not null)SetInputEnabled(true);
        Companion?.ReleaseNativeRescueInput();_runner.Clear();
    }
}
