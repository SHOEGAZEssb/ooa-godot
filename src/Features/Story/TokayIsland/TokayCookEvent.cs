using System;

namespace oracleofages;

/// <summary>
/// tokayCookScript for INTERAC_TOKAY $48:$05.
/// </summary>
internal sealed class TokayCookEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayInteractionDatabase _database;
    private TokayCookStage _stage;
    private GroundTreasurePickup? _reward;
    private int _counter;
    private bool _inputLocked;

    internal TokayCookEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
    {
        _context = context;
        _database = database;
    }

    public bool HasState => _stage != TokayCookStage.Inactive;
    public bool BlocksGameplay => _inputLocked;
    internal TokayCookStage Stage => _stage;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active || npc.Record is not { Id: 0x48, SubId: 0x05 })
            return false;

        if (CurrentRoomFlag(OracleSaveData.RoomFlagItem))
        {
            Show(0x0a07);
            _stage = TokayCookStage.DialogueOnly;
            return true;
        }
        LockInput();
        Show(0x0a00);
        _stage = TokayCookStage.Intro;
        return true;
    }

    public void UpdateFrame()
    {
        if (_stage == TokayCookStage.Inactive)
            return;
        if (_stage is TokayCookStage.CheckWait or TokayCookStage.AcceptedWait or
            TokayCookStage.SecondAcceptedWait or TokayCookStage.ThirdAcceptedWait or
            TokayCookStage.CookingWait or TokayCookStage.GiveWait)
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
        if (_context.DialogueOpen)
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
                if (TakeChoice() == 0)
                {
                    Show(0x0a02);
                    _stage = TokayCookStage.AcceptedText;
                }
                else
                {
                    Show(0x0a08);
                    _stage = TokayCookStage.Declined;
                }
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
                _context.Sound.PlaySound(_database.SoundJump);
                BeginWait(196, TokayCookStage.CookingWait);
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
        _reward?.Finish(_context.Player);
        _reward = null;
        UnlockInput();
        _counter = 0;
        _stage = TokayCookStage.Inactive;
    }

    private void CompleteWait()
    {
        switch (_stage)
        {
            case TokayCookStage.CheckWait:
                if (_context.Inventory.TradeItem != 2)
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
                Show(0x0a05);
                _stage = TokayCookStage.JumpText;
                break;
            case TokayCookStage.CookingWait:
                Show(0x0a06);
                _stage = TokayCookStage.BeforeRewardText;
                break;
            case TokayCookStage.GiveWait:
                _context.Inventory.LoseTreasure(TreasureDatabase.TreasureTradeItem);
                _reward = _context.GrantScriptTreasure(
                    _context.Rooms.ActiveGroup,
                    _context.Rooms.CurrentRoom.Id,
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

    private bool CurrentRoomFlag(byte flag) =>
        _context.Rooms.SaveData.HasRoomFlag(
            _context.Rooms.ActiveGroup, _context.Rooms.CurrentRoom.Id, flag);

    private void SetCurrentRoomFlag(byte flag) =>
        _context.Rooms.SaveData.SetRoomFlag(
            _context.Rooms.ActiveGroup, _context.Rooms.CurrentRoom.Id, flag);

    private void Show(int textId) =>
        _context.ShowDialogue(_database.Text(textId));

    private void ShowChoice(int textId) =>
        _context.ShowChoiceDialogue(_database.Text(textId));

    private int TakeChoice()
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
            throw new InvalidOperationException(
                "tokayCookScript prompt closed without a text-option result.");
        return choice;
    }

    private void LockInput()
    {
        _context.Player.BeginCutsceneControl();
        _inputLocked = true;
    }

    private void UnlockInput()
    {
        if (!_inputLocked)
            return;
        _context.Player.EndCutsceneControl();
        _inputLocked = false;
    }

    private void FinishInteraction()
    {
        UnlockInput();
        _stage = TokayCookStage.Inactive;
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
