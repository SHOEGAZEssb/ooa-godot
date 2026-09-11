using System;
using System.Linq;
using Godot;

namespace oracleofages;

/// <summary>tokayCookScript for INTERAC_TOKAY $48:$05; native movement follows each script update.</summary>
internal sealed class TokayCookEvent :
    InteractiveInfiniteScriptHost<TokayCharacter>, IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent
{
    private readonly TokayInteractionDatabase _interactions;
    private readonly TokayNativeDatabase _native = new();
    private readonly System.Collections.Generic.IReadOnlyList<CutsceneCommand> _commands =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/tokay_cook_commands.tsv");
    private GroundTreasurePickup? _reward;
    private bool _jumping;
    private bool _awayFromStart;
    private int _jumpIndex;
    private int _jumpState;
    private int _z;
    private int _speedZ;
    private Vector2 _position;

    internal TokayCookEvent(RoomEventContext context, TokayInteractionDatabase database)
        : base(context, "Cook") => _interactions = database;

    public bool MenusDisabled => BlocksGameplay;
    internal bool Jumping => _jumping;
    internal bool AwayFromStart => _awayFromStart;

    public bool Matches(int group, OracleRoomData room) =>
        Context.Entities.Entities<TokayCharacter>().Any(actor =>
            actor.Active && actor.Record.Group == group &&
            actor.Record.Room == room.Id && actor.Record.SubId == 0x05);

    public void Start(OracleRoomData room)
    {
        TokayCharacter actor = Context.Entities.Entities<TokayCharacter>().Single(
            actor => actor.Active && actor.Record.SubId == 0x05);
        actor.ScriptOwnsNativeUpdate = true;
        // @initSubid05 falls through tokayState1 and interactionRunScript.
        StartInfiniteScript(actor, _commands, initialScriptUpdates: 1);
        UpdateActor();
    }

    public override void UpdateFrame()
    {
        AdvanceInfiniteScript();
        UpdateActor();
    }

    public void UpdateDuringDialogueFrame() => UpdateActor();

    public override void InitializeActorCollisionRadii(string actor) =>
        RequireScriptActor(actor).InitializeCollisionRadii();

    public override bool RoomFlagSet(int flag) =>
        Context.Rooms.SaveData.HasRoomFlag(
            Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, (byte)flag);

    public override bool TradeItemEquals(int value) =>
        Context.Inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) &&
        Context.Inventory.TradeItem == value;

    public override bool TextOptionEquals(int value) =>
        EventResources.RequireDialogueChoice("tokayCookScript has no completed text option.") == value;

    public override void ShowText(int textId, string message)
    {
        if (textId == 0x0a01)
            Context.ShowChoiceDialogue(message);
        else
            Context.ShowDialogue(message);
    }

    public override bool MemoryEquals(string binding, int value) =>
        binding == "CookAwayFromStart"
            ? (_awayFromStart ? 1 : 0) == value
            : throw UnsupportedCommand($"read '{binding}'");

    public override void WriteObjectByte(string actor, int address, int value)
    {
        TokayCharacter cook = RequireScriptActor(actor);
        if (address != 0x3f || value is not (0 or 1))
            throw UnsupportedCommand($"write Cook.${address:x2}=${value:x2}");
        _jumping = value != 0;
        if (_jumping)
            _position = cook.Position;
    }

    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId != 0x41 || parameter != 0x03)
            throw UnsupportedCommand($"give treasure ${treasureId:x2}:${parameter:x2}");
        _reward = Context.GrantScriptTreasure(
            Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id,
            treasureId, parameter, "TREASURE_OBJECT_TRADEITEM_03",
            "scriptHelper.s:tokayCookScript giveitem TREASURE_TRADEITEM,$03");
    }

    protected override void ReleaseScriptActor(TokayCharacter actor)
    {
        actor.ScriptOwnsNativeUpdate = false;
        actor.SetScriptDrawOffset(Vector2.Zero);
    }

    protected override void ResetEventState()
    {
        _reward?.Finish(Context.Player);
        _reward = null;
        _jumping = false;
        _awayFromStart = false;
        _jumpIndex = _jumpState = _z = _speedZ = 0;
        _position = Vector2.Zero;
    }

    private void UpdateActor()
    {
        if (ScriptActor is not { } actor) return;
        if (!_jumping)
        {
            actor.FaceLinkAndAnimateOneUpdate(Context.Player);
            return;
        }
        if (_jumpState is 0 or 2)
        {
            if (_jumpState == 2) _jumpIndex = (_jumpIndex + 1) % _native.CookPaths.Count;
            _speedZ = _native.CookPaths[_jumpIndex].SpeedZ;
            _awayFromStart = true;
            _jumpState = 1;
            Context.Sound.PlaySound(_interactions.SoundJump);
        }
        else
        {
            TokayCookJump jump = _native.CookPaths[_jumpIndex];
            if (OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, jump.Gravity))
            {
                _jumpState = 2;
                if (_jumpIndex == 5)
                {
                    _position = new Vector2(0x48, 0x28);
                    _awayFromStart = false;
                }
            }
            else
                OracleObjectMovement.Shared.ApplySpeed(ref _position, _native.Constant("speed-300"), jump.Angle);
        }
        actor.SetStatePosition(OracleObjectMath.ToPixelPosition(_position));
        actor.SetScriptDrawOffset(new Vector2(0, _z >> 8));
        actor.AnimateAndUpdateDrawPriorityOneUpdate(Context.Player);
    }
}
