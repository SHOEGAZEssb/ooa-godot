using System;
using System.Linq;

namespace oracleofages;

internal sealed class SymmetryScriptHost : CutsceneCommandHost, ICutsceneCommandHost
{
    private readonly SymmetryEvent _owner;
    private readonly CutsceneCommandRunner _runner;
    private bool _listensForNut;
    private bool _secretPending;
    private int _secretResult;
    private string _secret = string.Empty;
    private int _generation;
    public RoomEventContext Context => _owner.Context;
    internal NpcCharacter Npc { get; }
    internal bool ButtonSensitive { get; private set; }
    internal bool ButtonPressed { get; set; }
    public override bool DialogueOpen => Context.DialogueOpen || _secretPending;

    public SymmetryScriptHost(SymmetryEvent owner, NpcCharacter npc)
    {
        _owner = owner;
        Npc = npc;
        _runner = new(this);
        _runner.Start(owner.Database.Commands, owner.Database.Entry(npc.Record.SubId));
    }
    internal void AdvanceFrame()
    {
        // Native state $02 observes the shared Tuni Nut slot's signal before
        // interactionRunScript (which itself remains frozen by active text).
        if (_listensForNut && (Context.Entities.RuntimeState.ReadWramByte(0xcfc0) & 1) != 0)
        {
            _listensForNut = false;
            _runner.Start(_owner.Database.Commands, _owner.Database.Entry(0x0d));
        }
        _runner.AdvanceFrame();
        Npc.AnimateAsNpcOneUpdate(Context.Player);
    }
    internal void Cancel()
    {
        _generation++;
        _runner.Clear();
        Npc.SetScriptButtonSensitive(false);
    }
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Symmetry";
    public override void InitializeActorCollisionRadii(string actor) =>
        Npc.InitializeCollisionRadii();

    public override void SetActorCollisionRadii(string actor, int radiusY, int radiusX) => Npc.SetCollisionRadii(radiusY, radiusX);
    public override void SetActorButtonSensitive(string actor)
    {
        ButtonSensitive = true;
        Npc.SetScriptButtonSensitive(true);
    }
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool pressed = ButtonPressed;
        ButtonPressed = false;
        if (pressed) Context.Player.ApplyObjectInteractionGrace();
        return pressed;
    }
    public override void SetInputEnabled(bool enabled) => _owner.SetInputEnabled(enabled);
    public override void SetDisabledObjects(int value)
    {
        if (value != 0x91) throw UnsupportedCommand($"set disabled objects ${value:x2}");
        _owner.SetInputEnabled(false);
    }
    public override bool RoomFlagSet(int flag) => (RoomFlags & flag) != 0;
    private byte RoomFlags => Context.Rooms.SaveData.GetRoomFlags(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id);
    public override void OrRoomFlag(int flag) => Context.Rooms.SaveData.SetRoomFlag(
        Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, checked((byte)flag));
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override int ReadMemory(string binding)
    {
        if (binding.StartsWith("Global:", StringComparison.Ordinal) && int.TryParse(binding.AsSpan(7), out int flag))
            return Context.Rooms.SaveData.HasGlobalFlag(flag) ? 1 : 0;
        return binding switch
        {
            "Treasure:TREASURE_TUNI_NUT" => Context.Inventory.HasTreasure(TreasureDatabase.TreasureTuniNut) ? 1 : 0,
            "Treasure:TREASURE_RING_BOX" => Context.Inventory.HasTreasure(TreasureDatabase.TreasureRingBox) ? 1 : 0,
            "wTmpcfc0.genericCutscene.cfc1" => Context.Entities.RuntimeState.ReadWramByte(0xcfc1),
            "wTextInputResult" => _secretResult,
            "wTextNumberSubstitution" => Context.Entities.RuntimeState.ReadWramByte(0xcba8),
            _ => throw UnsupportedCommand($"read symmetry memory '{binding}'")
        };
    }
    public override bool TextOptionEquals(int value)
    {
        if (!Context.TryTakeDialogueChoice(out int choice)) throw UnsupportedCommand("read absent symmetry choice");
        return choice == value;
    }
    public override void ShowText(int textId, string message)
    {
        message = message.Replace("\\secret1", _secret, StringComparison.Ordinal);
        if (message.Contains("\\opt(", StringComparison.Ordinal)) Context.ShowChoiceDialogue(message);
        else Context.ShowDialogue(message);
    }
    public override void GiveItem(int treasureId, int parameter)
    {
        string name = (treasureId, parameter) switch
        {
            (TreasureDatabase.TreasureTuniNut, 0) => "TREASURE_OBJECT_TUNI_NUT_00",
            (TreasureDatabase.TreasureRingBox, 1) => "TREASURE_OBJECT_RING_BOX_01",
            (TreasureDatabase.TreasureRingBox, 2) => "TREASURE_OBJECT_RING_BOX_02",
            _ => throw UnsupportedCommand($"give symmetry treasure ${treasureId:x2}:${parameter:x2}")
        };
        var item = Context.Treasures.GetObject(name);
        // treasure.s:@setLinkAnimationAndDeleteIfTextClosed owns the held-item
        // completion. GrantScriptTreasure registers that with InteractionController.
        Context.GrantScriptTreasure(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id,
            treasureId, parameter, name, "scriptHelper.s:symmetryNpc", objectParameter: item.Parameter);
    }
    public override void RunNativeHandler(string handler)
    {
        if (handler.StartsWith("LoadText:", StringComparison.Ordinal) &&
            int.TryParse(handler.AsSpan(9), System.Globalization.NumberStyles.HexNumber, null, out int text))
        {
            var command = _owner.Database.Commands.OfType<CutsceneShowTextCommand>().First(c => c.TextId == text);
            Npc.SetDialogue(text, command.Message, canFace: true);
            return;
        }
        switch (handler)
        {
            case "ListenForNut": _listensForNut = true; break;
            case "symmetryNpc_setRoomFlagIfTalkedToRightSister": OrRoomFlag(Npc.Record.SubId - 8); break;
            case "symmetryNpc_getTuniNutState":
            case "symmetryNpc_getTuniNutStateForSister":
                int state = Context.Inventory.HasTreasure(TreasureDatabase.TreasureTuniNut)
                    ? (Context.Inventory.TuniNutState == 0 ? 1 : 2) : 0;
                if (state == 0 && handler.EndsWith("ForSister", StringComparison.Ordinal))
                    state = (RoomFlags & 0x0f) == Npc.Record.SubId - 8 ? 0 : 3;
                Context.Entities.RuntimeState.SetWramByte(0xcfc1, (byte)state);
                break;
            case "symmetryNpc_getUpgradeCapacityForText":
                int capacity = !Context.Inventory.HasTreasure(TreasureDatabase.TreasureRingBox) || Context.Inventory.RingBoxLevel == 1 ? 3 : 5;
                Context.Entities.RuntimeState.SetWramByte(0xcba8, (byte)capacity);
                Context.Entities.RuntimeState.SetWramByte(0xcba9, 0);
                break;
            case "AskSecret":
                _secretPending = true;
                int generation = _generation;
                if (_owner.OpenSecretMenu is null || !_owner.OpenSecretMenu(0x09, valid =>
                    {
                        if (generation != _generation) return;
                        _secretResult = valid ? 0 : 1;
                        _secretPending = false;
                    })) throw UnsupportedCommand("acquire MENU_SECRET for SYMMETRY_SECRET $09");
                break;
            case "GenerateSecret": _secret = new LinkedGameNpcDatabase().GenerateSecret(0x19, Context.Rooms.SaveData); break;
            default: throw UnsupportedCommand($"run symmetry helper '{handler}'");
        }
    }
}
