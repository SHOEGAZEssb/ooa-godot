using System;

namespace oracleofages;

/// <summary>
/// INTERAC_MASK_SALESMAN $5c:$00 and maskSalesmanScript in room $2:$e6.
/// </summary>
internal sealed class MaskSalesmanEvent :
    InteractiveCutsceneCommandHost, IRoomEntryEvent, ICutsceneCommandHost,
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

    public MaskSalesmanEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => InputLeaseHeld;
    protected override RoomEventContext InputContext => _context;
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
        ReleaseInputControl();
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
        if (!_runner.Active || !_buttonSensitive || InputLeaseHeld ||
            !ReferenceEquals(npc, _salesman))
        {
            return false;
        }
        _buttonPressed = true;
        return true;
    }

    public void Cancel()
    {
        ReleaseInputControl();
        if (_salesman is not null)
        {
            _salesman.SetScriptButtonSensitive(false);
            _salesman.SetAnimationRate(1.0f);
        }
        _salesman = null;
        _buttonSensitive = false;
        _buttonPressed = false;
        _runner.Clear();
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == ActorName;

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

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireSalesman(actor).Visible = visible;

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
