
namespace Oracles;

internal abstract class TradeNpcEvent(RoomEventContext context, string actorName,
    TradeNpcEventRecord record, IReadOnlyList<CutsceneCommand> commands, int choiceText)
    : InteractiveInfiniteScriptHost<TradeNpcCharacter>(context, actorName),
        IRoomEntryEvent, IUpdatesDuringDialogueRoomEvent
{
    protected abstract bool AcceptText(int textId);
    public bool Matches(int group, OracleRoomData room) => group == record.Group && room.Id == record.Room;

    public void Start(OracleRoomData room)
    {
        var actor = Context.RequireNpc(record.Group, record.Room, record.InteractionId, record.SubId, ScriptActorName)
            as TradeNpcCharacter ?? throw UnsupportedCommand($"start INTERAC ${record.InteractionId:x2}:${record.SubId:x2} without its native actor");
        // interactionCode51/5c state zero runs the installed script, then animates as an NPC.
        StartInfiniteScript(actor, commands, record.InitialScriptUpdates);
        actor.AdvanceTradeNpc(Context.Player);
    }

    public override void UpdateFrame()
    {
        AdvanceInfiniteScript();
        ScriptActor?.AdvanceTradeNpc(Context.Player);
    }

    public void UpdateDuringDialogueFrame() => ScriptActor?.AdvanceTradeNpc(Context.Player);

    public override bool RoomFlagSet(int flag) => flag == record.RoomFlag
        ? Context.Rooms.SaveData.HasRoomFlag(record.Group, record.Room, (byte)flag)
        : throw UnsupportedCommand($"read room flag ${flag:x2}");

    public override bool TradeItemEquals(int value) => value == record.RequiredTradeItem
        ? Context.Inventory.HasTreasure(TreasureId.TradeItem) && Context.Inventory.TradeItem == value
        : throw UnsupportedCommand($"compare trade item ${value:x2}");

    public override bool TextOptionEquals(int value) =>
        EventResources.RequireDialogueChoice($"{ScriptActorName} text-option branch has no completed choice result.") == value;

    public override void ShowText(int textId, string message) => ShowTradeText(textId, message, null);

    protected void ShowTradeText(int textId, string message, int? position)
    {
        if (!AcceptText(textId)) throw UnsupportedCommand($"show unknown TX_{textId:x4}");
        if (textId == choiceText) Context.ShowChoiceDialogue(message, textboxPosition: position);
        else Context.ShowDialogue(message, position);
    }

    public override void SetActorAnimation(string actor, int animation, string encodedAnimation)
    {
        if (encodedAnimation != record.Animation(animation))
            throw UnsupportedCommand($"set divergent animation ${animation:x2}");
        RequireScriptActor(actor).SetScriptAnimation(encodedAnimation);
    }

    public override void InitializeActorCollisionRadii(string actor) => RequireScriptActor(actor).InitializeCollisionRadii();

    public override void SetActorCollisionRadii(string actor, int radiusY, int radiusX)
    {
        if (radiusY != record.CollisionRadiusY || radiusX != record.CollisionRadiusX)
            throw UnsupportedCommand($"set unexpected collision radii ${radiusY:x2}/${radiusX:x2}");
        RequireScriptActor(actor).SetCollisionRadii(radiusY, radiusX);
    }

    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId != record.RewardTreasure || parameter != record.RewardParameter)
            throw UnsupportedCommand($"give unexpected reward ${treasureId:x2}:${parameter:x2}");
        Context.GrantScriptTreasure(record.Group, record.Room, treasureId, parameter, record.RewardObject,
            $"scriptHelper.s:{ScriptActorName} giveitem ${treasureId:x2},${parameter:x2}");
    }
}
