using System;
using System.Collections.Generic;

namespace oracleofages;

// interactionCode5a and scriptHelp.oldZoraScript: one initial script update,
// then script/animation/push/priority in that order. No facing helper is called.
internal sealed class OldZoraEvent : InteractiveInfiniteScriptHost<OldZoraCharacter>,
    IRoomEntryEvent, ICutsceneCommandHost, IUpdatesDuringDialogueRoomEvent
{
    internal IReadOnlyList<CutsceneCommand> Commands { get; } =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/old_zora_commands.tsv");

    public OldZoraEvent(RoomEventContext context) : base(context, "OldZora") { }

    public bool Matches(int group, OracleRoomData room) => group == 2 && room.Id == 0xf5;

    public void Start(OracleRoomData room)
    {
        var actor = Context.RequireNpc(2, room.Id, 0x5a, 0, "INTERAC_OLD_ZORA")
            as OldZoraCharacter ?? throw new InvalidOperationException(
                "Room 2:f5 INTERAC_OLD_ZORA lacks its native actor.");
        StartInfiniteScript(actor, Commands, 1);
        actor.AdvanceOldZora(Context.Player);
    }

    public override void UpdateFrame()
    {
        AdvanceInfiniteScript();
        ScriptActor?.AdvanceOldZora(Context.Player);
    }

    public void UpdateDuringDialogueFrame() => ScriptActor?.AdvanceOldZora(Context.Player);

    public override void InitializeActorCollisionRadii(string actor) =>
        RequireScriptActor(actor).InitializeCollisionRadii();

    bool ICutsceneCommandHost.RoomFlagSet(int flag) => flag == 0x20
        ? Context.Rooms.SaveData.HasRoomFlag(2, 0xf5, (byte)flag)
        : throw new InvalidOperationException($"oldZoraScript unknown room flag ${flag:x2}.");

    bool ICutsceneCommandHost.TradeItemEquals(int value) => value == 0x0a
        ? Context.Inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
            Context.Inventory.TradeItem == value
        : throw new InvalidOperationException($"oldZoraScript unknown trade item ${value:x2}.");

    bool ICutsceneCommandHost.TextOptionEquals(int value) =>
        EventResources.RequireDialogueChoice("oldZoraScript has no completed choice.") == value;

    void ICutsceneCommandHost.ShowText(int textId, string message) => ShowText(textId, message, null);

    public override void ShowText(int textId, string message, int? textboxPosition)
    {
        if (textId is < 0x0b33 or > 0x0b39)
            throw new InvalidOperationException($"oldZoraScript unknown TX_{textId:x4}.");
        if (textId == 0x0b35)
            Context.ShowChoiceDialogue(message, textboxPosition: textboxPosition);
        else
            Context.ShowDialogue(message, textboxPosition);
    }

    void ICutsceneCommandHost.GiveItem(int treasureId, int parameter)
    {
        if (treasureId != 0x41 || parameter != 0x0b)
            throw new InvalidOperationException($"oldZoraScript unknown reward ${treasureId:x2}:${parameter:x2}.");
        Context.GrantScriptTreasure(2, 0xf5, treasureId, parameter,
            "TREASURE_OBJECT_TRADEITEM_0b", "scriptHelper.s:oldZoraScript giveitem TREASURE_TRADEITEM,$0b");
    }
}
