using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Resources shared by native events and room command hosts. This owner
/// owns no update loop, command capabilities, counters, or completion policy.
/// </summary>
internal sealed class RoomEventResources(RoomEventContext context, object owner)
{
    public bool InputLocked => context.Player.IsCutsceneControlOwner(owner);
    private bool _ownsFullScreenFade;
    private Vector2 _fadePosition;
    private Vector2 _fadeSize;
    private int? _fadeZIndex;
    private Color _fadeColor;

    public void LockInput(bool onlyIfUnlocked = false, bool interruptBracelet = true)
    {
        if (onlyIfUnlocked && InputLocked)
            return;
        context.Player.BeginCutsceneControl(interruptBracelet, owner);
    }

    public void UnlockInput()
    {
        context.Player.EndCutsceneControl(owner);
    }

    public static Vector2I DirectionToward(Vector2 origin, Vector2 target)
    {
        int angle = (OracleObjectMovement.Shared.RelativeAngle(origin, target) + 4) & 0x18;
        return angle switch
        {
            0 => Vector2I.Up,
            8 => Vector2I.Right,
            16 => Vector2I.Down,
            _ => Vector2I.Left
        };
    }

    // INTERAC_EXCLAMATION_MARK $9f state 0 reveals without decrementing or
    // animating. Its finite state-1 lifetime advances at the owner's slot.
    public static void UpdateExclamation(ref NpcCharacter? actor, ref bool fresh, ref int counter)
    {
        if (actor is null)
            return;
        if (fresh)
        {
            fresh = false;
            return;
        }
        if (counter <= 1)
        {
            RetireExclamation(ref actor, ref fresh, ref counter);
            return;
        }
        counter--;
        actor.AdvanceAnimationUpdates(1);
    }

    public static void RetireExclamation(ref NpcCharacter? actor, ref bool fresh, ref int counter)
    {
        if (actor is not null &&
            GodotObject.IsInstanceValid(actor))
        {
            actor.SetActive(false);
        }
        actor = null;
        counter = 0;
        fresh = false;
    }

    public int RequireDialogueChoice(string errorMessage)
    {
        if (!context.TryTakeDialogueChoice(out int choice))
            throw new InvalidOperationException(errorMessage);
        return choice;
    }

    // Capture once: repeated fade phases must not overwrite the room's
    // presentation with the already-expanded full-screen rectangle.
    public bool CaptureFullScreenFade(int? zIndex = null)
    {
        if (_ownsFullScreenFade)
            return false;
        _ownsFullScreenFade = true;
        ColorRect fade = context.Fade;
        _fadePosition = fade.Position;
        _fadeSize = fade.Size;
        _fadeZIndex = zIndex.HasValue ? fade.ZIndex : null;
        _fadeColor = fade.Color;
        fade.Position = Vector2.Zero;
        fade.Size = new Vector2(OracleRoomData.ViewportWidth, OracleRoomData.ScreenHeight);
        if (zIndex is int layer)
            fade.ZIndex = layer;
        return true;
    }

    // Some native paths explicitly clear to transparent white even when no
    // rectangle was captured. Their caller owns that write and passes false.
    public void ReleaseFullScreenFade(bool restoreColor = true)
    {
        if (!_ownsFullScreenFade)
            return;
        ColorRect fade = context.Fade;
        fade.Position = _fadePosition;
        fade.Size = _fadeSize;
        if (_fadeZIndex is int layer)
            fade.ZIndex = layer;
        if (restoreColor)
            fade.Color = _fadeColor;
        _ownsFullScreenFade = false;
    }
}
