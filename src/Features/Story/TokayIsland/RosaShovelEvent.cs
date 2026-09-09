using Godot;
using System;
using System.Linq;

namespace oracleofages;

/// <summary>
/// rosa_subid00Script for INTERAC_ROSA $68:$00 in linked room $1:$cb.
/// </summary>
internal sealed class RosaShovelEvent : TokayScriptEvent, IRoomEvent
{
    private RosaShovelStage _stage;
    private RosaShovelStage _nextStage;
    private NpcCharacter? _actor;
    private GroundTreasurePickup? _reward;
    private int _counter;
    private readonly TokayNativeDatabase _native = new();
    private Vector2 _position;
    private TokayAttachedVisualRoomEntity? _shovel;
    private RosaNpcRoomEntity? _nativeActor;

    internal RosaShovelEvent(
        RoomEventContext context,
        TokayInteractionDatabase database)
        : base(context, database)
    {
    }

    public bool MenusDisabled => HasState;
    public bool HasState => _stage != RosaShovelStage.Inactive;
    internal RosaShovelStage Stage => _stage;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (HasState || !npc.Active ||
            npc.Record is not { Group: 1, Room: 0xcb, Id: 0x68, SubId: 0x00 })
        {
            return false;
        }

        _actor = npc;
        _nativeActor = Context.Entities.EntityAdapters<RosaNpcRoomEntity>().Single();
        _nativeActor.ScriptOwnsNativeUpdate = true;
        _shovel = Context.Entities.Entities<TokayAttachedVisualRoomEntity>().FirstOrDefault();
        if (Context.Rooms.SaveData.HasRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40))
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
        var native = _nativeActor;
        UpdateScript();
        native?.RunNativeUpdate(Context.Player);
    }

    private void UpdateScript()
    {
        if (_stage == RosaShovelStage.Inactive)
            return;
        if (_stage == RosaShovelStage.MoveRight)
        {
            NpcCharacter actor = _actor ??
                throw new InvalidOperationException("rosa_subid00Script lost Rosa.");
            if (_counter == 0)
            {
                _counter = 20;
                _nextStage = RosaShovelStage.ShiftShovelRight;
                _stage = RosaShovelStage.SecondTextWait;
            }
            else if (--_counter != 0)
                actor.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(
                    ref _position, _native.Constant("speed-020"), 0x08));
            return;
        }
        if (_stage is RosaShovelStage.MoveWait or RosaShovelStage.SecondTextWait or
            RosaShovelStage.GiveWait or RosaShovelStage.FinalWait)
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
                _counter = 30;
                _nextStage = RosaShovelStage.Done;
                _stage = RosaShovelStage.FinalWait;
            }
            return;
        }
        if (Context.DialogueOpen)
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
        if (_nativeActor is not null) _nativeActor.ScriptOwnsNativeUpdate = false;
        _nativeActor = null;
        _reward?.Finish(Context.Player);
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
                _position = _actor!.Position;
                _actor.SetFacingDirection(Vector2I.Right);
                break;
            case RosaShovelStage.ShiftShovelRight:
                if (_shovel is { } right)
                {
                    right.FollowParent = true;
                    right.ParentOffset = new Vector2(9, 0);
                }
                _counter = 20;
                _nextStage = RosaShovelStage.ShiftShovelLeft;
                _stage = RosaShovelStage.SecondTextWait;
                break;
            case RosaShovelStage.ShiftShovelLeft:
                if (_shovel is { } left)
                {
                    left.ParentOffset = new Vector2(-9, 0);
                    left.ZIndex = NpcCharacter.FixedLowPriorityZIndex;
                }
                _actor!.SetFacingDirection(Vector2I.Left);
                _counter = 30;
                _nextStage = RosaShovelStage.SecondText;
                _stage = RosaShovelStage.SecondTextWait;
                break;
            case RosaShovelStage.Done:
                Context.Rooms.SaveData.SetRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40);
                FinishInteraction();
                break;
            case RosaShovelStage.SecondText:
                Show(0x1c11);
                break;
            case RosaShovelStage.Give:
                if (_shovel is { } shovel) shovel.Retired = true;
                _reward = Context.GrantScriptTreasure(
                    Context.Rooms.ActiveGroup,
                    Context.Rooms.CurrentRoom.Id,
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

    private void FaceActorToLink(NpcCharacter actor) =>
        actor.SetFacingDirection(DirectionToward(actor.Position, Context.Player.Position));

    private void FinishInteraction()
    {
        if (_nativeActor is not null) _nativeActor.ScriptOwnsNativeUpdate = false;
        _nativeActor = null;
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
    ShiftShovelRight,
    ShiftShovelLeft,
    SecondText,
    GiveWait,
    Give,
    Reward,
    FinalText,
    FinalWait,
    Done
}
