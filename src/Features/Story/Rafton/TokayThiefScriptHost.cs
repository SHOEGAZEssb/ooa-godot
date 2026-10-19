using Godot;
using System;

namespace oracleofages;

// One interpreter and native continuation per original INTERAC_TOKAY slot.
internal sealed class TokayThiefScriptHost : RoomCutsceneCommandHost
{
    public override RoomEventContext Context { get; }
    internal NpcCharacter Actor { get; }
    internal int SubId { get; }
    internal Vector2 Position;
    internal NpcCharacter? Accessory;
    internal int Stage, Counter, ZFixed, SpeedZ;
    internal bool AnimationFrozen;
    internal CutsceneCommandRunner Runner { get; }
    private readonly Action _makeLinkJump;

    internal TokayThiefScriptHost(RoomEventContext context, TokayTheftEventDatabase database,
        NpcCharacter actor, int subId, Action makeLinkJump)
    {
        Context=context; Actor=actor; SubId=subId; Position=actor.Position; _makeLinkJump=makeLinkJump;
        Runner=new(this);
        Runner.Start(database.Commands, subId == database.Record.MainSubId ? database.MainScriptEntry : 0);
    }

    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Thief";
    public override void SetMenuEnabled(bool enabled) =>
        Context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled, enabled ? (byte)0 : (byte)0x80);
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => Actor.SetScriptAnimation(encodedAnimation);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) => Move(speed,angle);
    internal void Move(int speed,int angle) =>
        Actor.SetStatePosition(NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,ref Position,speed,angle));
    public override void WriteObjectByte(string actor,int address,int value)
    {
        if(address != 0x39 || value is <0 or >1) throw UnsupportedCommand($"write Tokay object byte${address:x2}=${value:x2}");
        AnimationFrozen=value!=0;
    }
    public override void WriteMemory(string binding,int value)
    {
        if(binding != "TokayJumpSignal") throw UnsupportedCommand($"write '{binding}'");
        Context.Entities.RuntimeState.SetWramByte(0xcfd1,(byte)value);
    }
    public override void RunNativeHandler(string handler)
    {
        if(handler != "TokayMakeLinkJump") throw UnsupportedCommand($"run '{handler}'");
        _makeLinkJump();
    }
    public override void ScriptEnded() { }

    internal void AdvanceScript()
    {
        // Native animation precedes interactionRunScript, including its live
        // text gate. Read the speed left by the previous script update.
        if(!AnimationFrozen)
        {
            int speed=Runner.ActorSpeed(new("Thief"));
            Actor.AdvanceAnimationUpdates(Runner.MovementCounter("Thief")==0?1:
                speed>=0x78?4:speed>=0x50?3:speed>=0x28?2:1);
        }
        Runner.AdvanceFrame();
    }

    internal void ApplyJumpAnimationAbi(TokayJumpAnimationAbi animation)
    {
        int slot=Context.Entities.InteractionSlot(Actor), page=0xd000+slot*0x100;
        var memory=Context.Entities.RuntimeState;
        // $d7+ prefixes alias live ITEM parents; their ID/animation owner must
        // be represented before accepting a displaced thief in those pages.
        if(slot is <2 or >6 || memory.ReadWramByte(page+1)!=animation.Id)
            throw new NotSupportedException($"{animation.Source}: Tokay$48:${SubId:x2} at page${page:x4} aliases an unrepresented SPECIALOBJECT/ITEM animation owner in room$1:$aa.");
        memory.SetWramByte(page+0x30,(byte)animation.Mode);
        memory.SetWramByte(page+0x20,(byte)(animation.Duration-1));
        memory.SetWramByte(page+0x21,(byte)animation.Parameter);
        memory.SetWramByte(page+0x22,(byte)animation.Pointer);
        memory.SetWramByte(page+0x23,(byte)(animation.Pointer>>8));
        memory.SetWramByte(page+0x31,(byte)animation.Graphic);
        Actor.SetAnimationCounter(1);
    }
}
