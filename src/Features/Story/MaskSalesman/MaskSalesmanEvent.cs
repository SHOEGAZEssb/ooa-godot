using System;

namespace oracleofages;

/// <summary>
/// INTERAC_MASK_SALESMAN $5c:$00 and maskSalesmanScript in room $2:$e6.
/// </summary>
internal sealed class MaskSalesmanEvent :
    InteractiveInfiniteScriptHost<MaskSalesmanCharacter>,
    IRoomEntryEvent, ICutsceneCommandHost,
    IUpdatesDuringDialogueRoomEvent
{
    private const string ActorName = "MaskSalesman";
    private readonly MaskSalesmanEventDatabase _database = new();
    private readonly MaskSalesmanEventRecord _record;

    public MaskSalesmanEvent(RoomEventContext context) :
        base(context, ActorName)
    {
        _record = _database.Record;
    }

    internal MaskSalesmanEventDatabase Database => _database;

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
            "INTERAC_MASK_SALESMAN");
        MaskSalesmanCharacter salesman = actor as MaskSalesmanCharacter ??
            throw new InvalidOperationException(
                "Room 2:e6 instantiated INTERAC_MASK_SALESMAN without its native actor.");
        StartInfiniteScript(
            salesman,
            _database.Commands,
            _record.InitialScriptUpdates);

        // interactionCode5c state 0 falls through to state 1 and runs the
        // newly installed script once before interactionAnimateAsNpc.
        salesman.AdvanceMaskSalesman(Context.Player);
    }

    public override void UpdateFrame()
    {
        AdvanceInfiniteScript();
        ScriptActor?.AdvanceMaskSalesman(Context.Player);
    }

    public void UpdateDuringDialogueFrame() =>
        ScriptActor?.AdvanceMaskSalesman(Context.Player);

    bool ICutsceneCommandHost.RoomFlagSet(int flag)
    {
        if (flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"maskSalesmanScript cannot read room flag ${flag:x2}.");
        }
        return Context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    bool ICutsceneCommandHost.TradeItemEquals(int value)
    {
        if (value != _record.RequiredTradeItem)
        {
            throw new InvalidOperationException(
                $"maskSalesmanScript cannot compare trade item ${value:x2}.");
        }
        return Context.Inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
            Context.Inventory.TradeItem == value;
    }

    bool ICutsceneCommandHost.TextOptionEquals(int value)
    {
        if (!Context.TryTakeDialogueChoice(out int choice))
        {
            throw new InvalidOperationException(
                "maskSalesmanScript text-option branch has no completed choice result.");
        }
        return choice == value;
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId != 0x0b45 && textId is < 0x0b0d or > 0x0b15)
        {
            throw new InvalidOperationException(
                $"maskSalesmanScript requested unknown TX_{textId:x4}.");
        }
        if (textId == 0x0b10)
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
                $"Mask Salesman animation ${animation:x2} payload diverged from metadata.");
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
                $"maskSalesmanScript initialized unexpected collision radii " +
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
                $"maskSalesmanScript requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        Context.GrantScriptTreasure(
            _record.Group,
            _record.Room,
            treasureId,
            parameter,
            _record.RewardObject,
            "scriptHelper.s:maskSalesmanScript giveitem TREASURE_TRADEITEM,$04");
    }

}
