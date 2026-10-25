using System;
using System.Linq;

namespace Oracles;

/// <summary>pirateCaptainScript runs from wBigBuffer, in the captain's $c3 slot.</summary>
internal sealed class PirateCaptainEvent(RoomEventContext context)
    : InteractiveInfiniteScriptHost<NpcCharacter>(context, "Captain"), IRoomEntryEvent
{
    public bool FreezesNonInteractionObjects => BlocksGameplay;
    public bool MenusDisabled => BlocksGameplay;
    public bool ScreenTransitionsDisabled => BlocksGameplay;
    public bool Matches(int group, OracleRoomData room) => group == 5 && room.Id == 0xf8;
    public void Start(OracleRoomData room)
    {
        var actor = Context.RequireNpc(5, 0xf8, AgesInteractionId.PirateCaptain, 0, "INTERAC_PIRATE_CAPTAIN");
        StartInfiniteScript(actor, PirateShipDatabase.Shared.CaptainCommands);
    }
    internal void InitializeCaptain(NpcCharacter actor)
    {
        if (!ReferenceEquals(actor, ScriptActor)) throw new InvalidOperationException("pirateCaptain.s: missing captain script owner.");
        if (!Context.Rooms.SaveData.IsLinkedGame) Context.Rooms.CurrentRoom.OrTilesetFlags((byte)TilesetFlags.Past);
    }
    // The room scheduler owns input/cancellation; native entity owns dispatch.
    public override void UpdateFrame() { }
    internal void AdvanceInteraction()
    {
        ScriptActor?.PreventPlayerPassing(Context.Player);
        AdvanceInfiniteScript();
    }
    internal bool TryInteractPirate(NpcCharacter npc)
    {
        if (npc.Record.Id == AgesInteractionId.PirateCaptain) return TryInteractNpc(npc);
        if (npc.Record.Id != AgesInteractionId.Pirate || npc.Record.SubId > 3) return false;
        return Context.Entities.EntityAdapters<PirateCrewRoomEntity>()
            .Single(crew => ReferenceEquals(crew.Node, npc)).QueueButton();
    }
    public override void InitializeActorCollisionRadii(string actor) => RequireScriptActor(actor).InitializeCollisionRadii();
    public override void ShowText(int id, string message) => ShowText(id, message, null);
    public override void ShowText(int id, string message, int? position)
    {
        if (id is 0x3600 or 0x3601) Context.ShowChoiceDialogue(message, textboxPosition: position);
        else Context.ShowDialogue(message, position, sourceTextId: id);
    }
    public override bool TextOptionEquals(int value) => EventResources.RequireDialogueChoice("Pirate captain has no completed answer.") == value;
    public override bool GateOpen(string gate) => gate == "CrewLanded"
        ? (Context.Entities.RuntimeState.ReadWramByte(AgesWramAddress.wTmpcfc0) & PirateCrewRoomEntity.LandedSignal) != 0
        : throw UnsupportedCommand($"read {gate}");
    public override void GiveItem(int treasure, int parameter)
    {
        if (treasure != TreasureId.TokayEyeball || parameter != 0) throw UnsupportedCommand($"give ${treasure:x2}:${parameter:x2}");
        Context.GrantScriptTreasure(5, 0xf8, treasure, parameter, "TREASURE_OBJECT_TOKAY_EYEBALL_00",
            "scripts/ages/scriptHelper.s:pirateCaptainScript", nativeDispatch: true);
        WaitForNativeCounter(3);
    }
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "SignalJump":
                var runtime = Context.Entities.RuntimeState;
                runtime.SetWramByte(AgesWramAddress.wTmpcfc0, (byte)(runtime.ReadWramByte(AgesWramAddress.wTmpcfc0) ^ PirateCrewRoomEntity.JumpSignal));
                break;
            case "loseTreasure, TREASURE_ZORA_SCALE": Context.Inventory.LoseTreasure(TreasureId.ZoraScale); break;
            case "pirateCaptain_warpOut":
                // Manager dispatches this after the later crew slots. The
                // script continues to set $34 before that room replacement.
                Context.Entities.OnRoomWarpRequested(PirateShipDatabase.Shared.Warp(
                    Context.Rooms.SaveData.IsLinkedGame ? "linked" : "unlinked", 5, 0xf8));
                break;
            default: throw UnsupportedCommand($"run {handler}");
        }
    }
    public override void ScriptEnded() => ReleaseInputControl();
}
