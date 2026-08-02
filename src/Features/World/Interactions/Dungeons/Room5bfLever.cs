using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_LEVER $61:$30 in room 5:bf.</summary>
internal sealed partial class Room5bfLever : NpcCharacter,
    IRoomEntity, IFixedRoomEntity, IRoomBlocker,
    IBraceletPullInteractableRoomEntity
{
    private readonly Room5bfLeverState _state;
    private readonly Room5bfConstants _constants;
    private readonly Action<int> _playSound;
    private readonly int _baseY;
    private Vector2 _precisePosition;
    private bool _grabbed;
    private bool _pullRequested;
    private bool _movedSincePause;
    private bool _releasedThisUpdate;

    public Node2D Node => this;
    internal bool Grabbed => _grabbed;
    internal int PullDistance => _state.PullDistance;
    internal int BaseY => _baseY;

    internal Room5bfLever(
        Room5bfInteractionRecord record,
        Room5bfLeverState state,
        Room5bfConstants constants,
        Action<int> playSound)
    {
        if (record.Kind != Room5bfInteractionKind.Lever ||
            record.SubId != 0x30)
        {
            throw new ArgumentException(
                $"Room 5:bf lever has invalid source record " +
                $"${record.Id:x2}:${record.SubId:x2}.", nameof(record));
        }

        _state = state;
        _constants = constants;
        _playSound = playSound;
        _baseY = record.Y;
        _precisePosition = new Vector2(record.X, record.Y);
        Name = "Room5bfLever";
        ZIndex = BehindLinkZIndex;
        Initialize(record.ToNpcRecord());
        SetCollisionRadii(constants.LeverRadiusY, constants.LeverRadiusX);
        SetScriptAnimation(record.Animations[0]);
    }

    public bool TryBeginBraceletPull(Player player)
    {
        if (_grabbed || player.IsCarryingObject || player.CutsceneControlled ||
            player.FacingVector != Vector2I.Up)
        {
            return false;
        }

        int angle = OracleObjectMovement.Shared.RelativeAngle(
            Position, player.Position);
        int direction = ((angle + 0x14) & 0x18) switch
        {
            0x00 => 0,
            0x08 => 1,
            0x10 => 2,
            _ => 3
        };
        if (direction != 0)
            return false;

        Vector2 point = player.Position +
            (Vector2)player.FacingVector * 6.0f;
        Vector2 delta = Position - point;
        if (Mathf.Abs(delta.Y) >=
                _constants.LeverRadiusY + NpcCharacter.LinkCollisionRadius ||
            Mathf.Abs(delta.X) >=
                _constants.LeverRadiusX + NpcCharacter.LinkCollisionRadius)
        {
            return false;
        }

        _grabbed = true;
        _pullRequested = false;
        _movedSincePause = false;
        _releasedThisUpdate = false;
        player.SetScriptedPosition(new Vector2(
            Position.X, Position.Y + _constants.LinkYOffset));
        player.SetBraceletActionPose(BraceletActionPose.Pull);
        return true;
    }

    public bool UpdateBraceletPull(
        Player player,
        Vector2 movementInput,
        bool assignedButtonHeld)
    {
        if (!_grabbed)
            return false;
        if (!assignedButtonHeld)
        {
            CancelBraceletPull(player);
            return false;
        }

        _pullRequested = movementInput.Dot(Vector2.Down) > 0.5f;
        player.SetBraceletActionPose(_pullRequested
            ? BraceletActionPose.PullStrain
            : BraceletActionPose.Pull);
        return true;
    }

    public void CancelBraceletPull(Player player)
    {
        if (!_grabbed)
            return;
        _grabbed = false;
        _pullRequested = false;
        _movedSincePause = false;
        _releasedThisUpdate = true;
        player.ClearBraceletActionPose();
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        if (_grabbed)
        {
            if (_pullRequested)
                Pull(frame.Player);
            else
                _movedSincePause = false;
            _pullRequested = false;
            UpdateDrawPriority(frame.Player.Position);
            return;
        }

        if (_releasedThisUpdate)
        {
            _releasedThisUpdate = false;
            UpdateDrawPriority(frame.Player.Position);
            return;
        }

        if ((_state.PullDistance & 0x7f) != 0)
            Retract();

        PreventPlayerPassing(frame.Player);
        UpdateDrawPriority(frame.Player.Position);
    }

    public bool BlocksLink(Vector2 linkCenter) => BlocksLinkCenter(linkCenter);

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    private void Pull(Player player)
    {
        int currentOffset =
            Mathf.FloorToInt(Position.Y) - _baseY;
        if (currentOffset >= _constants.LeverLength)
            return;

        int oldDistance = _state.PullDistance & 0x7f;
        Vector2 linkPrecise = player.PrecisePosition;
        OracleObjectMovement.Shared.ApplySpeed(
            ref linkPrecise, _constants.PullSpeed, 0x10);
        player.SetScriptedPosition(linkPrecise);

        int leverY = Mathf.FloorToInt(linkPrecise.Y) -
            _constants.LinkYOffset;
        leverY = Math.Min(_baseY + _constants.LeverLength, leverY);
        float fraction = _precisePosition.Y -
            Mathf.Floor(_precisePosition.Y);
        _precisePosition = new Vector2(Position.X, leverY + fraction);
        SetStatePosition(new Vector2(Position.X, leverY));

        int newDistance = leverY - _baseY;
        bool fullyPulled = newDistance == _constants.LeverLength;
        _state.PullDistance = fullyPulled
            ? newDistance | 0x80
            : newDistance;
        if (fullyPulled)
            _playSound(_constants.FullSound);

        if (newDistance == oldDistance)
            return;
        if (!_movedSincePause && !fullyPulled)
            _playSound(_constants.MoveSound);
        _movedSincePause = true;
    }

    private void Retract()
    {
        SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, _constants.PullSpeed, 0x00));
        int y = Mathf.FloorToInt(Position.Y);
        if (y <= _baseY)
        {
            float fraction = _precisePosition.Y -
                Mathf.Floor(_precisePosition.Y);
            _precisePosition = new Vector2(Position.X, _baseY + fraction);
            SetStatePosition(new Vector2(Position.X, _baseY));
            _state.PullDistance = 0;
            return;
        }
        _state.PullDistance = y - _baseY;
    }
}
