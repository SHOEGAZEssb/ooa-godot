using System;
using System.Globalization;

namespace oracleofages;

internal sealed class BlackTowerPatrolScriptHost : NpcInteractionCommandHost
{
    private readonly BlackTowerPatrollingWorkerRoomEntity _slot;
    private readonly BlackTowerWorkerDatabase _data;
    private bool _ownsInput;

    internal BlackTowerPatrolScriptHost(BlackTowerPatrollingWorkerRoomEntity slot,
        BlackTowerWorkerDatabase data, RoomSession rooms, RoomEntityManager entities, DialogueBox dialogue)
        : base("Patrol", rooms, entities, dialogue, data.PatrolCommands)
    {
        _slot = slot; _data = data;
        BindObject(slot.Npc);
    }

    internal bool ButtonPending => ObjectButtonPending;
    internal void Advance(Player player) => AdvanceObject(player);
    protected override bool MatchesAndPrepare(NpcCharacter npc) => ReferenceEquals(npc, _slot.Npc);
    public override bool TryInteract(NpcInteractionTarget target, Player player) =>
        !ButtonPending && MatchesAndPrepare(target.Npc) && QueueObjectInteraction(target, player);
    public override void SetInputEnabled(bool enabled)
    {
        NativeScriptInputControl.SetEnabled(Entities.RuntimeState, ScriptPlayer, enabled);
        _ownsInput = !enabled;
        if (enabled) base.SetInputEnabled(true);
    }
    protected override void ResetHostState()
    {
        if (_ownsInput) SetInputEnabled(true);
    }
    public override int ReadMemory(string binding) => binding switch
    {
        "PatrolPattern" => ScriptActor.Record.Var03,
        "PatrolPressedA" => ButtonPending ? 1 : 0,
        "PatrolZeroFlag" => Entities.RuntimeState.ReadWramByte(0xcddb) & 0x80,
        _ => throw UnsupportedCommand($"read '{binding}'")
    };
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override void ShowLoadedText() => ShowDialogue(ScriptActor.Message, choice: false);
    public override void WriteObjectByte(string actor, int address, int value)
    {
        _ = RequireActor(actor);
        if (address != 0x31 || value != 0) throw UnsupportedCommand($"write ${address:x2}=${value:x2}");
        ClearObjectButton();
    }
    public override void RunNativeHandler(string handler)
    {
        if (handler.StartsWith("hardhatWorker_setPatrolDirection:", StringComparison.Ordinal))
        {
            int direction = NativeByteParameter(handler);
            if (direction > 3) throw UnsupportedCommand($"patrol direction ${direction:x2}");
            _slot.SetPatrolDirection(direction);
            return;
        }
        if (handler.StartsWith("hardhatWorker_setPatrolCounter:", StringComparison.Ordinal))
        {
            _slot.SetPatrolCounter(NativeByteParameter(handler));
            return;
        }
        switch (handler)
        {
            case "soldierSetSpeed80AndVar3fTo01": _slot.StartPatrolling(); break;
            case "hardhatWorker_chooseTextForPatroller":
                int pattern = ScriptActor.Record.Var03;
                int text = _data.HardhatText(pattern == 4 ? 4 : Entities.Random.Next().Value & 3);
                ScriptActor.SetDialogue(text, _data.Text(text), canFace: true);
                break;
            case "hardhatWorker_decPatrolCounter": _slot.DecrementPatrolCounter(Entities.RuntimeState); break;
            case "objectApplySpeed": _slot.Move(Entities.RuntimeState); break;
            case "turnToFaceLink":
                ScriptActor.SetFacingDirection(RoomEventResources.DirectionToward(ScriptActor.Position, ScriptPlayer.Position));
                break;
            case "hardhatWorker_updatePatrolAnimation": _slot.RestorePatrolAnimation(); break;
            default: throw UnsupportedCommand($"run native handler '{handler}'");
        }
    }
    private int NativeByteParameter(string handler) =>
        byte.TryParse(handler.AsSpan(handler.IndexOf(':') + 1), NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out byte value) ? value :
            throw UnsupportedCommand($"native parameter '{handler}'");
    public override void ScriptEnded() => throw UnsupportedCommand("end the infinite hardhat patrol");
}
