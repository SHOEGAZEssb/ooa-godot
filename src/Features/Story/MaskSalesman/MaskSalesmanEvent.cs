using System;

namespace oracleofages;

/// <summary>
/// INTERAC_MASK_SALESMAN $5c:$00 and maskSalesmanScript in room $2:$e6.
/// </summary>
internal sealed class MaskSalesmanEvent : IRoomEntryEvent, ICutsceneCommandHost,
    IUpdatesDuringDialogueRoomEvent
{
    private const string ActorName = "MaskSalesman";
    private readonly RoomEventContext _context;
    private readonly MaskSalesmanEventDatabase _database = new();
    private readonly MaskSalesmanEventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private MaskSalesmanCharacter? _salesman;
    private bool _buttonSensitive;
    private bool _buttonPressed;
    private bool _inputDisabled;

    public MaskSalesmanEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => _inputDisabled;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int Counter => _runner.Counter;
    internal bool ButtonSensitive => _buttonSensitive;
    internal MaskSalesmanEventDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        _ = room;
        _runner.Clear();
        NpcCharacter actor = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_MASK_SALESMAN");
        _salesman = actor as MaskSalesmanCharacter ??
            throw new InvalidOperationException(
                "Room 2:e6 instantiated INTERAC_MASK_SALESMAN without its native actor.");
        _buttonSensitive = false;
        _buttonPressed = false;
        _inputDisabled = false;
        _runner.Start(_database.Commands);

        // interactionCode5c state 0 falls through to state 1 and runs the
        // newly installed script once before interactionAnimateAsNpc.
        for (int update = 0; update < _record.InitialScriptUpdates; update++)
            _runner.AdvanceFrame();
        _salesman.AdvanceMaskSalesman(_context.Player);
    }

    public void UpdateFrame()
    {
        _runner.AdvanceFrame();
        _salesman?.AdvanceMaskSalesman(_context.Player);
    }

    public void UpdateDuringDialogueFrame() =>
        _salesman?.AdvanceMaskSalesman(_context.Player);

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (!_runner.Active || !_buttonSensitive || _inputDisabled ||
            !ReferenceEquals(npc, _salesman))
        {
            return false;
        }
        _buttonPressed = true;
        return true;
    }

    public void Cancel()
    {
        if (_inputDisabled)
            _context.Player.EndCutsceneControl();
        if (_salesman is not null)
        {
            _salesman.SetScriptButtonSensitive(false);
            _salesman.SetAnimationRate(1.0f);
        }
        _salesman = null;
        _buttonSensitive = false;
        _buttonPressed = false;
        _inputDisabled = false;
        _runner.Clear();
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == ActorName;

    void ICutsceneCommandHost.SetInputEnabled(bool enabled)
    {
        if (enabled)
        {
            if (_inputDisabled)
                _context.Player.EndCutsceneControl();
            _inputDisabled = false;
        }
        else
        {
            if (!_inputDisabled)
                _context.Player.BeginCutsceneControl();
            _inputDisabled = true;
        }
    }

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw new InvalidOperationException(
            $"maskSalesmanScript does not set menu enabled={enabled} independently.");

    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw new InvalidOperationException(
            $"maskSalesmanScript does not write wDisabledObjects=${value:x2}.");

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw new InvalidOperationException(
            $"maskSalesmanScript has no gate named '{gate}'.");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        throw new InvalidOperationException(
            $"maskSalesmanScript cannot compare '{binding}'=${value:x2}.");

    int ICutsceneCommandHost.ReadMemory(string binding) =>
        throw new InvalidOperationException(
            $"maskSalesmanScript cannot read '{binding}'.");

    bool ICutsceneCommandHost.RoomFlagSet(int flag)
    {
        if (flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"maskSalesmanScript cannot read room flag ${flag:x2}.");
        }
        return _context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    bool ICutsceneCommandHost.TradeItemEquals(int value)
    {
        if (value != _record.RequiredTradeItem)
        {
            throw new InvalidOperationException(
                $"maskSalesmanScript cannot compare trade item ${value:x2}.");
        }
        return _context.Inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
            _context.Inventory.TradeItem == value;
    }

    bool ICutsceneCommandHost.TextOptionEquals(int value)
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "maskSalesmanScript text-option branch has no completed choice result.");
        }
        return choice == value;
    }

    bool ICutsceneCommandHost.TryConsumeActorButton(CutsceneActorId actor)
    {
        _ = RequireSalesman(actor.Value);
        if (!_buttonPressed)
            return false;
        _buttonPressed = false;
        return true;
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId != 0x0b45 && textId is < 0x0b0d or > 0x0b15)
        {
            throw new InvalidOperationException(
                $"maskSalesmanScript requested unknown TX_{textId:x4}.");
        }
        if (textId == 0x0b10)
            _context.ShowChoiceDialogue(message);
        else
            _context.ShowDialogue(message);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation)
    {
        if (encodedAnimation != _record.Animation(animation))
        {
            throw new InvalidOperationException(
                $"Mask Salesman animation ${animation:x2} payload diverged from metadata.");
        }
        RequireSalesman(actor).SetScriptAnimation(encodedAnimation);
    }

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        throw new InvalidOperationException(
            $"Mask Salesman actor '{actor}' cannot use movement animation ${angle:x2}.");

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX)
    {
        if (radiusY != _record.CollisionRadiusY ||
            radiusX != _record.CollisionRadiusX)
        {
            throw new InvalidOperationException(
                $"maskSalesmanScript initialized unexpected collision radii " +
                $"${radiusY:x2}/${radiusX:x2}.");
        }
        RequireSalesman(actor).SetCollisionRadii(radiusY, radiusX);
    }

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor)
    {
        RequireSalesman(actor).SetScriptButtonSensitive(true);
        _buttonSensitive = true;
    }

    void ICutsceneCommandHost.MoveActorAtSpeed(
        string actor,
        int speed,
        int angle) =>
        throw new InvalidOperationException(
            $"Mask Salesman actor '{actor}' cannot move at ${speed:x2}/${angle:x2}.");

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw new InvalidOperationException(
            $"Mask Salesman actor '{actor}' cannot set Z to ${zFixed:x4}.");

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireSalesman(actor).Visible = visible;

    void ICutsceneCommandHost.WriteMemory(string binding, int value) =>
        throw new InvalidOperationException(
            $"maskSalesmanScript cannot write '{binding}'=${value:x2}.");

    void ICutsceneCommandHost.GiveItem(int treasureId, int parameter)
    {
        if (treasureId != _record.RewardTreasure ||
            parameter != _record.RewardParameter)
        {
            throw new InvalidOperationException(
                $"maskSalesmanScript requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        _context.GrantScriptTreasure(
            _record.Group,
            _record.Room,
            treasureId,
            parameter,
            _record.RewardObject,
            "scriptHelper.s:maskSalesmanScript giveitem TREASURE_TRADEITEM,$04");
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw new InvalidOperationException(
            $"maskSalesmanScript does not directly OR room flag ${flag:x2}.");

    void ICutsceneCommandHost.RunNativeHandler(string handler) =>
        throw new InvalidOperationException(
            $"maskSalesmanScript has no native handler '{handler}'.");

    void ICutsceneCommandHost.ScriptEnded() =>
        throw new InvalidOperationException(
            "maskSalesmanScript must remain in its NPC loop.");

    private MaskSalesmanCharacter RequireSalesman(string actor)
    {
        if (actor != ActorName || _salesman is null)
        {
            throw new InvalidOperationException(
                $"Unknown Mask Salesman command actor '{actor}'.");
        }
        return _salesman;
    }
}
