using Godot;
using System;
using System.Linq;

namespace oracleofages;

/// <summary>
/// tokayRunningFromRosaScript for INTERAC_TOKAY $48:$0b.
/// </summary>
internal sealed class TokayRunningFromRosaEvent : TokayScriptEvent, IRoomEntryEvent
{
    private TokayRunningFromRosaStage _stage;
    private TokayRunningFromRosaStage _nextStage;
    private NpcCharacter? _actor;
    private Vector2 _moveDirection;
    private int _counter;
    private readonly TokayNativeDatabase _native = new();
    private Vector2 _position;
    private int _z;
    private int _speedZ;

    internal TokayRunningFromRosaEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
        : base(context, database)
    {
    }

    public bool MenusDisabled => HasState;
    public bool HasState => _stage != TokayRunningFromRosaStage.Inactive;
    internal TokayRunningFromRosaStage Stage => _stage;

    public bool Matches(int group, OracleRoomData room) =>
        group == 1 && room.Id == 0xbb && FindActor() is { Active: true };

    public void Start(OracleRoomData room)
    {
        if (!Matches(Context.Rooms.ActiveGroup, room))
            throw new InvalidOperationException(
                $"tokayRunningFromRosaScript cannot start in " +
                $"{Context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        _actor = FindActor() ?? throw new InvalidOperationException(
            "tokayRunningFromRosaScript lost INTERAC_TOKAY $48:$0b on room entry.");
        ((TokayCharacter)_actor).ScriptOwnsNativeUpdate = true;
        _position = _actor.Position;
        _stage = TokayRunningFromRosaStage.LinkTriggerWait;
    }

    public void UpdateFrame()
    {
        UpdateScript();
        if (_actor is { } actor)
        {
            actor.AdvanceAnimationUpdates(_stage == TokayRunningFromRosaStage.ActorMove && _counter != 0 ? 2 : 1);
            actor.SetScriptDrawOffset(new Vector2(_position.X >= 0xf0 ? -256 : 0, _z >> 8));
        }
    }

    private void UpdateScript()
    {
        if (_stage == TokayRunningFromRosaStage.Inactive)
            return;
        if (_stage == TokayRunningFromRosaStage.LinkTriggerWait)
        {
            if (Mathf.FloorToInt(Context.Player.Position.Y) == 0x50)
            {
                EventResources.LockInput();
                BeginWait(30, TokayRunningFromRosaStage.FirstText);
            }
            return;
        }
        if (_stage == TokayRunningFromRosaStage.ActorMove)
        {
            NpcCharacter actor = _actor ?? throw new InvalidOperationException(
                "tokayRunningFromRosaScript lost its actor while moving.");
            if (_counter == 0)
                EnterStage(_nextStage);
            else if (--_counter != 0)
                actor.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(ref _position,
                    _native.Constant("speed-180"), _moveDirection == Vector2.Up ? 0 : 0x18));
            return;
        }
        if (_stage == TokayRunningFromRosaStage.Jumping)
        {
            if (OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x20))
                BeginWait(20, TokayRunningFromRosaStage.SecondText);
            return;
        }
        if (_stage == TokayRunningFromRosaStage.Wait)
        {
            if (--_counter == 0)
                EnterStage(_nextStage);
            return;
        }
        if (Context.DialogueOpen)
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
        if (_actor is TokayCharacter actor && GodotObject.IsInstanceValid(actor))
        {
            actor.ScriptOwnsNativeUpdate = false;
            actor.SetScriptDrawOffset(Vector2.Zero);
        }
        EventResources.UnlockInput();
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
                BeginWait(30, TokayRunningFromRosaStage.TurnDown);
                break;
            case TokayRunningFromRosaStage.TurnDown:
                _actor!.SetFacingDirection(Vector2I.Down);
                BeginWait(30, TokayRunningFromRosaStage.Jump);
                break;
            case TokayRunningFromRosaStage.Jump:
                _speedZ = -0x1c0;
                _z = 0;
                Context.Sound.PlaySound(Interactions.SoundJump);
                _stage = TokayRunningFromRosaStage.Jumping;
                // setzspeed and the first asm15 objectUpdateSpeedZ share this pass.
                OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x20);
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
                Context.Rooms.SaveData.SetRoomFlag(
                    Context.Rooms.ActiveGroup,
                    Context.Rooms.CurrentRoom.Id,
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
        _actor!.SetFacingDirection(direction == Vector2.Up ? Vector2I.Up : Vector2I.Left);
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
        Context.Entities.Entities<NpcCharacter>()
            .FirstOrDefault(npc => npc.Record is { Id: 0x48, SubId: 0x0b });

    private void FinishInteraction()
    {
        EventResources.UnlockInput();
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
    TurnDown,
    Jump,
    Jumping,
    SecondText,
    SecondMoveUp,
    SecondPause,
    MoveLeft,
    Done
}
