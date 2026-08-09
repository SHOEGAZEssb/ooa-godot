using Godot;
using System;
using System.Linq;

namespace oracleofages;

/// <summary>
/// tokayRunningFromRosaScript for INTERAC_TOKAY $48:$0b.
/// </summary>
internal sealed class TokayRunningFromRosaEvent : IRoomEntryEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayInteractionDatabase _database;
    private TokayRunningFromRosaStage _stage;
    private TokayRunningFromRosaStage _nextStage;
    private NpcCharacter? _actor;
    private Vector2 _moveDirection;
    private int _counter;
    private bool _inputLocked;

    internal TokayRunningFromRosaEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
    {
        _context = context;
        _database = database;
    }

    public bool HasState => _stage != TokayRunningFromRosaStage.Inactive;
    public bool BlocksGameplay => _inputLocked;
    internal TokayRunningFromRosaStage Stage => _stage;

    public bool Matches(int group, OracleRoomData room) =>
        group == 1 && room.Id == 0xbb && FindActor() is { Active: true };

    public void Start(OracleRoomData room)
    {
        if (!Matches(_context.Rooms.ActiveGroup, room))
            throw new InvalidOperationException(
                $"tokayRunningFromRosaScript cannot start in " +
                $"{_context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        _actor = FindActor() ?? throw new InvalidOperationException(
            "tokayRunningFromRosaScript lost INTERAC_TOKAY $48:$0b on room entry.");
        _stage = TokayRunningFromRosaStage.LinkTriggerWait;
    }

    public void UpdateFrame()
    {
        if (_stage == TokayRunningFromRosaStage.Inactive)
            return;
        if (_stage == TokayRunningFromRosaStage.LinkTriggerWait)
        {
            if (Mathf.FloorToInt(_context.Player.Position.Y) == 0x50)
            {
                LockInput();
                BeginWait(30, TokayRunningFromRosaStage.FirstText);
            }
            return;
        }
        if (_stage == TokayRunningFromRosaStage.ActorMove)
        {
            NpcCharacter actor = _actor ?? throw new InvalidOperationException(
                "tokayRunningFromRosaScript lost its actor while moving.");
            actor.SetStatePosition(actor.Position + _moveDirection);
            if (--_counter == 0)
                EnterStage(_nextStage);
            return;
        }
        if (_stage == TokayRunningFromRosaStage.Wait)
        {
            if (--_counter == 0)
                EnterStage(_nextStage);
            return;
        }
        if (_context.DialogueOpen)
            return;

        switch (_stage)
        {
            case TokayRunningFromRosaStage.FirstText:
                BeginActorMove(Vector2.Up, 17, TokayRunningFromRosaStage.FirstPause);
                break;
            case TokayRunningFromRosaStage.SecondText:
                BeginWait(30, TokayRunningFromRosaStage.SecondMoveUp);
                break;
            default:
                throw new InvalidOperationException(
                    $"Rosa-escape stage {_stage} closed an unexpected dialogue.");
        }
    }

    public void Cancel()
    {
        UnlockInput();
        _actor = null;
        _counter = 0;
        _stage = TokayRunningFromRosaStage.Inactive;
    }

    private void EnterStage(TokayRunningFromRosaStage stage)
    {
        _stage = stage;
        switch (stage)
        {
            case TokayRunningFromRosaStage.FirstText:
                Show(0x0a0e);
                break;
            case TokayRunningFromRosaStage.FirstPause:
                BeginWait(80, TokayRunningFromRosaStage.Jump);
                break;
            case TokayRunningFromRosaStage.Jump:
                _context.Sound.PlaySound(_database.SoundJump);
                BeginWait(50, TokayRunningFromRosaStage.SecondText);
                break;
            case TokayRunningFromRosaStage.SecondText:
                Show(0x0a0f);
                break;
            case TokayRunningFromRosaStage.SecondMoveUp:
                BeginActorMove(Vector2.Up, 57, TokayRunningFromRosaStage.SecondPause);
                break;
            case TokayRunningFromRosaStage.SecondPause:
                BeginWait(6, TokayRunningFromRosaStage.MoveLeft);
                break;
            case TokayRunningFromRosaStage.MoveLeft:
                BeginActorMove(Vector2.Left, 43, TokayRunningFromRosaStage.Done);
                break;
            case TokayRunningFromRosaStage.Done:
                _context.Rooms.SaveData.SetRoomFlag(
                    _context.Rooms.ActiveGroup,
                    _context.Rooms.CurrentRoom.Id,
                    OracleSaveData.RoomFlag80);
                _actor?.SetActive(false);
                FinishInteraction();
                break;
            default:
                throw new InvalidOperationException(
                    $"Rosa-escape wait entered unsupported stage {stage}.");
        }
    }

    private void BeginActorMove(
        Vector2 direction,
        int pixels,
        TokayRunningFromRosaStage next)
    {
        _moveDirection = direction;
        _counter = pixels;
        _nextStage = next;
        _stage = TokayRunningFromRosaStage.ActorMove;
    }

    private void BeginWait(int frames, TokayRunningFromRosaStage next)
    {
        _counter = frames;
        _nextStage = next;
        _stage = TokayRunningFromRosaStage.Wait;
    }

    private NpcCharacter? FindActor() =>
        _context.Entities.Entities<NpcCharacter>()
            .FirstOrDefault(npc => npc.Record is { Id: 0x48, SubId: 0x0b });

    private void Show(int textId) =>
        _context.ShowDialogue(_database.Text(textId));

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
        _stage = TokayRunningFromRosaStage.Inactive;
    }
}

internal enum TokayRunningFromRosaStage
{
    Inactive,
    LinkTriggerWait,
    Wait,
    ActorMove,
    FirstText,
    FirstPause,
    Jump,
    SecondText,
    SecondMoveUp,
    SecondPause,
    MoveLeft,
    Done
}
