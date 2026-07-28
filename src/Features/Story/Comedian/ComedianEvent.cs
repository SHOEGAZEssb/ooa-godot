using System;

namespace oracleofages;

/// <summary>
/// INTERAC_COMEDIAN $65:$00 and comedianScript in present room $0:$56.
/// </summary>
internal sealed class ComedianEvent :
    InteractiveInfiniteScriptHost<ComedianCharacter>,
    IRoomEntryEvent, ICutsceneCommandHost
{
    private const string ActorName = "Comedian";
    private readonly ComedianEventDatabase _database = new();
    private readonly ComedianEventRecord _record;
    private int _progress;

    public ComedianEvent(RoomEventContext context) :
        base(context, ActorName)
    {
        _record = _database.Record;
    }

    internal int Progress => _progress;
    internal ComedianEventDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        _ = room;
        NpcCharacter actor = Context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_COMEDIAN");
        ComedianCharacter comedian = actor as ComedianCharacter ??
            throw new InvalidOperationException(
                "Room 0:56 instantiated INTERAC_COMEDIAN without its native actor.");
        _progress = 0;
        StartInfiniteScript(
            comedian,
            _database.Commands,
            _record.InitialScriptUpdates);

        // interactionCode65 state 0 calls interactionRunScript twice before
        // its single interactionAnimateAsNpc call.
        comedian.AdvanceInitialUpdate(Context.Player);
    }

    public override void UpdateFrame()
    {
        AdvanceInfiniteScript();
        ScriptActor?.UpdateComedian(Context.Player);
    }

    protected override void ResetEventState()
    {
        _progress = 0;
    }

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
        return Context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    bool ICutsceneCommandHost.TradeItemEquals(int value)
    {
        if (value != _record.RequiredTradeItem)
        {
            throw new InvalidOperationException(
                $"comedianScript cannot compare trade item ${value:x2}.");
        }
        return Context.Inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
            Context.Inventory.TradeItem == value;
    }

    bool ICutsceneCommandHost.TextOptionEquals(int value)
    {
        if (!Context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "comedianScript text-option branch has no completed choice result.");
        }
        return choice == value;
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId is < 0x0b2c or > 0x0b32)
        {
            throw new InvalidOperationException(
                $"comedianScript requested unknown TX_{textId:x4}.");
        }
        if (textId == 0x0b2f)
            Context.ShowChoiceDialogue(message);
        else
            Context.ShowDialogue(message);
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
        RequireScriptActor(actor).SetScriptAnimation(encodedAnimation);
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
        RequireScriptActor(actor).SetCollisionRadii(radiusY, radiusX);
    }

    void ICutsceneCommandHost.GiveItem(int treasureId, int parameter)
    {
        if (treasureId != _record.RewardTreasure ||
            parameter != _record.RewardParameter)
        {
            throw new InvalidOperationException(
                $"comedianScript requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        Context.GrantScriptTreasure(
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
                _progress = CalculateProgress(Context.Inventory.Essences);
                break;
            case "comedian_disableMustache":
                RequireScriptActor(ActorName).SetMustacheEnabled(false);
                break;
            case "comedian_enableMustache":
                RequireScriptActor(ActorName).SetMustacheEnabled(true);
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
