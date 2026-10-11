using Godot;
using System;

namespace oracleofages;

internal sealed class LibraryReaderScriptHost(NpcCharacter npc, LibraryDatabase database,
    OracleSaveData? save, RoomEntityManager entities) : CutsceneCommandHost
{
    private bool _pressed;
    internal Player Player { get; set; } = null!;
    internal bool PressA()
    {
        if (_pressed) return false;
        _pressed = true;
        return true;
    }
    public override bool DialogueOpen => entities.IsTextActive();
    public override bool ScriptExecutionBlocked => entities.IsScriptTextActive() || Player.IsDying;
    public override int FrameCounter => entities.FrameCounter;
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Library";
    public override void InitializeActorCollisionRadii(string actor) => npc.InitializeCollisionRadii();
    public override void SetActorButtonSensitive(string actor)
    {
        // objectAddToAButtonSensitiveObjectList clears the pressed-A byte.
        _pressed = false;
        npc.SetScriptButtonSensitive(true);
    }
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool pressed = _pressed; _pressed = false; return pressed;
    }
    public override void ShowLoadedText() => entities.OnRoomEntityDialogueRequested(npc.TextId, npc.Message, Player.Position);
    public override void ShowText(int id, string message, int? position) => entities.OnRoomEntityDialogueRequested(id, message, Player.Position);
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => npc.SetScriptAnimation(encodedAnimation);
    public override bool MemoryEquals(string binding, int value)
    {
        if (binding.StartsWith("Global:", StringComparison.Ordinal))
            return (save?.HasGlobalFlag(int.Parse(binding.AsSpan(7))) == true ? 1 : 0) == value;
        throw UnsupportedCommand($"read library reader '{binding}'");
    }
    public override void RunNativeHandler(string handler)
    {
        if (handler != "FaceLink") throw UnsupportedCommand(handler);
        Vector2 target = entities.ActiveScentSeedTarget() ?? Player.Position;
        int animation = ((OracleObjectMovement.Shared.RelativeAngle(npc.Position, target) + 4) & ObjectAngle.CardinalMask) >> 3;
        npc.SetScriptAnimation(database.Animation(npc.Record.Id, animation, npc.Record.SubId));
    }
}
