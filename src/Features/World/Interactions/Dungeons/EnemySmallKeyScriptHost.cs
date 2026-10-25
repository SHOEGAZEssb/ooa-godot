namespace Oracles;

internal sealed class EnemySmallKeyScriptHost(DungeonRewardRoomEntity owner) : CutsceneCommandHost
{
    public override bool DialogueOpen => owner.SmallKeyScriptBlocked;
    public override bool ScriptExecutionBlocked => owner.SmallKeyScriptBlocked;
    public override bool RoomFlagSet(int flag) => owner.SmallKeyRoomFlagSet(flag);
    public override bool GateOpen(string gate) => gate == "NoEnemies"
        ? owner.SmallKeyNoEnemies : throw UnsupportedCommand($"read small-key gate '{gate}'");
    public override void RunNativeHandler(string handler)
    {
        if (handler != "SpawnSmallKey") throw UnsupportedCommand($"run small-key handler '{handler}'");
        owner.SpawnSmallKeyFromScript();
    }
    public override void ScriptEnded() => owner.CompleteSmallKeyScript();
}
