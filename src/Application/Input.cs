using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Fixed-update input facade. Outside an application-owned update it delegates
/// directly to Godot, preserving focused controller validation and editor
/// behavior. During an update every reader observes the same immutable sample.
/// </summary>
internal static class Input
{
    private static ApplicationInputSnapshot? _current;
    private static ulong _originalUpdate;

    internal static ulong TimingFrame =>
        _current is null ? Engine.GetProcessFrames() : _originalUpdate;

    internal static void BeginOriginalUpdate(ApplicationInputSnapshot snapshot)
    {
        if (_current is not null)
        {
            throw new InvalidOperationException(
                "An application input snapshot is already active.");
        }
        _originalUpdate++;
        _current = snapshot;
    }

    internal static void EndOriginalUpdate()
    {
        if (_current is null)
        {
            throw new InvalidOperationException(
                "No application input snapshot is active.");
        }
        _current = null;
    }

    internal static bool IsActionPressed(
        StringName action,
        bool exactMatch = false) =>
        _current?.IsPressed(action.ToString()) ??
        Godot.Input.IsActionPressed(action, exactMatch);

    internal static bool IsActionJustPressed(
        StringName action,
        bool exactMatch = false) =>
        _current?.IsJustPressed(action.ToString()) ??
        Godot.Input.IsActionJustPressed(action, exactMatch);

    internal static Vector2 GetVector(
        StringName negativeX,
        StringName positiveX,
        StringName negativeY,
        StringName positiveY,
        float deadzone = -1.0f)
    {
        if (_current is not null &&
            negativeX.ToString() == "move_left" &&
            positiveX.ToString() == "move_right" &&
            negativeY.ToString() == "move_up" &&
            positiveY.ToString() == "move_down")
        {
            return _current.Value.Movement;
        }
        return Godot.Input.GetVector(
            negativeX, positiveX, negativeY, positiveY, deadzone);
    }

    internal static void ActionPress(StringName action, float strength = 1.0f) =>
        Godot.Input.ActionPress(action, strength);

    internal static void ActionRelease(StringName action) =>
        Godot.Input.ActionRelease(action);
}
