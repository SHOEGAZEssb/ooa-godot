namespace Oracles;

internal sealed class SharedDungeonScriptHost(SharedDungeonScriptRoomEntity owner) : CutsceneCommandHost
{
    public override bool DialogueOpen => owner.ScriptBlocked;
    public override bool ScriptExecutionBlocked => owner.ScriptBlocked;
    public override bool RoomFlagSet(int flag) => owner.RoomFlag(flag);
    public override void OrRoomFlag(int flag) => owner.SetRoomFlag(flag);
    public override bool GateOpen(string gate) => gate == "NoEnemies" ? owner.EnemyCount() == 0
        : throw UnsupportedCommand($"read shared dungeon gate '{gate}'");
    public override bool MemoryEquals(string binding, int value) => binding == "Torches" ? owner.Torches() == value
        : throw UnsupportedCommand($"read shared dungeon memory '{binding}'");
    public override void PlaySound(int sound) => owner.Sound(sound);
    public override void RunNativeHandler(string handler) => owner.NativeHandler(handler);
    public override void ScriptEnded() => owner.EndScript();
}
