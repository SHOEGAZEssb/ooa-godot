using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_LEVER $61, shared upward/downward bracelet lever.</summary>
internal sealed partial class LeverRoomEntity : NpcCharacter,
    IRoomEntity, IFixedRoomEntity, IRoomBlocker,
    IBraceletPullInteractableRoomEntity
{
    private readonly LeverState _state;
    private readonly LeverBehavior _constants;
    private readonly Action<int> _playSound;
    private readonly int _baseY;
    private readonly int _sign;
    private Vector2 _precisePosition;
    private bool _grabbed;
    private bool _pullRequested;
    private bool _movedSincePause;
    private bool _releasedThisUpdate;

    public Node2D Node => this;
    internal bool Grabbed => _grabbed;
    internal int PullDistance => _state.PullDistance;
    internal int BaseY => _baseY;
    internal int DirectionSign => _sign;

    internal LeverRoomEntity(
        NpcRecord record,
        LeverState state,
        LeverBehavior constants,
        Action<int> playSound)
    {
        _state = state;
        _constants = constants;
        _playSound = playSound;
        _baseY = record.Y;
        _sign = (record.SubId & 1) == 0 ? 1 : -1;
        _precisePosition = new Vector2(record.X, record.Y);
        Name = "Lever";
        ZIndex = BehindLinkZIndex;
        Initialize(record);
        SetCollisionRadii(constants.LeverRadiusY, constants.LeverRadiusX);
    }

    public bool TryBeginBraceletPull(Player player)
    {
        if (_grabbed || player.IsCarryingObject || player.CutsceneControlled ||
            player.FacingVector != (_sign > 0 ? Vector2I.Up : Vector2I.Down))
        {
            return false;
        }

        int angle = OracleObjectMovement.Shared.RelativeAngle(
            Position, player.Position);
        int direction = ((angle + 0x14) & ObjectAngle.CardinalMask) switch
        {
            0x00 => 0,
            0x08 => 1,
            0x10 => 2,
            _ => 3
        };
        if (direction != (_sign > 0 ? 0 : 2))
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
        _precisePosition = new Vector2(Position.X, Position.Y);
        player.SetScriptedPosition(new Vector2(
            Position.X + player.PrecisePosition.X - Mathf.Floor(player.PrecisePosition.X),
            Position.Y + _constants.LinkYOffset));
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

        _pullRequested = movementInput.Dot(Vector2.Down * _sign) > 0.5f;
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
            (Mathf.FloorToInt(Position.Y) - _baseY) * _sign;
        if (currentOffset >= _constants.LeverLength)
            return;

        int oldDistance = _state.PullDistance;
        player.AdvanceInteractionVelocity(_constants.PullSpeed, _sign > 0 ? ObjectAngle.Down : ObjectAngle.Up);
        Vector2 linkPrecise = player.PrecisePosition;

        int leverY = Mathf.FloorToInt(linkPrecise.Y) -
            _constants.LinkYOffset;
        leverY = _sign > 0 ? Math.Min(_baseY + _constants.LeverLength, leverY)
            : Math.Max(_baseY - _constants.LeverLength, leverY);
        float fraction = _precisePosition.Y -
            Mathf.Floor(_precisePosition.Y);
        _precisePosition = new Vector2(Position.X, leverY + fraction);
        SetStatePosition(new Vector2(Position.X, leverY));

        int newDistance = (leverY - _baseY) * _sign;
        WritePullOffset(newDistance);
        bool fullyPulled = (_state.PullDistance & 0x80) != 0;

        if (_state.PullDistance == oldDistance)
            return;
        if (!_movedSincePause && !fullyPulled)
            _playSound(_constants.MoveSound);
        _movedSincePause = true;
    }

    private void Retract()
    {
        SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, _constants.PullSpeed, _sign > 0 ? ObjectAngle.Up : ObjectAngle.Down));
        int y = Mathf.FloorToInt(Position.Y);
        // The native helper runs before the retraction cap and also sets bit
        // 7/plays OPENCHEST while an upward lever still has its full YHIGH
        // distance during the first three fractional retraction updates.
        WritePullOffset(Math.Abs(y - _baseY));
        if ((y - _baseY) * _sign <= 0)
        {
            float fraction = _precisePosition.Y -
                Mathf.Floor(_precisePosition.Y);
            _precisePosition = new Vector2(Position.X, _baseY + fraction);
            SetStatePosition(new Vector2(Position.X, _baseY));
            return;
        }
    }

    private void WritePullOffset(int distance)
    {
        if (distance == _constants.LeverLength)
        {
            _playSound(_constants.FullSound);
            distance |= 0x80;
        }
        _state.PullDistance = distance;
    }
}
