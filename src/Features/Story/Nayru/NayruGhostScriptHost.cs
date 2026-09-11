using System.Collections.Generic;
using Godot;

namespace oracleofages;

/// <summary>The $3e:$00 substate-8 script, installed after Nayru signals $cfd0=$17.</summary>
internal sealed class NayruGhostScriptHost : RoomCutsceneCommandHost
{
    private readonly CutsceneCommandRunner _runner;
    private readonly IReadOnlyList<CutsceneCommand> _commands;
    private NpcCharacter? _actor;
    private OracleObjectPosition _position;

    internal NayruGhostScriptHost(RoomEventContext context, IReadOnlyList<CutsceneCommand> commands)
    {
        Context = context;
        _commands = commands;
        _runner = new CutsceneCommandRunner(this);
    }

    public override RoomEventContext Context { get; }
    internal CutsceneCommandRunner Runner => _runner;
    internal Vector2 PrecisePosition => _position.PrecisePosition;
    internal bool Active => _runner.Active;

    internal void Start(NpcCharacter actor)
    {
        _actor = actor;
        _position = OracleObjectPosition.FromPixels(actor.Position);
        actor.SetActive(true);
        actor.Visible = true;
        _runner.Start(_commands);
        // substate7 falls through substate8 in the same interaction slot.
        _runner.AdvanceFrame();
    }

    internal void AdvanceFrame() => _runner.AdvanceFrame();
    internal void Clear()
    {
        _runner.Clear();
        _actor = null;
    }

    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "GhostVeran";

    public override void SetActorCoordinates(string actor, int y, int x)
    {
        RequireActor(actor);
        _position = new OracleObjectPosition(
            (ushort)((y << 8) | (_position.YFixed & 0xff)),
            (ushort)((x << 8) | (_position.XFixed & 0xff)));
        _actor!.Position = _position.PixelPosition;
    }

    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    {
        RequireActor(actor);
        _position = OracleObjectMovement.Shared.ApplySpeed(_position, speed, angle);
        _actor!.Position = _position.PixelPosition;
    }

    public override bool MemoryEquals(string binding, int value) =>
        binding == "NayruPortalSignal"
            ? Context.Entities.RuntimeState.ReadWramByte(0xcfd2) == value
            : throw UnsupportedCommand($"read '{binding}'");

    public override void WriteMemory(string binding, int value)
    {
        if (binding != "NayruPhase")
            throw UnsupportedCommand($"write '{binding}'");
        Context.Entities.RuntimeState.SetWramByte(0xcfd0, (byte)value);
    }

    public override void ScriptEnded() => _actor!.SetActive(false);

    private void RequireActor(string actor)
    {
        if (actor != "GhostVeran" || _actor is null)
            throw UnsupportedCommand($"access actor '{actor}'");
    }
}
