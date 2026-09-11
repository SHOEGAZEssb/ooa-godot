using System;

namespace oracleofages;

/// <summary>
/// INTERAC_DUMBBELL_MAN $51:$00 and dumbbellManScript in room $2:$e8.
/// </summary>
internal sealed class DumbbellManEvent :
    InteractiveInfiniteScriptHost<DumbbellManCharacter>,
    IRoomEntryEvent, ICutsceneCommandHost,
    IUpdatesDuringDialogueRoomEvent
{
    private const string ActorName = "DumbbellMan";
    private readonly DumbbellManEventDatabase _database = new();
    private readonly DumbbellManEventRecord _record;

    public DumbbellManEvent(RoomEventContext context) :
        base(context, ActorName)
    {
        _record = _database.Record;
    }

    internal DumbbellManEventDatabase Database => _database;

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
            "INTERAC_DUMBBELL_MAN");
        DumbbellManCharacter man = actor as DumbbellManCharacter ??
            throw new InvalidOperationException(
                "Room 2:e8 instantiated INTERAC_DUMBBELL_MAN without its native actor.");
        StartInfiniteScript(
            man,
            _database.Commands,
            _record.InitialScriptUpdates);

        // interactionCode51 state 0 falls through to state 1 and runs the
        // newly installed script once before interactionAnimateAsNpc.
        man.AdvanceDumbbellMan(Context.Player);
    }

    public override void UpdateFrame()
    {
        AdvanceInfiniteScript();
        ScriptActor?.AdvanceDumbbellMan(Context.Player);
    }

    public void UpdateDuringDialogueFrame() =>
        ScriptActor?.AdvanceDumbbellMan(Context.Player);

    bool ICutsceneCommandHost.RoomFlagSet(int flag)
    {
        if (flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"dumbbellManScript cannot read room flag ${flag:x2}.");
        }
        return Context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    bool ICutsceneCommandHost.TradeItemEquals(int value)
    {
        if (value != _record.RequiredTradeItem)
        {
            throw new InvalidOperationException(
                $"dumbbellManScript cannot compare trade item ${value:x2}.");
        }
        return Context.Inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
            Context.Inventory.TradeItem == value;
    }

    bool ICutsceneCommandHost.TextOptionEquals(int value) =>
        EventResources.RequireDialogueChoice("dumbbellManScript text-option branch has no completed choice result.") == value;

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId is < 0x0b1d or > 0x0b24)
        {
            throw new InvalidOperationException(
                $"dumbbellManScript requested unknown TX_{textId:x4}.");
        }
        if (textId == 0x0b21)
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
                $"Dumbbell Man animation ${animation:x2} payload diverged from metadata.");
        }
        RequireScriptActor(actor).SetScriptAnimation(encodedAnimation);
    }

    public override void InitializeActorCollisionRadii(string actor) =>
        RequireScriptActor(actor).InitializeCollisionRadii();

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX)
    {
        if (radiusY != _record.CollisionRadiusY ||
            radiusX != _record.CollisionRadiusX)
        {
            throw new InvalidOperationException(
                $"dumbbellManScript initialized unexpected collision radii " +
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
                $"dumbbellManScript requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }

        Context.GrantScriptTreasure(
            _record.Group,
            _record.Room,
            treasureId,
            parameter,
            _record.RewardObject,
            "scriptHelper.s:dumbbellManScript giveitem TREASURE_TRADEITEM,$06");
    }

}
