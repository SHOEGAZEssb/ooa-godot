using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace oracleofages;

/// <summary>INTERAC_PLEN $cc:$00 and scriptHelp.plenSubid0Script.</summary>
internal sealed class PlenEvent : InteractiveCutsceneCommandHost, IRoomEntryEvent,
    ICutsceneCommandHost, IUpdatesDuringDialogueRoomEvent
{
    public override RoomEventContext Context { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; } =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/plen_commands.tsv");
    private readonly CutsceneCommandRunner _runner;
    private NpcCharacter? _npc;
    private bool _buttonSensitive;
    private bool _buttonPressed;
    private bool _secretPending;
    private int _secretResult;
    private int _generation;
    internal Func<int, Action<bool>, bool>? OpenSecretMenu { get; set; }
    public bool HasState => _npc is not null;
    public bool BlocksGameplay => InputControlHeld;
    public bool FreezesNonInteractionObjects => InputControlHeld;
    public bool MenusDisabled => BlocksGameplay;
    public bool ScreenTransitionsDisabled => BlocksGameplay;
    public override bool DialogueOpen => Context.DialogueOpen || _secretPending;

    public PlenEvent(RoomEventContext context)
    {
        Context = context;
        _runner = new(this);
    }

    public bool Matches(int group, OracleRoomData room) =>
        Context.Entities.EntityAdapters<PlenRoomEntity>().Any();

    public void Start(OracleRoomData room)
    {
        Cancel();
        _npc = Context.Entities.EntityAdapters<PlenRoomEntity>().Single().Npc;
        _runner.Start(Commands);
    }

    public void UpdateFrame()
    {
        if (_npc is null) return;
        _runner.AdvanceFrame();
        // plen.s sets the always-update bit; native animation continues while
        // interactionRunScript is blocked by text, including his reward text.
        _npc.AnimateAsNpcOneUpdate(Context.Player);
    }

    public void UpdateDuringDialogueFrame() => UpdateFrame();

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (!ReferenceEquals(_npc, npc) || !npc.Active || !_buttonSensitive ||
            BlocksGameplay || DialogueOpen) return false;
        _buttonPressed = true;
        return true;
    }

    public void Cancel()
    {
        _generation++;
        _runner.Clear();
        _npc?.SetScriptButtonSensitive(false);
        _npc = null;
        _buttonSensitive = _buttonPressed = _secretPending = false;
        _secretResult = 0;
        ReleaseInputControl();
    }

    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Plen";
    public override void InitializeActorCollisionRadii(string actor) => _npc!.InitializeCollisionRadii();
    public override void SetActorButtonSensitive(string actor)
    {
        _buttonSensitive = true;
        _npc!.SetScriptButtonSensitive(true);
    }
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool pressed = _buttonPressed;
        _buttonPressed = false;
        if (pressed) Context.Player.ApplyObjectInteractionGrace();
        return pressed;
    }
    public override bool MemoryEquals(string binding, int value)
    {
        if (binding.StartsWith("Global:", StringComparison.Ordinal) &&
            int.TryParse(binding.AsSpan(7), out int flag))
            return (Context.Rooms.SaveData.HasGlobalFlag(flag) ? 1 : 0) == value;
        if (binding == "wTextInputResult") return _secretResult == value;
        throw UnsupportedCommand($"read Plen memory '{binding}'");
    }
    public override bool TextOptionEquals(int value)
    {
        if (!Context.TryTakeDialogueChoice(out int choice))
            throw UnsupportedCommand("read absent Plen choice");
        return choice == value;
    }
    public override void ShowText(int textId, string message)
    {
        if (message.Contains("\\opt(", StringComparison.Ordinal)) Context.ShowChoiceDialogue(message);
        else Context.ShowDialogue(message);
    }
    public override void RunNativeHandler(string handler)
    {
        if (handler.StartsWith("LoadText:", StringComparison.Ordinal))
        {
            int text = int.Parse(handler.AsSpan(9), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var command = Commands.OfType<CutsceneShowTextCommand>().Single(c => c.TextId == text);
            _npc!.SetDialogue(text, command.Message, canFace: false);
            return;
        }
        switch (handler)
        {
            case "AskSecret":
                _secretPending = true;
                int generation = _generation;
                if (OpenSecretMenu is null || !OpenSecretMenu(0x03, valid =>
                    {
                        if (generation != _generation) return;
                        _secretResult = valid ? 0 : 1;
                        _secretPending = false;
                    })) throw UnsupportedCommand("acquire MENU_SECRET for PLEN_SECRET $03");
                return;
            case "GiveSpinRing":
                // giveRingAToLink -> giveRingToLink: no RNG, no retry if the
                // interaction pool is full. The script still sets DONE $71.
                if (!Context.Entities.InteractionSlotAvailable) return;
                Context.Entities.GrantGroundTreasure(new GroundTreasureGrantRequest(
                    Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, 0,
                    (int)Context.Player.Position.Y, (int)Context.Player.Position.X,
                    "TREASURE_OBJECT_RING_00", "scriptHelper.s:plenSubid0Script:giveRingAToLink")
                {
                    SpawnMode = 0,
                    GrabMode = 2,
                    InventoryWrite = GroundTreasureInventoryWrite.UnappraisedRing,
                    InventoryParameter = 0x2f,
                    RoomFlagTiming = GroundTreasureRoomFlagTiming.Never
                }, Context.Player);
                return;
            default: throw UnsupportedCommand($"run Plen helper '{handler}'");
        }
    }
}
