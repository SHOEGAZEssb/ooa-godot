using Godot;
using System;
using System.Linq;

namespace oracleofages;

/// <summary>
/// tokayWithDimitri1Script and tokayWithDimitri2Script for the coordinated
/// INTERAC_TOKAY $48:$0f-$10 pair.
/// </summary>
internal sealed class TokayDimitriEvent : IRoomEntryEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayIslandDatabase _database;
    private TokayDimitriStage _stage;
    private TokayDimitriStage _nextStage;
    private NpcCharacter? _actor;
    private int _counter;
    private bool _inputLocked;

    internal TokayDimitriEvent(
        RoomEventContext context,
        TokayIslandDatabase database)
    {
        _context = context;
        _database = database;
    }

    public bool HasState => _stage != TokayDimitriStage.Inactive;
    public bool BlocksGameplay => _inputLocked;
    internal TokayDimitriStage Stage => _stage;

    public bool Matches(int group, OracleRoomData room) =>
        group == 0 && room.Id == 0xaa &&
        FindActor(0x0f) is { Active: true } &&
        (_context.Rooms.SaveData.ReadWramByte(_database.DimitriStateAddress) & 0x01) == 0;

    public void Start(OracleRoomData room)
    {
        if (!Matches(_context.Rooms.ActiveGroup, room))
            throw new InvalidOperationException(
                $"Tokay Dimitri introduction cannot start in " +
                $"{_context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        _actor = FindActor(0x0f) ?? throw new InvalidOperationException(
            "tokayWithDimitri1Script lost INTERAC_TOKAY $48:$0f on room entry.");
        LockInput();
        Show(0x0a1d);
        _stage = TokayDimitriStage.IntroFirstText;
    }

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active || npc.Record.Id != 0x48 ||
            npc.Record.SubId is not (0x0f or 0x10))
        {
            return false;
        }

        _actor = npc;
        if (npc.Record.SubId == 0x10)
        {
            Show(0x0a1e);
            _stage = TokayDimitriStage.DialogueOnly;
            return true;
        }

        LockInput();
        FaceActorToLink(npc);
        Show(0x0a1f);
        _stage = TokayDimitriStage.TradeIntro;
        return true;
    }

    public void UpdateFrame()
    {
        if (_stage == TokayDimitriStage.Inactive)
            return;
        if (_stage == TokayDimitriStage.Wait)
        {
            if (--_counter == 0)
                EnterStage(_nextStage);
            return;
        }
        if (_context.DialogueOpen)
            return;

        switch (_stage)
        {
            case TokayDimitriStage.DialogueOnly:
            case TokayDimitriStage.IntroSecondText:
                FinishInteraction();
                break;
            case TokayDimitriStage.IntroFirstText:
                BeginWait(30, TokayDimitriStage.IntroSecondText);
                break;
            case TokayDimitriStage.TradeIntro:
                if (_context.Inventory.EmberSeeds == 0)
                    FinishInteraction();
                else
                {
                    ShowChoice(0x0a20);
                    _stage = TokayDimitriStage.TradePrompt;
                }
                break;
            case TokayDimitriStage.TradePrompt:
                if (TakeChoice() != 0)
                {
                    Show(0x0a22);
                    _stage = TokayDimitriStage.DialogueOnly;
                }
                else
                {
                    Show(0x0a23);
                    _stage = TokayDimitriStage.TradeAccepted;
                }
                break;
            case TokayDimitriStage.TradeAccepted:
                _context.Inventory.TryConsumeSeedsFromScript(0x20, 1);
                BeginWait(30, TokayDimitriStage.TradeSecondText);
                break;
            case TokayDimitriStage.TradeSecondText:
                BeginWait(60, TokayDimitriStage.TradeThirdText);
                break;
            case TokayDimitriStage.TradeThirdText:
                CompleteTrade();
                break;
            default:
                throw new InvalidOperationException(
                    $"Tokay Dimitri stage {_stage} closed an unexpected dialogue.");
        }
    }

    public void Cancel()
    {
        UnlockInput();
        _actor = null;
        _counter = 0;
        _stage = TokayDimitriStage.Inactive;
    }

    private void EnterStage(TokayDimitriStage stage)
    {
        _stage = stage;
        switch (stage)
        {
            case TokayDimitriStage.IntroSecondText:
                Show(0x0a1e);
                break;
            case TokayDimitriStage.TradeSecondText:
                Show(0x0a24);
                break;
            case TokayDimitriStage.TradeThirdText:
                Show(0x0a25);
                break;
            default:
                throw new InvalidOperationException(
                    $"Tokay Dimitri wait entered unsupported stage {stage}.");
        }
    }

    private void CompleteTrade()
    {
        NpcCharacter? second = FindActor(0x10);
        _actor?.SetStatePosition(_actor.Position + Vector2.Left * 16);
        second?.SetStatePosition(second.Position + Vector2.Left * 32);
        _actor?.SetActive(false);
        second?.SetActive(false);
        OracleSaveData save = _context.Rooms.SaveData;
        byte state = save.ReadWramByte(_database.DimitriStateAddress);
        if (save.WriteWramByte(_database.DimitriStateAddress, (byte)(state | 0x02)))
            save.CommitInventoryChange();
        FinishInteraction();
    }

    private void BeginWait(int frames, TokayDimitriStage next)
    {
        _counter = frames;
        _nextStage = next;
        _stage = TokayDimitriStage.Wait;
    }

    private NpcCharacter? FindActor(int subId) =>
        _context.Entities.Entities<NpcCharacter>()
            .FirstOrDefault(npc => npc.Record.Id == 0x48 && npc.Record.SubId == subId);

    private void FaceActorToLink(NpcCharacter actor)
    {
        Vector2 delta = _context.Player.Position - actor.Position;
        int animation = Mathf.Abs(delta.X) > Mathf.Abs(delta.Y)
            ? (delta.X >= 0 ? 1 : 3)
            : (delta.Y >= 0 ? 2 : 0);
        actor.SetScriptAnimation(_database.Animation(animation));
    }

    private void Show(int textId) =>
        _context.ShowDialogue(_database.Text(textId));

    private void ShowChoice(int textId) =>
        _context.ShowChoiceDialogue(_database.Text(textId));

    private int TakeChoice()
    {
        if (!_context.TryTakeDialogueChoice(out int choice))
            throw new InvalidOperationException(
                "tokayWithDimitri1Script prompt closed without a text-option result.");
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
        _actor = null;
        _stage = TokayDimitriStage.Inactive;
    }
}

internal enum TokayDimitriStage
{
    Inactive,
    DialogueOnly,
    Wait,
    IntroFirstText,
    IntroSecondText,
    TradeIntro,
    TradePrompt,
    TradeAccepted,
    TradeSecondText,
    TradeThirdText
}
