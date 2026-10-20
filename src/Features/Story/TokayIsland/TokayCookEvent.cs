using System;
using System.Linq;
using Godot;

namespace oracleofages;

/// <summary>tokayCookScript for INTERAC_TOKAY $48:$05; native movement follows each script update.</summary>
internal sealed class TokayCookEvent :
    InteractiveInfiniteScriptHost<TokayCharacter>, IRoomEntryEvent
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
    private TokayNpcRoomEntity? _slot;
    private bool _ownsInput;

    internal TokayCookEvent(RoomEventContext context, TokayInteractionDatabase database)
        : base(context, "Cook") => _interactions = database;

    public bool MenusDisabled => BlocksGameplay;
    public override bool BlocksGameplay => _ownsInput;
    internal bool Jumping => _jumping;

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
        StartInfiniteScript(actor, _commands);
        _slot=Context.Entities.EntityAdapters<TokayNpcRoomEntity>().Single(slot=>ReferenceEquals(slot.Npc,actor));
        _slot.NativeUpdate=AdvanceObject;
    }

    public override void UpdateFrame() { } // The physical object slot dispatches this lane.
    private void AdvanceObject()
    {
        AdvanceInfiniteScript();
        UpdateActor();
    }

    public override void SetInputEnabled(bool enabled)
    {
        NativeScriptInputControl.SetEnabled(Context.Entities.RuntimeState,Context.Player,enabled);
        _ownsInput=!enabled;
    }

    public override void InitializeActorCollisionRadii(string actor) =>
        RequireScriptActor(actor).InitializeCollisionRadii();

    public override bool RoomFlagSet(int flag) =>
        Context.Rooms.SaveData.HasRoomFlag(
            Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, (byte)flag);

    public override bool TradeItemEquals(int value) =>
        Context.Inventory.HasTreasure(TreasureId.TradeItem) &&
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
    public override int ReadMemory(string binding) => binding=="CookTradeOutcome"
        ? !Context.Inventory.HasTreasure(TreasureId.TradeItem)||Context.Inventory.TradeItem<2 ? 0
            : Context.Inventory.TradeItem==2 ? 1 : 2
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
        if (treasureId != TreasureId.TradeItem || parameter != 0x03)
            throw UnsupportedCommand($"give treasure ${treasureId:x2}:${parameter:x2}");
        if(!Context.Entities.InteractionSlotAvailable)return;
        TreasureObjectRecord treasure=Context.Treasures.GetObject("TREASURE_OBJECT_TRADEITEM_03");
        Vector2 position=Context.Player.Position;
        _reward=Context.Entities.SpawnGroundTreasure(new GroundTreasureGrantRequest(
            Context.Rooms.ActiveGroup,Context.Rooms.CurrentRoom.Id,0,(int)position.Y,(int)position.X,
            treasure.Name,"scriptHelper.s:tokayCookScript giveitem TREASURE_TRADEITEM,$03")
        {
            SpawnMode=(treasure.NativeFlags>>4)&7,GrabMode=treasure.NativeFlags&7,
            RoomFlagTiming=(treasure.NativeFlags&8)!=0?GroundTreasureRoomFlagTiming.OnActivation:GroundTreasureRoomFlagTiming.Never,
            ExpectedTreasureId=treasureId,ExpectedSubId=parameter,ExpectedObjectParameter=treasure.Parameter
        });
        _reward.BeginScriptGrant();
        WaitForNativeCounter(3);
    }

    protected override void ReleaseScriptActor(TokayCharacter actor)
    {
        if(_slot is not null)_slot.NativeUpdate=null;
        _slot=null;
        actor.ScriptOwnsNativeUpdate = false;
        actor.SetScriptDrawOffset(Vector2.Zero);
    }

    protected override void ResetEventState()
    {
        if(_ownsInput)SetInputEnabled(true);
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
                NativeObjectMovement.ApplySpeed(Context.Entities.RuntimeState,
                    ref _position, _native.Constant("speed-300"), jump.Angle);
        }
        actor.SetStatePosition(OracleObjectMath.ToPixelPosition(_position));
        actor.SetScriptDrawOffset(new Vector2(0, _z >> 8));
        actor.AnimateAndUpdateDrawPriorityOneUpdate(Context.Player);
    }
}
