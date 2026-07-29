using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Native state carried by room 2:2f's INTERAC_POSTMAN $55:$00.
/// </summary>
internal sealed partial class PostmanCharacter : NpcCharacter
{
    internal const int Speed200 = 0x50;
    internal const int RightAngle = 0x08;
    internal const int DownAngle = 0x10;

    private Vector2 _precisePosition;
    private bool _leaving;
    private bool _movementCounterActive;

    internal bool Leaving => _leaving;
    internal bool MovementCounterActive => _movementCounterActive;
    internal Vector2 PrecisePosition => _precisePosition;

    internal void InitializePostman(NpcRecord record)
    {
        if (record is not
            {
                Group: 2,
                Room: 0x2f,
                Id: 0x55,
                SubId: 0x00,
                Var03: 0x00
            })
        {
            throw new InvalidOperationException(
                "PostmanCharacter requires room 2:2f INTERAC_POSTMAN " +
                "$55:$00 var03=$00.");
        }

        Initialize(record);
        ResetNativeNpcFacingState();
        SetScriptButtonSensitive(true);
        _precisePosition = Position;
        _leaving = false;
        _movementCounterActive = false;
    }

    internal void SetLeaving()
    {
        _leaving = true;
    }

    internal void SetMovementAnimation(
        int angle,
        string encodedAnimation,
        Player player)
    {
        string expected = angle switch
        {
            RightAngle => Record.RightAnimation,
            DownAngle => Record.DownAnimation,
            _ => throw new InvalidOperationException(
                $"postmanScript cannot select movement angle ${angle:x2}.")
        };
        if (!string.Equals(
            encodedAnimation, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Postman movement angle ${angle:x2} diverges from its " +
                "imported animation.");
        }

        SetScriptAnimation(encodedAnimation);
        _precisePosition = Position;
        _movementCounterActive = true;

        // interactionRunScript selects the animation before the native
        // interactionAnimateBasedOnSpeed tail. The room-entity pass has
        // already occurred in this runtime update, so apply those three
        // SPEED_200 animation calls here to preserve the source boundary.
        AdvanceAnimationUpdates(3);
        UpdateDrawPriority(player.Position);
    }

    internal void MoveAtSpeed(int speed, int angle, Player player)
    {
        if (speed != Speed200 ||
            angle is not (RightAngle or DownAngle))
        {
            throw new InvalidOperationException(
                $"postmanScript requested unexpected movement " +
                $"${speed:x2}/${angle:x2}.");
        }

        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, speed, angle);
        UpdateDrawPriority(player.Position);
    }

    internal void CompleteMovement()
    {
        _movementCounterActive = false;
    }

    internal void UpdatePostman(Player player)
    {
        if (!Active)
            return;
        if (!_leaving)
        {
            FaceLinkAndAnimateOneUpdate(player);
            return;
        }

        if (_movementCounterActive)
        {
            AdvanceAnimationUpdates(3);
            UpdateDrawPriority(player.Position);
            return;
        }

        AnimateAndUpdateDrawPriorityOneUpdate(player);
    }
}
