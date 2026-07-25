using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_COMEDIAN $65:$00 and comedianScript in present room $0:$56.
/// </summary>
internal sealed class ComedianEvent : IRoomEntryEvent, ICutsceneCommandHost
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
    private bool _inputDisabled;

    public ComedianEvent(RoomEventContext context)
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
        _inputDisabled = false;
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
        if (!_runner.Active || !_buttonSensitive || _inputDisabled ||
            !ReferenceEquals(npc, _comedian))
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
        if (_comedian is not null)
        {
            _comedian.SetScriptButtonSensitive(false);
            _comedian.SetAnimationRate(1.0f);
        }
        _comedian = null;
        _progress = 0;
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
            $"comedianScript does not set menu enabled={enabled} independently.");

    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw new InvalidOperationException(
            $"comedianScript does not write wDisabledObjects=${value:x2}.");

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw new InvalidOperationException(
            $"comedianScript has no gate named '{gate}'.");

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

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation) =>
        throw new InvalidOperationException(
            $"Comedian actor '{actor}' cannot use movement animation ${angle:x2}.");

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

    void ICutsceneCommandHost.MoveActorAtSpeed(
        string actor,
        int speed,
        int angle) =>
        throw new InvalidOperationException(
            $"Comedian actor '{actor}' cannot move at ${speed:x2}/${angle:x2}.");

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw new InvalidOperationException(
            $"Comedian actor '{actor}' cannot set Z to ${zFixed:x4}.");

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequireComedian(actor).Visible = visible;

    void ICutsceneCommandHost.WriteMemory(string binding, int value) =>
        throw new InvalidOperationException(
            $"comedianScript cannot write '{binding}'=${value:x2}.");

    void ICutsceneCommandHost.GiveItem(int treasureId, int parameter)
    {
        if (treasureId != _record.RewardTreasure ||
            parameter != _record.RewardParameter)
        {
            throw new InvalidOperationException(
                $"comedianScript requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        TreasureObjectRecord treasure =
            _context.Treasures.GetObject(_record.RewardObject);
        if (treasure.TreasureId != treasureId ||
            treasure.SubId != parameter ||
            treasure.Parameter != parameter)
        {
            throw new InvalidOperationException(
                $"{_record.RewardObject} no longer matches comedianScript's reward.");
        }
        TreasureObjectVisualRecord visual =
            _context.Treasures.GetObjectVisual(treasure.Graphic);
        Vector2 position = _context.Player.Position;
        var record = new GroundTreasureDatabaseRecord(
            _record.Group,
            _record.Room,
            0,
            Mathf.FloorToInt(position.Y),
            Mathf.FloorToInt(position.X),
            treasure.Name,
            visual.Sprite,
            visual.TileBase,
            visual.Palette,
            visual.Animation,
            treasure.TextId,
            treasure.Message,
            "scriptHelper.s:comedianScript giveitem TREASURE_TRADEITEM,$07",
            SpawnMode: 0,
            GrabMode: 2);
        _context.Entities.GrantGroundTreasure(record, _context.Player);
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw new InvalidOperationException(
            $"comedianScript does not directly OR room flag ${flag:x2}.");

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

    void ICutsceneCommandHost.ScriptEnded() =>
        throw new InvalidOperationException(
            "comedianScript must remain in its NPC loop.");

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
