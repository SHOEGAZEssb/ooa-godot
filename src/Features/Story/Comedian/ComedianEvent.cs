using System;

namespace oracleofages;

/// <summary>
/// INTERAC_COMEDIAN $65:$00 and comedianScript in present room $0:$56.
/// </summary>
internal sealed class ComedianEvent :
    InteractiveCutsceneCommandHost, IRoomEntryEvent, ICutsceneCommandHost
{
    private const string ActorName = "Comedian";
    private readonly RoomEventContext _context;
    private readonly ComedianEventDatabase _database = new();
    private readonly ComedianEventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private ComedianCharacter? _comedian;
    private int _progress;
    private bool _buttonSensitive;
    private bool _buttonPressed;

    public ComedianEvent(RoomEventContext context)
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
    internal int Progress => _progress;
    internal bool ButtonSensitive => _buttonSensitive;
    internal ComedianEventDatabase Database => _database;

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
            "INTERAC_COMEDIAN");
        _comedian = actor as ComedianCharacter ??
            throw new InvalidOperationException(
                "Room 0:56 instantiated INTERAC_COMEDIAN without its native actor.");
        _buttonSensitive = false;
        _buttonPressed = false;
        ReleaseInputControl();
        _progress = 0;
        _runner.Start(_database.Commands);

        // interactionCode65 state 0 calls interactionRunScript twice before
        // its single interactionAnimateAsNpc call.
        for (int update = 0; update < _record.InitialScriptUpdates; update++)
            _runner.AdvanceFrame();
        _comedian.AdvanceInitialUpdate(_context.Player);
    }

    public void UpdateFrame()
    {
        _runner.AdvanceFrame();
        _comedian?.UpdateComedian(_context.Player);
    }

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (!_runner.Active || !_buttonSensitive || InputLeaseHeld ||
            !ReferenceEquals(npc, _comedian))
        {
            return false;
        }
        _buttonPressed = true;
        return true;
    }

    public void Cancel()
    {
        ReleaseInputControl();
        if (_comedian is not null)
        {
            _comedian.SetScriptButtonSensitive(false);
            _comedian.SetAnimationRate(1.0f);
        }
        _comedian = null;
        _progress = 0;
        _buttonSensitive = false;
        _buttonPressed = false;
        _runner.Clear();
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == ActorName;

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        ReadMemory(binding) == value;

    int ICutsceneCommandHost.ReadMemory(string binding) => ReadMemory(binding);

    bool ICutsceneCommandHost.RoomFlagSet(int flag)
    {
        if (flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"comedianScript cannot read room flag ${flag:x2}.");
        }
        return _context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    bool ICutsceneCommandHost.TradeItemEquals(int value)
    {
        if (value != _record.RequiredTradeItem)
        {
            throw new InvalidOperationException(
                $"comedianScript cannot compare trade item ${value:x2}.");
        }
        return _context.Inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
            _context.Inventory.TradeItem == value;
    }

    bool ICutsceneCommandHost.TextOptionEquals(int value)
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "comedianScript text-option branch has no completed choice result.");
        }
        return choice == value;
    }

    bool ICutsceneCommandHost.TryConsumeActorButton(CutsceneActorId actor)
    {
        _ = RequireComedian(actor.Value);
        if (!_buttonPressed)
            return false;
        _buttonPressed = false;
        return true;
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId is < 0x0b2c or > 0x0b32)
        {
            throw new InvalidOperationException(
                $"comedianScript requested unknown TX_{textId:x4}.");
        }
        if (textId == 0x0b2f)
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
                $"Comedian animation ${animation:x2} payload diverged from metadata.");
        }
        RequireComedian(actor).SetScriptAnimation(encodedAnimation);
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
                $"comedianScript initialized unexpected collision radii " +
                $"${radiusY:x2}/${radiusX:x2}.");
        }
        RequireComedian(actor).SetCollisionRadii(radiusY, radiusX);
    }

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor)
    {
        RequireComedian(actor).SetScriptButtonSensitive(true);
        _buttonSensitive = true;
    }

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireComedian(actor).Visible = visible;

    void ICutsceneCommandHost.GiveItem(int treasureId, int parameter)
    {
        if (treasureId != _record.RewardTreasure ||
            parameter != _record.RewardParameter)
        {
            throw new InvalidOperationException(
                $"comedianScript requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        _context.GrantScriptTreasure(
            _record.Group,
            _record.Room,
            treasureId,
            parameter,
            _record.RewardObject,
            "scriptHelper.s:comedianScript giveitem TREASURE_TRADEITEM,$07");
    }

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "comedian_checkGameProgress":
                _progress = CalculateProgress(_context.Inventory.Essences);
                break;
            case "comedian_disableMustache":
                RequireComedian(ActorName).SetMustacheEnabled(false);
                break;
            case "comedian_enableMustache":
                RequireComedian(ActorName).SetMustacheEnabled(true);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown comedian native handler '{handler}'.");
        }
    }

    private int ReadMemory(string binding)
    {
        if (binding != _record.ProgressBinding)
        {
            throw new InvalidOperationException(
                $"comedianScript cannot read '{binding}'.");
        }
        return _progress;
    }

    private ComedianCharacter RequireComedian(string actor)
    {
        if (actor != ActorName || _comedian is null)
        {
            throw new InvalidOperationException(
                $"Unknown comedian command actor '{actor}'.");
        }
        return _comedian;
    }

    private static int CalculateProgress(int essences)
    {
        if (essences == 0)
            return 0;

        int highestSetBit = 0;
        while ((essences >>= 1) != 0)
            highestSetBit++;
        return highestSetBit >= 3 ? 2 : highestSetBit;
    }
}
