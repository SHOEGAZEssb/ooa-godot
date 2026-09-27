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
    private PostmanScriptHost? _script;

    internal bool Leaving => _leaving;
    internal bool Initialized { get; private set; }

    internal void BindScript(PostmanScriptHost script)
    {
        _script = script;
        script.Bind(this);
    }

    internal void InitializePostman(NpcRecord record)
    {
        if (record is not
            {
                Group: 2,
                Room: 0x2f,
                Id: InteractionId.Postman,
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
        SetCollisionRadii(0, 0);
        SetScriptButtonSensitive(false);
        _precisePosition = Position;
        _leaving = false;
        Initialized = false;
    }

    internal void SetLeaving()
    {
        _leaving = true;
    }

    internal void SetMovementAnimation(
        int angle,
        string encodedAnimation)
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
    }

    internal void MoveAtSpeed(int speed, int angle)
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
    }

    internal void UpdatePostman(Player player)
    {
        PostmanScriptHost script = _script ?? throw new InvalidOperationException(
            "Room 2:2f INTERAC_POSTMAN $55:$00 has no script owner.");
        Initialized = true;
        // postman.s state0 falls through into state1. Script and native tail
        // share this single eligible object visit, including text-opening work.
        // The reward flag can hide the actor before the runner finishes.
        script.Advance(player);
        if (!script.HasState || !Active)
            return;
        if (!_leaving)
        {
            FaceLinkAndAnimateOneUpdate(player);
            return;
        }

        // bank0.interactionAnimateBasedOnSpeed: one call even on counter2's
        // zero update, with two extra calls for nonzero SPEED_200 movement.
        AdvanceAnimationUpdates(script.MovementCounter != 0 ? 3 : 1);
        UpdateDrawPriority(player.Position);
    }
}
