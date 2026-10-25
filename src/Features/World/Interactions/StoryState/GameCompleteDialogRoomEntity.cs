using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Shared INTERAC_GAME_COMPLETE_DIALOG $d1. scriptend leaves this invisible
// interaction allocated until the native ending resets the game.
internal sealed class GameCompleteDialogRoomEntity : RoomCutsceneCommandHost,
    IRoomEntity,IFixedRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly CutsceneCommandRunner _runner;
    public override RoomEventContext Context {get;}
    internal GameCompleteDialogRoomEntity(RoomEventContext context){Context=context;_runner=new(this);}
    internal int State {get;private set;}
    public Node2D Node {get;}=new(){Visible=false};
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset){}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;DeathRespawnPresets.Load(Context.Rooms.SaveData,1);
            _runner.Start(CutsceneCommandCatalog.Load(AgesGameContent.AssetRoot + "cutscenes/game_complete_dialog_commands.tsv"));return;
        }
        _runner.AdvanceFrame();
    }
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Actor";
    public override void ShowText(int id,string message,int? position)
        =>Context.ShowChoiceDialogue(message,textboxPosition:position,sourceTextId:id);
    public override bool TextOptionEquals(int value)=>EventResources.RequireDialogueChoice("INTERAC $d1: missing completion dialogue choice.")==value;
    public override void WriteMemory(string binding,int value)
    {
        if(binding!="CompleteDialogDone")throw UnsupportedCommand(binding);
        Context.Entities.RuntimeState.SetWramByte(0xcfde,(byte)value);
    }
    public override void RunNativeHandler(string handler)
    {
        if(handler=="MarkGameComplete")Context.Rooms.SaveData.MarkGameCompleted();
        else if(handler=="SaveFile")
        {
            var result=Context.SaveFile();
            if(!result.Success)throw new InvalidOperationException($"INTERAC $d1 saveFile failed: {result.ErrorMessage}");
        }
        else throw UnsupportedCommand(handler);
    }
    public override void ScriptEnded(){}
}
