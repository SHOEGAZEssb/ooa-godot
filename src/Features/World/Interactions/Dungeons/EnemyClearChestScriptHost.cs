namespace Oracles;

internal sealed class EnemyClearChestScriptHost(EnemyClearChestRoomEntity owner) : CutsceneCommandHost
{
    public override bool HasActorBinding(CutsceneActorId actor)=>actor.Value=="Chest";
    public override bool DialogueOpen=>owner.ScriptBlocked;
    public override bool ScriptExecutionBlocked=>owner.ScriptBlocked;
    public override bool RoomFlagSet(int flag)=>owner.RoomFlagSet(flag);
    public override bool GateOpen(string gate)=>gate=="NoEnemies"?owner.NoEnemies:
        throw UnsupportedCommand($"read chest gate '{gate}'");
    public override void SetActorCollisionRadii(string actor,int radiusY,int radiusX)
    {
        if(actor!="Chest")throw UnsupportedCommand($"set radii for '{actor}'");
        owner.SetRadii(radiusY,radiusX);
    }
    public override void PlaySound(int sound)=>owner.Sound(sound);
    public override void RunNativeHandler(string handler)=>owner.NativeHandler(handler);
    public override void ScriptEnded()=>owner.EndScript();
}
