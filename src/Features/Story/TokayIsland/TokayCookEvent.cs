using System;
using Godot;

namespace oracleofages;

/// <summary>
/// tokayCookScript for INTERAC_TOKAY $48:$05.
/// </summary>
internal sealed class TokayCookEvent : TokayScriptEvent, IRoomEvent, IUpdatesDuringDialogueRoomEvent
{
    private TokayCookStage _stage;
    private GroundTreasurePickup? _reward;
    private int _counter;
    private readonly TokayNativeDatabase _native = new();
    private TokayCharacter? _actor;
    private bool _jumping;
    private bool _awayFromStart;
    private int _jumpIndex;
    private int _jumpState;
    private int _z;
    private int _speedZ;
    private Vector2 _position;
    private int _choice;

    internal TokayCookEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
        : base(context, database)
    {
    }

    public bool MenusDisabled => HasState;
    public bool HasState => _stage != TokayCookStage.Inactive;
    internal TokayCookStage Stage => _stage;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active || npc.Record is not { Id: 0x48, SubId: 0x05 })
            return false;
        _actor = (TokayCharacter)npc;
        _actor.ScriptOwnsNativeUpdate = true;
        LockInput();

        if (CurrentRoomFlag(OracleSaveData.RoomFlagItem))
        {
            Show(0x0a07);
            _stage = TokayCookStage.DialogueOnly;
            return true;
        }
        Show(0x0a00);
        _stage = TokayCookStage.Intro;
        return true;
    }

    public void UpdateFrame()
    {
        UpdateScript();
        UpdateActor();
    }

    public void UpdateDuringDialogueFrame() => UpdateActor();

    private void UpdateScript()
    {
        if (_stage == TokayCookStage.Inactive)
            return;
        if (_stage is TokayCookStage.CheckWait or TokayCookStage.AcceptedWait or
            TokayCookStage.SecondAcceptedWait or TokayCookStage.ThirdAcceptedWait or
            TokayCookStage.CookingWait or TokayCookStage.GiveWait or TokayCookStage.ChoiceWait)
        {
            if (--_counter == 0)
                CompleteWait();
            return;
        }
        if (_stage == TokayCookStage.Reward)
        {
            if (_reward is { Finished: true })
            {
                _reward = null;
                SetCurrentRoomFlag(OracleSaveData.RoomFlagItem);
                FinishInteraction();
            }
            return;
        }
        if (Context.DialogueOpen)
            return;

        switch (_stage)
        {
            case TokayCookStage.DialogueOnly:
            case TokayCookStage.WrongItem:
            case TokayCookStage.Declined:
                FinishInteraction();
                break;
            case TokayCookStage.Intro:
                BeginWait(30, TokayCookStage.CheckWait);
                break;
            case TokayCookStage.Prompt:
                _choice = TakeChoice();
                BeginWait(30, TokayCookStage.ChoiceWait);
                break;
            case TokayCookStage.AcceptedText:
                BeginWait(30, TokayCookStage.AcceptedWait);
                break;
            case TokayCookStage.SecondAcceptedText:
                BeginWait(30, TokayCookStage.SecondAcceptedWait);
                break;
            case TokayCookStage.ThirdAcceptedText:
                BeginWait(30, TokayCookStage.ThirdAcceptedWait);
                break;
            case TokayCookStage.JumpText:
                if (!_awayFromStart)
                {
                    _jumping = false;
                    BeginWait(40, TokayCookStage.CookingWait);
                }
                break;
            case TokayCookStage.BeforeRewardText:
                BeginWait(30, TokayCookStage.GiveWait);
                break;
            default:
                throw new InvalidOperationException(
                    $"Tokay cook stage {_stage} closed an unexpected dialogue.");
        }
    }

    public void Cancel()
    {
        _reward?.Finish(Context.Player);
        _reward = null;
        UnlockInput();
        _counter = 0;
        _stage = TokayCookStage.Inactive;
        ReleaseActor();
    }

    private void CompleteWait()
    {
        switch (_stage)
        {
            case TokayCookStage.ChoiceWait:
                Show(_choice == 0 ? 0x0a02 : 0x0a08);
                _stage = _choice == 0 ? TokayCookStage.AcceptedText : TokayCookStage.Declined;
                break;
            case TokayCookStage.CheckWait:
                if (Context.Inventory.TradeItem != 2)
                {
                    Show(0x0a09);
                    _stage = TokayCookStage.WrongItem;
                }
                else
                {
                    ShowChoice(0x0a01);
                    _stage = TokayCookStage.Prompt;
                }
                break;
            case TokayCookStage.AcceptedWait:
                Show(0x0a03);
                _stage = TokayCookStage.SecondAcceptedText;
                break;
            case TokayCookStage.SecondAcceptedWait:
                Show(0x0a04);
                _stage = TokayCookStage.ThirdAcceptedText;
                break;
            case TokayCookStage.ThirdAcceptedWait:
                _jumping = true;
                _jumpIndex = 0;
                _jumpState = 0;
                _position = _actor!.Position;
                Show(0x0a05);
                _stage = TokayCookStage.JumpText;
                break;
            case TokayCookStage.CookingWait:
                Show(0x0a06);
                _stage = TokayCookStage.BeforeRewardText;
                break;
            case TokayCookStage.GiveWait:
                Context.Inventory.LoseTreasure(TreasureDatabase.TreasureTradeItem);
                _reward = Context.GrantScriptTreasure(
                    Context.Rooms.ActiveGroup,
                    Context.Rooms.CurrentRoom.Id,
                    TreasureDatabase.TreasureTradeItem,
                    3,
                    "TREASURE_OBJECT_TRADEITEM_03",
                    "scripts/ages:tokayCookScript",
                    objectParameter: 3);
                _stage = TokayCookStage.Reward;
                break;
            default:
                throw new InvalidOperationException(
                    $"Tokay cook wait completed in stage {_stage}.");
        }
    }

    private void BeginWait(int frames, TokayCookStage stage)
    {
        _counter = frames;
        _stage = stage;
    }

    private int TakeChoice() =>
        RequireDialogueChoice("tokayCookScript prompt closed without a text-option result.");

    private void FinishInteraction()
    {
        ReleaseActor();
        UnlockInput();
        _stage = TokayCookStage.Inactive;
    }

    private void ReleaseActor()
    {
        if (_actor is { } actor && GodotObject.IsInstanceValid(actor))
        {
            actor.ScriptOwnsNativeUpdate = false;
            actor.SetScriptDrawOffset(Vector2.Zero);
        }
        _actor = null;
        _jumping = false;
        _awayFromStart = false;
        _z = 0;
        _speedZ = 0;
        _jumpState = 0;
        _jumpIndex = 0;
    }

    private void UpdateActor()
    {
        if (_actor is not { } actor) return;
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
            Context.Sound.PlaySound(Interactions.SoundJump);
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

internal enum TokayCookStage
{
    Inactive,
    DialogueOnly,
    Intro,
    CheckWait,
    WrongItem,
    Prompt,
    ChoiceWait,
    Declined,
    AcceptedText,
    AcceptedWait,
    SecondAcceptedText,
    SecondAcceptedWait,
    ThirdAcceptedText,
    ThirdAcceptedWait,
    JumpText,
    CookingWait,
    BeforeRewardText,
    GiveWait,
    Reward
}
