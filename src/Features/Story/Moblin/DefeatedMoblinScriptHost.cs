using Godot;

namespace oracleofages;

internal sealed class DefeatedMoblinScriptHost(DefeatedMoblinEvent owner,NpcCharacter actor,int slot,bool remote=false):RoomCutsceneCommandHost
{
    internal NpcCharacter Actor {get;}=actor;
    internal int Slot {get;}=slot;
    internal bool Remote {get;}=remote;
    internal bool Initialized {get;set;}
    internal bool Ended {get;private set;}
    internal int Angle {get;set;}=16;
    private CutsceneCommandRunner? _runner;
    internal CutsceneCommandRunner Runner=>_runner??=new(this);
    public override RoomEventContext Context=>owner.Context;
    public override bool HasActorBinding(CutsceneActorId id)=>id.Value=="Actor";
    public override bool MemoryEquals(string binding,int value)=>ReadMemory(binding)==value;
    public override int ReadMemory(string binding)=>binding=="MoblinSignal"?Context.Entities.RuntimeState.ReadWramByte(0xcfd0):throw UnsupportedCommand(binding);
    public override void WriteMemory(string binding,int value)
    {
        if(binding!="MoblinSignal") throw UnsupportedCommand(binding);
        Context.Entities.RuntimeState.SetWramByte(0xcfd0,checked((byte)value));
    }
    public override void ShowText(int id,string message)=>Context.ShowDialogue(message);
    public override void ShowText(int id,string message,int? position)=>Context.ShowDialogue(message,position);
    public override void SetActorAnimation(string id,int animation,string encoded)=>Actor.SetScriptAnimation(encoded);
    public override void MoveActorAtSpeed(string id,int speed,int angle)
    {
        var position=Actor.Position; OracleObjectMovement.Shared.ApplySpeed(ref position,speed,Angle); Actor.Position=position;
    }
    public override void GiveItem(int treasure,int parameter)=>Context.GrantScriptTreasure(0,9,treasure,parameter,
        "TREASURE_OBJECT_BOMB_FLOWER_00","scripts/ages/scripts.s:kingMoblinDefeated_goron3");
    public override void RunNativeHandler(string handler)
    {
        if(handler=="RemoteMaku") {owner.SpawnRemote();return;}
        if(handler.Length==10 && handler.StartsWith("Direction",System.StringComparison.Ordinal) && handler[9] is >= '1' and <= '3')
        {
            int index=handler[9]-'0';var row=owner.Database.Bytes("directions");
            Angle=row[index*2];Actor.SetScriptAnimation(owner.Database.Animation(row[index*2+1]));return;
        }
        throw UnsupportedCommand(handler);
    }
    public override void ScriptEnded()=>Ended=true;
}
