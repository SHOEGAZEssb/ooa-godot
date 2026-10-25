using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class AdvanceShopTheftScriptHost : RoomCutsceneCommandHost
{
    private readonly RoomEventContext _context;
    private readonly NpcCharacter _keeper;
    private readonly Action _releaseInput, _finished;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _precise;
    public override RoomEventContext Context => _context;
    internal AdvanceShopTheftScriptHost(RoomEventContext context, NpcCharacter keeper,
        IReadOnlyList<CutsceneCommand> commands, Action releaseInput, Action finished)
    {
        _context=context;_keeper=keeper;_releaseInput=releaseInput;_finished=finished;
        _precise=keeper.Position;_runner=new(this);_runner.Start(commands);
    }
    internal void Advance() => _runner.AdvanceFrame();
    internal void Cancel() => _runner.Clear();
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value=="Keeper";
    public override void SetActorAnimation(string actor,int animation,string encodedAnimation)
    { RequireActor(actor);_keeper.SetScriptAnimation(encodedAnimation); }
    public override void SetActorMovementAnimation(string actor,int angle,string encodedAnimation)
    { RequireActor(actor);_keeper.SetScriptAnimation(encodedAnimation); }
    public override void MoveActorAtSpeed(string actor,int speed,int angle)
    { RequireActor(actor);_keeper.SetStatePosition(NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref _precise,speed,angle)); }
    public override void ShowText(int textId,string message)
    { if(textId!=0x0e07)throw UnsupportedCommand($"Advance theft TX_{textId:x4}");Context.ShowDialogue(message); }
    public override void SetDisabledObjects(int value)
    { if(value!=0)throw UnsupportedCommand($"Advance theft disabled objects ${value:x2}");_releaseInput(); }
    public override void ScriptEnded() => _finished();
    private void RequireActor(string actor)
    { if(actor!="Keeper")throw UnsupportedCommand($"Advance theft actor '{actor}'"); }
}
