using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Buffers host-frame input until the application scheduler assigns it to an
/// original update. Held state is sampled for every update; pressed edges are
/// cleared after the first consumer.
/// </summary>
internal sealed class ApplicationInputBuffer
{
    private static readonly string[] SampledActions =
    {
        "move_up",
        "move_right",
        "move_down",
        "move_left",
        "attack",
        "item",
        "map",
        "inventory",
        "debug_map_travel",
        "debug_room_warp",
        "debug_collision",
        "debug_flags",
        "debug_maple"
    };

    private readonly HashSet<string> _pressed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pendingJustPressed =
        new(StringComparer.Ordinal);
    private Vector2 _movement;

    internal void CaptureHostFrame()
    {
        _pressed.Clear();
        foreach (string action in SampledActions)
        {
            // Development actions are installed by gameplay-scoped
            // controllers. Title and file-select frames still use this
            // application buffer, so an action may legitimately not exist yet.
            if (!InputMap.HasAction(action))
                continue;
            if (Godot.Input.IsActionPressed(action))
                _pressed.Add(action);
            if (Godot.Input.IsActionJustPressed(action))
                _pendingJustPressed.Add(action);
        }
        _movement = Godot.Input.GetVector(
            "move_left", "move_right", "move_up", "move_down");
    }

    internal ApplicationInputSnapshot ConsumeOriginalUpdate()
    {
        var snapshot = new ApplicationInputSnapshot(
            _pressed,
            _pendingJustPressed,
            _movement);
        _pendingJustPressed.Clear();
        return snapshot;
    }

    internal void CaptureForValidation(
        IEnumerable<string> pressed,
        IEnumerable<string> justPressed,
        Vector2 movement)
    {
        _pressed.Clear();
        foreach (string action in pressed)
            _pressed.Add(action);
        foreach (string action in justPressed)
            _pendingJustPressed.Add(action);
        _movement = movement;
    }

    internal void Clear()
    {
        _pressed.Clear();
        _pendingJustPressed.Clear();
        _movement = Vector2.Zero;
    }
}

internal readonly struct ApplicationInputSnapshot
{
    private readonly HashSet<string> _pressed;
    private readonly HashSet<string> _justPressed;

    internal Vector2 Movement { get; }

    internal ApplicationInputSnapshot(
        IEnumerable<string> pressed,
        IEnumerable<string> justPressed,
        Vector2 movement)
    {
        _pressed = new HashSet<string>(pressed, StringComparer.Ordinal);
        _justPressed = new HashSet<string>(
            justPressed, StringComparer.Ordinal);
        Movement = movement;
    }

    internal bool IsPressed(string action) => _pressed.Contains(action);
    internal bool IsJustPressed(string action) =>
        _justPressed.Contains(action);
}
