using Godot;
using System;

namespace oracleofages;

internal sealed class PalaceFriendlySoldierScriptHost(NpcCharacter actor, PalaceSoldierDatabase database,
    PalaceSoldierRecord record, OracleRuntimeState runtime, Func<bool> textActive,
    Action<int, string, Vector2> showText) : CutsceneCommandHost
{
    private Vector2 _precise = actor.Position;
    private int _direction, _patrolCounter;
    private bool _pressed;
    internal Player Player { get; set; } = null!;
    internal bool InputDisabled => actor.Active && (runtime.ReadWramByte(WramAddress.wDisabledObjects) & 1) != 0;
    internal void PressA() => _pressed = true;
    public override bool DialogueOpen => textActive();
    public override bool ScriptExecutionBlocked => DialogueOpen || Player.IsDying;
    public override bool HasActorBinding(CutsceneActorId binding) => binding.Value == "Soldier";
    public override int ReadMemory(string binding) => binding switch
    {
        "Mode" => record.Mode,
        "PressedA" => _pressed ? 1 : 0,
        "PatrolCounterZero" => (runtime.ReadWramByte(0xcddb) & 0x80) != 0 ? 1 : 0,
        "TimeWarp" => runtime.ReadWramByte(WramAddress.wcde0),
        _ => throw UnsupportedCommand($"read soldier byte '{binding}'")
    };
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override bool TryConsumeActorButton(CutsceneActorId binding)
    {
        bool pressed = _pressed; _pressed = false; return pressed;
    }
    public override void InitializeActorCollisionRadii(string binding) => actor.InitializeCollisionRadii();
    public override void SetActorButtonSensitive(string binding) => actor.SetScriptButtonSensitive(true);
    public override void WriteObjectByte(string binding, int address, int value)
    {
        if (address != 0x31 || value != 0) throw UnsupportedCommand("write soldier object byte");
        _pressed = false;
    }
    public override void SetInputEnabled(bool enabled)
    {
        runtime.SetWramByte(WramAddress.wDisabledObjects, enabled ? (byte)0 : (byte)0x81);
        runtime.SetWramByte(WramAddress.wMenuDisabled, enabled ? (byte)0 : (byte)0x80);
        if (!enabled) { Player.ClearNativeItemParents(); Player.DropHeldItemsForScript(); }
    }
    public override void ShowLoadedText() => showText(record.TextId, record.Message, actor.Position);
    public override void RunNativeHandler(string handler)
    {
        if (handler.StartsWith("Direction:", StringComparison.Ordinal))
        {
            _direction = int.Parse(handler.AsSpan(10)); SetAnimation(_direction); return;
        }
        if (handler.StartsWith("Counter:", StringComparison.Ordinal))
        {
            _patrolCounter = int.Parse(handler.AsSpan(8)); return;
        }
        switch (handler)
        {
            case "InitializeMotion": return; // Native SPEED_80; movement below reads the same imported speed table.
            case "LoadText": actor.SetDialogue(record.TextId, record.Message, true); return;
            case "UnmarkSolid": actor.TimeWarpSolidMarked = false; return;
            case "DecrementCounter":
                _patrolCounter = (_patrolCounter - 1) & 0xff;
                runtime.SetWramByte(0xcddb, (byte)(_patrolCounter == 0 ? 0xc0 : 0x40)); return;
            case "ApplySpeed": actor.SetStatePosition(NativeObjectMovement.ApplySpeed(runtime, ref _precise, 0x14, _direction << 3)); return;
            case "FaceLink": SetAnimation(((OracleObjectMovement.Shared.RelativeAngle(actor.Position, Player.Position) + 4) & 0x18) >> 3); return;
            case "RestorePatrolAnimation": SetAnimation(_direction); return;
            default: throw UnsupportedCommand($"run soldier native '{handler}'");
        }
    }
    private void SetAnimation(int direction) => actor.SetScriptAnimation(database.Animations[direction]);
    public override void ScriptEnded() => actor.SetActive(false);
}
