using Godot;
using System;

namespace Oracles;

internal sealed class SharedNpcScriptHost : NpcInteractionCommandHost
{
    // constants/common/rupeeValues.s; scriptHelper.s:oldMan_rupeeValues[0].
    private const int RupeeValue200 = 0x0d;
    private readonly SharedScriptNpcRoomEntity _slot;
    private readonly SharedNpcDatabase _data;
    private readonly InventoryState _inventory;
    private readonly Func<bool> _rupeeDisplayUpdated;
    private readonly int _adlarState;
    internal bool AnimationDisabled { get; private set; }
    internal SharedNpcScriptHost(SharedScriptNpcRoomEntity slot, SharedNpcDatabase data,
        RoomSession rooms, RoomEntityManager entities, DialogueBox dialogue,
        InventoryState inventory, Func<bool> rupeeDisplayUpdated)
        : base("Npc", rooms, entities, dialogue, data.Script(slot.Npc.Record))
    {
        _slot = slot; _data = data; _inventory = inventory; _rupeeDisplayUpdated = rupeeDisplayUpdated;
        var save = rooms.SaveData;
        _adlarState = save.HasGlobalFlag(AgesGlobalFlag.FinishedGame) ? 4 :
            save.HasRoomFlag(4, 0xfc, 0x80) ? 3 : save.HasGlobalFlag(AgesGlobalFlag.SavedNayru) ? 2 :
            save.HasRoomFlag(slot.Npc.Record.Group, slot.Npc.Record.Room, 0x40) ? 1 : 0;
        BindObject(slot.Npc);
    }
    internal void Advance(Player player) => AdvanceObject(player);
    protected override bool MatchesAndPrepare(NpcCharacter npc) => ReferenceEquals(npc, _slot.Npc);
    public override bool TryInteract(NpcInteractionTarget target, Player player) =>
        MatchesAndPrepare(target.Npc) && QueueObjectInteraction(target, player);
    public override bool ScriptExecutionBlocked => base.ScriptExecutionBlocked ||
        Entities.ScreenTransitionActive || Entities.InitializedObjectsDisabledSource();
    public override void ShowText(int textId, string message) => ShowDialogue(message, false);
    public override void ShowLoadedText() => ShowDialogue(ScriptActor.Message, false);
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) =>
        RequireActor(actor).SetScriptAnimation(encodedAnimation);
    public override void WriteObjectByte(string actor, int address, int value)
    {
        _ = RequireActor(actor);
        if (address != 0x79 || value is not (0 or 1)) throw UnsupportedCommand($"write ${address:x2}=${value:x2}");
        AnimationDisabled = value != 0;
    }
    public override int ReadMemory(string binding) => binding == "AdlarState" ? _adlarState :
        throw UnsupportedCommand($"read {binding}");
    public override bool RoomFlagSet(int flag) => flag == 0x40 ?
        Rooms.SaveData.HasRoomFlag(Rooms.ActiveGroup, Rooms.CurrentRoom.Id, 0x40) :
        throw UnsupportedCommand($"read room flag ${flag:x2}");
    public override void OrRoomFlag(int flag)
    {
        if (flag != 0x40) throw UnsupportedCommand($"write room flag ${flag:x2}");
        Rooms.SaveData.SetRoomFlag(Rooms.ActiveGroup, Rooms.CurrentRoom.Id, 0x40);
    }
    public override bool GateOpen(string gate) => gate == "RupeeDisplayUpdated" ? _rupeeDisplayUpdated() :
        throw UnsupportedCommand($"read gate {gate}");
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "turnToFaceLink":
                Vector2 delta = ScriptPlayer.Position - ScriptActor.Position;
                int direction = Math.Abs(delta.X) >= Math.Abs(delta.Y) ? delta.X >= 0 ? 1 : 3 : delta.Y >= 0 ? 2 : 0;
                ScriptActor.SetScriptAnimation(_data.Animation(ScriptActor.Record.Id, direction));
                break;
            case "monkey_turnToFaceLink":
                ScriptActor.SetScriptAnimation(_data.Animation(0x39, ScriptPlayer.Position.Y >= ScriptActor.Position.Y ? 6 : 5));
                break;
            case "monkey_setAnimationFromVar3a": ScriptActor.SetScriptAnimation(_data.Animation(0x39, 5)); break;
            case "boy_orCfde04":
                const int audienceTalkSignal = AgesWramAddress.wTmpcfc0 + 0x1e;
                Entities.RuntimeState.SetWramByte(audienceTalkSignal, (byte)(Entities.RuntimeState.ReadWramByte(audienceTalkSignal) | 4)); break;
            case "oldMan_giveRupees": _inventory.GiveTreasure(TreasureId.Rupees, RupeeValue200); break;
            default: throw UnsupportedCommand(handler);
        }
    }
}
