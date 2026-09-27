using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// interactionCode53 does not set the always-update bit: both script and
// npcFaceLinkAndAnimate freeze during text, unlike the neighboring trade NPCs.
internal sealed class MamamuEvent : InteractiveInfiniteScriptHost<NpcCharacter>, IRoomEntryEvent
{
    internal IReadOnlyList<CutsceneCommand> Commands { get; } =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/mamamu_commands.tsv");
    internal Func<int, Action<bool>, bool>? OpenSecretMenu { get; set; }
    private bool _secretPending;
    private int _secretResult;
    private int _generation;
    public override bool DialogueOpen => Context.DialogueOpen || _secretPending;
    public bool FreezesNonInteractionObjects => BlocksGameplay;
    public bool MenusDisabled => BlocksGameplay;
    public bool ScreenTransitionsDisabled => BlocksGameplay;

    public MamamuEvent(RoomEventContext context) : base(context, "Mamamu") { }
    public bool Matches(int group, OracleRoomData room) => group == 2 && room.Id == 0xe7;
    public void Start(OracleRoomData room)
    {
        var actor = Context.RequireNpc(2, room.Id, InteractionId.MamamuYan, 0, "INTERAC_MAMAMU_YAN");
        StartInfiniteScript(actor, Commands);
        Context.Entities.EntityAdapters<MamamuRoomEntity>().Single().Script = this;
    }
    // The entity dispatcher samples text once before walking interaction slots.
    // Mamamu precedes the dog; opening text blocks the dog's script, but its
    // native animation tail still runs on this same pass (bank0:updateInteractions).
    public override void UpdateFrame() { }
    internal void AdvanceObjectFrame()
    {
        AdvanceInfiniteScript();
        ScriptActor?.FaceLinkAndAnimateOneUpdate(Context.Player);
    }
    public override void InitializeActorCollisionRadii(string actor) => RequireScriptActor(actor).InitializeCollisionRadii();
    public override void SetActorCoordinates(string actor, int y, int x) => RequireScriptActor(actor).SetStatePosition(new Vector2(x, y));
    public override bool RoomFlagSet(int flag) => Context.Rooms.SaveData.HasRoomFlag(2, 0xe7, (byte)flag);
    public override void OrRoomFlag(int flag) => Context.Rooms.SaveData.SetRoomFlag(2, 0xe7, (byte)flag);
    public override bool TradeItemEquals(int value) => Context.Inventory.HasTreasure(TreasureId.TradeItem) && Context.Inventory.TradeItem == value;
    public override bool TextOptionEquals(int value) => EventResources.RequireDialogueChoice("mamamuYanScript has no completed choice.") == value;
    public override bool MemoryEquals(string binding, int value)
    {
        if (binding.StartsWith("Global:", StringComparison.Ordinal) && int.TryParse(binding.AsSpan(7), out int flag))
            return (Context.Rooms.SaveData.HasGlobalFlag(flag) ? 1 : 0) == value;
        if (binding == "wTextInputResult") return _secretResult == value;
        throw UnsupportedCommand($"read Mamamu memory '{binding}'");
    }
    public override void ShowText(int textId, string message) => ShowText(textId, message, null);
    public override void ShowText(int textId, string message, int? textboxPosition)
    {
        if (message.Contains("\\opt(", StringComparison.Ordinal)) Context.ShowChoiceDialogue(message, textboxPosition: textboxPosition);
        else Context.ShowDialogue(message, textboxPosition);
    }
    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId != TreasureId.TradeItem || parameter != 5) throw UnsupportedCommand($"Mamamu reward ${treasureId:x2}:${parameter:x2}");
        Context.GrantScriptTreasure(2, 0xe7, treasureId, parameter, "TREASURE_OBJECT_TRADEITEM_05",
            "scriptHelper.s:mamamuYanScript giveitem TREASURE_TRADEITEM,$05");
    }
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "LoadText:0b44":
                var text = Commands.OfType<CutsceneShowTextCommand>().Single(c => c.TextId == 0x0b44);
                ScriptActor!.SetDialogue(text.TextId, text.Message, canFace: true);
                break;
            case "AskSecret":
                _secretPending = true;
                int generation = _generation;
                if (OpenSecretMenu is null || !OpenSecretMenu(0x06, valid =>
                    {
                        if (generation != _generation) return;
                        _secretResult = valid ? 0 : 1;
                        _secretPending = false;
                    })) throw UnsupportedCommand("acquire MENU_SECRET for MAMAMU_SECRET $06");
                break;
            case "mamamuYanRandomizeDogLocation":
                int old = Context.Entities.RuntimeState.ReadWramByte(OracleRuntimeState.MamamuDogLocationAddress);
                int next;
                do { next = Context.Entities.NextRandomValue() & 3; } while (next == old);
                Context.Entities.RuntimeState.SetWramByte(OracleRuntimeState.MamamuDogLocationAddress, (byte)next);
                break;
            case "forceLinkDirection, DIR_LEFT":
                Context.Player.Face(Vector2I.Left);
                break;
            case "giveRingAToLink, SNOWSHOE_RING":
                if (!Context.Entities.InteractionSlotAvailable) return;
                Context.Entities.GrantGroundTreasure(new GroundTreasureGrantRequest(2, 0xe7, 0,
                    (int)Context.Player.Position.Y, (int)Context.Player.Position.X,
                    "TREASURE_OBJECT_RING_00", "scriptHelper.s:mamamuYanScript:giveRingAToLink")
                {
                    SpawnMode = TreasureSpawnMode.Instant, GrabMode = TreasureGrabMode.TwoHands,
                    InventoryWrite = GroundTreasureInventoryWrite.UnappraisedRing, InventoryParameter = 0x21,
                    RoomFlagTiming = GroundTreasureRoomFlagTiming.Never
                }, Context.Player);
                break;
            default: throw UnsupportedCommand($"Mamamu helper '{handler}'");
        }
    }
    protected override void ResetEventState()
    {
        _generation++;
        _secretPending = false;
        _secretResult = 0;
    }
    protected override void ReleaseScriptActor(NpcCharacter actor)
    {
        foreach (var entity in Context.Entities.EntityAdapters<MamamuRoomEntity>())
            if (ReferenceEquals(entity.Node, actor)) entity.Script = null;
    }
}
