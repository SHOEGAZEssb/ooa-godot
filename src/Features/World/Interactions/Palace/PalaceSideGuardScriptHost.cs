using Godot;
using System.Collections.Generic;

namespace oracleofages;

// soldier.s:soldierSubid03 / scripts.s:soldierSubid03Script.
internal sealed class PalaceSideGuardScriptHost : CutsceneCommandHost
{
    private readonly NpcCharacter _guard;
    private readonly RoomEventContext _context;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _precise;

    internal PalaceSideGuardScriptHost(NpcCharacter guard, RoomEventContext context,
        IReadOnlyList<CutsceneCommand> commands)
    {
        _guard = guard; _context = context; _precise = guard.Position;
        _runner = new(this); _runner.Start(commands);
    }

    public override bool DialogueOpen => _context.DialogueOpen;
    public override bool ScriptExecutionBlocked => DialogueOpen ||
        _context.Player.IsDying;
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Guard";
    public override bool MemoryEquals(string binding, int value) => binding switch
    {
        "GuardX" => OracleObjectPosition.HighByte(_guard.Position.X) == value,
        "GuardY" => OracleObjectPosition.HighByte(_guard.Position.Y) == value,
        "LinkY" => OracleObjectPosition.HighByte(_context.Player.Position.Y) == value,
        _ => throw UnsupportedCommand($"read palace guard byte '{binding}'")
    };
    public override void MoveActorAtSpeed(string actor, int speed, int angle) =>
        _guard.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(ref _precise, speed, angle));
    public override void ScriptEnded() { }
    internal void Update() => _runner.AdvanceFrame();
}
