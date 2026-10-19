using Godot;
using System;

namespace oracleofages;

internal sealed class OceanZoraEvent(RoomEventContext context, OceanInteractionDatabase data)
    : InteractiveInfiniteScriptHost<NpcCharacter>(context, "Zora"), IRoomEntryEvent,
      IUpdatesDuringDialogueRoomEvent
{
    // scripts/ages/scriptHelper.s: genericCutscene.cfc1 is shared WRAM scratch.
    private const int SignalAddress = WramAddress.wTmpcfc0 + 1;
    private Vector2 _position;
    private int _alignmentCounter;
    private bool _scale;
    private bool _walkingAway;
    public bool FreezesNonInteractionObjects=>BlocksGameplay;
    public bool MenusDisabled=>BlocksGameplay;
    public bool ScreenTransitionsDisabled=>BlocksGameplay;

    public bool Matches(int group, OracleRoomData room) =>
        group == 2 && room.Id is 0xa0 or 0xd7 || group == 3 && room.Id == 0xd6;

    public void Start(OracleRoomData room)
    {
        int group = Context.Rooms.ActiveGroup;
        int subid = room.Id == 0xa0 ? 0x10 : group == 2 ? 0x12 : 0x11;
        var save = Context.Rooms.SaveData;
        _scale = subid == 0x10;
        if (_scale ? save.HasRoomFlag(group, room.Id, 0x20) || (Context.Inventory.Essences & 0x40) == 0 :
            save.HasRoomFlag(group, room.Id, 0x40) || save.IsLinkedGame != (subid == 0x12)) return;
        var actor = Context.RequireNpc(group, room.Id, InteractionId.Zora, subid, "INTERAC_ZORA");
        actor.SetDialogue(_scale ? 0 : 0x3431, _scale ? "" : actor.Message, canFace: !_scale);
        actor.SetFacingDirection(_scale ? Vector2I.Left : Vector2I.Down);
        actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        actor.ShowInitializedPresentation();
        actor.SetAnimationRate(0);
        _position = actor.Position;
        _alignmentCounter = 0;
        _walkingAway = _scale;
        StartInfiniteScript(actor, _scale ? data.Scale : data.StormGuard);
    }

    public override void UpdateFrame()
    {
        // $ab:$10/$11/$12 use their own initializer without the common
        // always-update bit. Admit native tails with the original slot walk.
        if (!Context.Entities.InitializedInteractionUpdatesAllowed) return;
        // interactionRunScript's counter1=$01 expires before counter2. A
        // counter2 zero update yields, and dispatch resumes the next update.
        if (!ScriptExecutionBlocked && _alignmentCounter != 0)
        {
            if (--_alignmentCounter != 0) MoveActorAtSpeed("Zora", 0x28, ObjectAngle.Left);
        }
        else AdvanceInfiniteScript();
        AnimateActor();
    }

    private void AnimateActor()
    {
        if (ScriptActor is not { Active: true } actor) return;
        if (_walkingAway) actor.AdvanceAnimationUpdates(1);
        else actor.FaceLinkAndAnimateOneUpdate(Context.Player, preventPassing: false);
    }
    public void UpdateDuringDialogueFrame() => UpdateFrame();
    public override void InitializeActorCollisionRadii(string actor) => RequireScriptActor(actor).InitializeCollisionRadii();
    public override bool MemoryEquals(string binding, int value) => binding == "HasZoraScale" && value == 1
        ? Context.Inventory.HasTreasure(TreasureId.ZoraScale) : throw UnsupportedCommand($"compare {binding}");
    public override int ReadMemory(string binding) => binding == "OceanSignal"
        ? Context.Entities.RuntimeState.ReadWramByte(SignalAddress) : throw UnsupportedCommand($"read {binding}");
    public override void ShowText(int id, string message, int? position) => Context.ShowDialogue(message, position, sourceTextId: id);
    public override void SetActorMovementAnimation(string actor, int angle, string animation) => RequireScriptActor(actor).SetScriptAnimation(animation);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) =>
        RequireScriptActor(actor).SetStatePosition(NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState, ref _position, speed, angle));
    public override void OrRoomFlag(int flag) => Context.Rooms.SaveData.SetRoomFlag(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, (byte)flag);
    public override void GiveItem(int treasure, int parameter)
    {
        if (treasure != TreasureId.ZoraScale || parameter != 0) throw UnsupportedCommand($"give ${treasure:x2}:${parameter:x2}");
        Context.GrantScriptTreasure(2, 0xa0, treasure, parameter, "TREASURE_OBJECT_ZORA_SCALE_00", "scripts.s:zoraSubid10Script",nativeDispatch:true);
        WaitForNativeCounter(3);
    }
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "zora_waitForLinkToMoveDown":
                Context.Entities.RuntimeState.SetWramByte(SignalAddress, (byte)(Context.Player.Position.Y >= 0x18 ? 1 : 0)); break;
            case "zora_checkIsLinkedGame":
                Context.Entities.RuntimeState.SetWramByte(SignalAddress, (byte)(Context.Rooms.SaveData.IsLinkedGame ? 1 : 0)); break;
            case "zora_makeLinkFaceDown": Context.Player.Face(Vector2I.Down); Context.Player.ClearNativeItemParents(); break;
            case "zora_setLinkDirectionUp": Context.Player.Face(Vector2I.Up); break;
            case "zora_setLinkDirectionRight": Context.Player.Face(Vector2I.Right); break;
            case "zora_moveToLinksXPosition":
                _alignmentCounter = ((int)RequireScriptActor("Zora").Position.X - (int)Context.Player.Position.X) & 0xff; break;
            case "zora_createExclamationMarkToTheRight":
                Context.Sound.PlaySound(SoundId.SndClink);
                if (Context.Entities.InteractionSlotAvailable) Context.Entities.Spawn(new ExclamationMarkSpawn(RequireScriptActor("Zora").Position + new Vector2(8, -8), true, 0x2d));
                break;
            case "zora_incState": _walkingAway = true; break;
            default: throw UnsupportedCommand($"run {handler}");
        }
    }
    public override void ScriptEnded() => RequireScriptActor("Zora").SetActive(false);
    protected override void ResetEventState() { _alignmentCounter = 0; _walkingAway = false; }
}
