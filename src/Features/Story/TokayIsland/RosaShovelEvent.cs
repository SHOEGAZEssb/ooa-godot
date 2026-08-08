using Godot;
using System;

namespace oracleofages;

/// <summary>
/// rosa_subid00Script for INTERAC_ROSA $68:$00 in linked room $1:$cb.
/// </summary>
internal sealed class RosaShovelEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayIslandDatabase _database;
    private RosaShovelStage _stage;
    private RosaShovelStage _nextStage;
    private NpcCharacter? _actor;
    private GroundTreasurePickup? _reward;
    private int _counter;
    private bool _inputLocked;

    internal RosaShovelEvent(
        RoomEventContext context,
        TokayIslandDatabase database)
    {
        _context = context;
        _database = database;
    }

    public bool HasState => _stage != RosaShovelStage.Inactive;
    public bool BlocksGameplay => _inputLocked;
    internal RosaShovelStage Stage => _stage;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active ||
            npc.Record is not { Group: 1, Room: 0xcb, Id: 0x68, SubId: 0x00 })
        {
            return false;
        }

        _actor = npc;
        if (_context.Rooms.SaveData.HasRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40))
        {
            Show(0x1c12);
            _stage = RosaShovelStage.DialogueOnly;
            return true;
        }

        LockInput();
        FaceActorToLink(npc);
        Show(0x1c10);
        _stage = RosaShovelStage.FirstText;
        return true;
    }

    public void UpdateFrame()
    {
        if (_stage == RosaShovelStage.Inactive)
            return;
        if (_stage == RosaShovelStage.MoveRight)
        {
            NpcCharacter actor = _actor ??
                throw new InvalidOperationException("rosa_subid00Script lost Rosa.");
            actor.SetStatePosition(actor.Position + Vector2.Right);
            if (--_counter == 0)
            {
                _counter = 70;
                _nextStage = RosaShovelStage.SecondText;
                _stage = RosaShovelStage.SecondTextWait;
            }
            return;
        }
        if (_stage is RosaShovelStage.MoveWait or RosaShovelStage.SecondTextWait or
            RosaShovelStage.GiveWait)
        {
            if (--_counter == 0)
                EnterStage(_nextStage);
            return;
        }
        if (_stage == RosaShovelStage.Reward)
        {
            if (_reward is { Finished: true })
            {
                _reward = null;
                _context.Rooms.SaveData.SetRoomFlag(
                    1, 0xcb, OracleSaveData.RoomFlag40);
                Show(0x1c12);
                _stage = RosaShovelStage.FinalText;
            }
            return;
        }
        if (_context.DialogueOpen)
            return;

        switch (_stage)
        {
            case RosaShovelStage.DialogueOnly:
            case RosaShovelStage.FinalText:
                FinishInteraction();
                break;
            case RosaShovelStage.FirstText:
                _counter = 30;
                _nextStage = RosaShovelStage.MoveRight;
                _stage = RosaShovelStage.MoveWait;
                break;
            case RosaShovelStage.SecondText:
                _counter = 30;
                _nextStage = RosaShovelStage.Give;
                _stage = RosaShovelStage.GiveWait;
                break;
            default:
                throw new InvalidOperationException(
                    $"Rosa shovel stage {_stage} closed an unexpected dialogue.");
        }
    }

    public void Cancel()
    {
        _reward?.Finish(_context.Player);
        _reward = null;
        UnlockInput();
        _actor = null;
        _counter = 0;
        _stage = RosaShovelStage.Inactive;
    }

    private void EnterStage(RosaShovelStage stage)
    {
        _stage = stage;
        switch (stage)
        {
            case RosaShovelStage.MoveRight:
                _counter = 48;
                break;
            case RosaShovelStage.SecondText:
                Show(0x1c11);
                break;
            case RosaShovelStage.Give:
                _reward = _context.GrantScriptTreasure(
                    _context.Rooms.ActiveGroup,
                    _context.Rooms.CurrentRoom.Id,
                    TreasureDatabase.TreasureShovel,
                    1,
                    "TREASURE_OBJECT_SHOVEL_01",
                    "scripts/ages:rosa_subid00Script",
                    objectParameter: 0);
                _stage = RosaShovelStage.Reward;
                break;
            default:
                throw new InvalidOperationException(
                    $"Rosa shovel wait entered unsupported stage {stage}.");
        }
    }

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
        _stage = RosaShovelStage.Inactive;
    }
}

internal enum RosaShovelStage
{
    Inactive,
    DialogueOnly,
    FirstText,
    MoveWait,
    MoveRight,
    SecondTextWait,
    SecondText,
    GiveWait,
    Give,
    Reward,
    FinalText
}
