using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class OldManRupeesScriptHost : NpcInteractionCommandHost
{
    private readonly InventoryState _inventory;
    private readonly Func<bool> _rupeeDisplayUpdated;
    private int _var3f;

    internal OldManRupeesScriptHost(RoomSession rooms, RoomEntityManager entities,
        DialogueBox dialogue, IReadOnlyList<CutsceneCommand> commands,
        InventoryState inventory, Func<bool> rupeeDisplayUpdated)
        : base("OldMan", rooms, entities, dialogue, commands)
    {
        _inventory = inventory;
        _rupeeDisplayUpdated = rupeeDisplayUpdated;
    }

    protected override bool MatchesAndPrepare(NpcCharacter npc) =>
        npc.Record is { Id: InteractionId.OldManWithRupees, SubId: 0x01 };

    // bank0.updateInteractions suppresses initialized ordinary actors during
    // scrolling and when wDisabledObjects bit 1 is set, as well as text.
    public override bool ScriptExecutionBlocked => base.ScriptExecutionBlocked ||
        Entities.ScreenTransitionActive || Entities.InitializedObjectsDisabledSource();

    public override bool RoomFlagSet(int flag) => flag == 0x40
        ? Rooms.SaveData.HasRoomFlag(Rooms.ActiveGroup, Rooms.CurrentRoom.Id, OracleSaveData.RoomFlag40)
        : throw UnsupportedCommand($"read old man room flag ${flag:x2}");

    public override void OrRoomFlag(int flag)
    {
        if (flag != 0x40) throw UnsupportedCommand($"write old man room flag ${flag:x2}");
        Rooms.SaveData.SetRoomFlag(Rooms.ActiveGroup, Rooms.CurrentRoom.Id, OracleSaveData.RoomFlag40);
    }

    public override void ShowText(int textId, string message)
    {
        if (textId is < 0x3315 or > 0x3317 || string.IsNullOrEmpty(message))
            throw UnsupportedCommand($"show old man TX_{textId:x4}");
        ShowDialogue(message, choice: false);
    }

    public override void RunNativeHandler(string handler)
    {
        if (handler != "oldMan_takeRupees100") throw UnsupportedCommand(handler);
        // scriptHelper.s checks for any money, not enough money. The BCD
        // subtraction saturates at zero; var3f still records a successful charge.
        _var3f = _inventory.Rupees == 0 ? 0 : 1;
        if (_var3f != 0) _inventory.AddRupees(-100);
    }

    public override bool MemoryEquals(string binding, int value) =>
        binding == "OldManVar3f" && value == 0
            ? _var3f == 0
            : throw UnsupportedCommand($"compare {binding}=${value:x2}");

    public override bool GateOpen(string gate) => gate == "RupeeDisplayUpdated"
        ? _rupeeDisplayUpdated()
        : throw UnsupportedCommand($"read {gate}");

    protected override void ResetHostState() => _var3f = 0;
}
