namespace oracleofages;

internal sealed class TokayEyeballSlotScriptHost(TokayEyeballSlotRoomEntity owner) : CutsceneCommandHost
{
    public override bool DialogueOpen => owner.ScriptBlocked;
    public override bool ScriptExecutionBlocked => owner.ScriptBlocked;
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Socket";
    public override void OrRoomFlag(int flag) => owner.WriteScriptRoomFlag(flag);
    public override void PlaySound(int sound) => owner.PlayScriptSound(sound);
    public override void SetMusic(int music)
    {
        if (music != 0xff) throw UnsupportedCommand($"set socket music ${music:x2}");
        owner.ResetScriptMusic();
    }
    public override void SetActorCoordinates(string actor, int y, int x) => owner.Position = new(x, y);
    public override void RunNativeHandler(string handler)
    {
        if (!owner.RunScriptNative(handler)) throw UnsupportedCommand($"run socket handler '{handler}'");
    }
    public override void SetInputEnabled(bool enabled)
    {
        if (!enabled) throw UnsupportedCommand("disable socket input outside its accepted native prefix");
        owner.EnableScriptInput();
    }
    public override void ScriptEnded() => owner.CompleteScript();
}
