using Godot;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateControllerDirectionCombinations()
    {
        // specialObjects.s:updateGameKeysPressed @directionButtonToAngle:
        // $ff $08 $18 $ff $00 $04 $1c $ff $10 $0c $14 $ff $ff $ff $ff.
        // Opposing buttons have no movement direction, including three-button chords.
        Vector2[] expected =
        [
            Vector2.Zero, Vector2.Right, Vector2.Left, Vector2.Zero,
            Vector2.Up, new(1, -1), new(-1, -1), Vector2.Zero,
            Vector2.Down, new(1, 1), new(-1, 1), Vector2.Zero,
            Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero
        ];
        string[] actions = ["move_right", "move_left", "move_up", "move_down"];
        for (int mask = 0; mask < 16; mask++)
        {
            Vector2? splitResult = null;
            foreach (bool batched in new[] { false, true })
            {
                ReinitializeGameplayForValidation();
                ResetValidationInput();
                LoadValidationRoom(0, 0x11);
                Vector2? origin = null;
                for (int y = 24; y < 104 && origin is null; y += 8)
                for (int x = 24; x < 136 && origin is null; x += 8)
                {
                    Vector2 point = new(x, y);
                    bool clear = true;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        clear &= !_collision.Collides(point + new Vector2(dx, dy) * 8);
                    if (clear) origin = point;
                }
                FailIf(origin is null, "Room 0:$11 has no clear direction-combination fixture.");
                _player.WarpTo(origin.Value);
                void PressMask()
                {
                    for (int bit = 0; bit < actions.Length; bit++)
                        if ((mask & (1 << bit)) != 0) Input.ActionPress(actions[bit]);
                }
                void Advance()
                {
                    if (batched) base._Process(3.0 / 60);
                    else for (int i = 0; i < 3; i++) base._Process(1.0 / 60);
                }
                try
                {
                    PressMask();
                    Vector2 direction = expected[mask].Normalized();
                    FailIf(!Input.ReadMovement().IsEqualApprox(direction),
                        $"Physical direction bits ${mask:x1} did not match the source angle table.");
                    var buffer = new ApplicationInputBuffer();
                    buffer.CaptureHostFrame();
                    Input.BeginOriginalUpdate(buffer.ConsumeOriginalUpdate());
                    try
                    {
                        FailIf(!Input.GetVector(actions[1], actions[0], actions[2], actions[3]).IsEqualApprox(direction),
                            $"Buffered direction bits ${mask:x1} changed the source direction.");
                        Input.OverrideGameKeys(mask << 4);
                        FailIf(!Input.GetVector(actions[1], actions[0], actions[2], actions[3]).IsEqualApprox(direction),
                            $"Simulated direction bits ${mask:x1} disagreed with physical input.");
                        for (int bit = 0; bit < actions.Length; bit++)
                            FailIf(Input.IsActionPressed(actions[bit]) != ((mask & (1 << bit)) != 0) ||
                                Input.IsPhysicalActionPressed(actions[bit]) != ((mask & (1 << bit)) != 0),
                                $"Direction bits ${mask:x1} lost raw held buttons while resolving movement.");
                    }
                    finally { Input.EndOriginalUpdate(); }
                    Advance();
                    Vector2 displacement = _player.PrecisePosition - origin.Value;
                    FailIf(direction == Vector2.Zero ? displacement != Vector2.Zero :
                        displacement.Dot(direction) <= 0 || !Mathf.IsZeroApprox(displacement.Cross(direction)),
                        $"Direction bits ${mask:x1} moved Link incorrectly in room 0:$11: {displacement}.");
                    if (splitResult is not null)
                        FailIf(displacement != splitResult.Value, $"Direction bits ${mask:x1} changed with host batching.");
                    splitResult = displacement;
                    ResetValidationInput();
                    Vector2 stopped = _player.PrecisePosition;
                    base._Process(1.0 / 60);
                    FailIf(_player.PrecisePosition != stopped, $"Releasing direction bits ${mask:x1} did not stop Link.");
                    PressMask();
                    Advance();
                    FailIf(_player.PrecisePosition - stopped != displacement,
                        $"Direction bits ${mask:x1} changed after release and repeat.");
                }
                finally { ResetValidationInput(); }
            }
        }
        GD.Print("Validated all $10 direction combinations through physical, buffered and simulated input, gameplay movement, release/repeat and split/batched updates.");
    }

    private void ValidateControllerMovement()
    {
        (string action, JoyButton button, JoyAxis axis, float sign, Vector2 direction)[] directions =
        [
            ("move_up", JoyButton.DpadUp, JoyAxis.LeftY, -1, Vector2.Up),
            ("move_down", JoyButton.DpadDown, JoyAxis.LeftY, 1, Vector2.Down),
            ("move_left", JoyButton.DpadLeft, JoyAxis.LeftX, -1, Vector2.Left),
            ("move_right", JoyButton.DpadRight, JoyAxis.LeftX, 1, Vector2.Right)
        ];
        foreach (int device in new[] { 0, 3 })
        foreach (var direction in directions)
        foreach (bool stick in new[] { false, true })
        foreach (float crossAxis in new[] { 0f, -0.24f, 0.24f, -0.75f, 0.75f })
        {
            Vector2 expectedDirection = direction.direction;
            if (Mathf.Abs(crossAxis) > 0.25f)
                expectedDirection += direction.axis == JoyAxis.LeftX
                    ? new Vector2(0, Mathf.Sign(crossAxis)) : new Vector2(Mathf.Sign(crossAxis), 0);
            Vector2? splitResult = null;
            foreach (bool batched in new[] { false, true })
            {
                ReinitializeGameplayForValidation();
                ResetValidationInput();
                LoadValidationRoom(0, 0x11);
                // Find a collision-free patch in the actual room, including
                // every tested destination and Link's collision footprint.
                Vector2? origin = null;
                for (int y = 24; y < 104 && origin is null; y += 8)
                for (int x = 24; x < 136 && origin is null; x += 8)
                {
                    Vector2 point = new(x, y);
                    bool clear = !_collision.Collides(point);
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        clear &= !_collision.Collides(point + new Vector2(dx, dy) * 8);
                    if (clear) origin = point;
                }
                FailIf(origin is null, "Room 0:$11 has no clear controller movement fixture.");
                _player.WarpTo(origin.Value);
                void Send(float strength)
                {
                    using InputEvent ev = stick
                        ? new InputEventJoypadMotion { Device = device, Axis = direction.axis, AxisValue = direction.sign * strength }
                        : new InputEventJoypadButton { Device = device, ButtonIndex = direction.button, Pressed = strength > 0 };
                    Godot.Input.ParseInputEvent(ev);
                    using var cross = new InputEventJoypadMotion
                    {
                        Device = device,
                        Axis = direction.axis == JoyAxis.LeftX ? JoyAxis.LeftY : JoyAxis.LeftX,
                        AxisValue = strength > 0.25f ? crossAxis : 0
                    };
                    Godot.Input.ParseInputEvent(cross);
                    Godot.Input.FlushBufferedEvents();
                }
                try
                {
                    if (stick)
                    {
                        Send(0.1f);
                        base._Process(1.0 / 60);
                        FailIf(_player.PrecisePosition != origin.Value || Godot.Input.IsActionPressed(direction.action),
                            "Left-stick noise inside the 0.25 deadzone moved Link.");
                    }
                    Send(1);
                    FailIf(!Input.GetVector("move_left", "move_right", "move_up", "move_down")
                        .IsEqualApprox(expectedDirection.Normalized()),
                        $"Controller direction leaked cross-axis input {crossAxis} through the input facade.");
                    if (batched) base._Process(3.0 / 60);
                    else for (int i = 0; i < 3; i++) base._Process(1.0 / 60);
                    Vector2 displacement = _player.PrecisePosition - origin.Value;
                    FailIf(displacement.Dot(expectedDirection) <= 0 ||
                        !Mathf.IsZeroApprox(displacement.Cross(expectedDirection)),
                        $"Controller device {device}, {direction.action}, stick={stick}, cross-axis={crossAxis} moved Link incorrectly: {displacement}.");
                    if (splitResult is not null)
                        FailIf(displacement != splitResult.Value, "Controller movement changed with host batching.");
                    splitResult = displacement;
                    Send(0);
                    Vector2 stopped = _player.PrecisePosition;
                    base._Process(1.0 / 60);
                    FailIf(_player.PrecisePosition != stopped, "Controller release did not stop Link.");
                    Send(1);
                    base._Process(1.0 / 60);
                    FailIf((_player.PrecisePosition - stopped).Dot(direction.direction) <= 0,
                        "Controller movement failed after release and repeat.");
                }
                finally { Send(0); ResetValidationInput(); }
            }
        }
        foreach (int device in new[] { 0, 3 })
        {
            foreach (var button in new[] { ("attack", JoyButton.A), ("item", JoyButton.B),
                ("map", JoyButton.Back), ("inventory", JoyButton.Start) })
            {
                using var ev = new InputEventJoypadButton { Device = device, ButtonIndex = button.Item2, Pressed = true };
                try
                {
                    Godot.Input.ParseInputEvent(ev);
                    Godot.Input.FlushBufferedEvents();
                    var input = new ApplicationInputBuffer();
                    input.CaptureHostFrame();
                    FailIf(!input.ConsumeOriginalUpdate().IsPressed(button.Item1),
                        $"Controller device {device} did not bind {button.Item1}.");
                }
                finally
                {
                    using var release = new InputEventJoypadButton
                        { Device = device, ButtonIndex = button.Item2, Pressed = false };
                    Godot.Input.ParseInputEvent(release);
                    Godot.Input.FlushBufferedEvents();
                    ResetValidationInput();
                }
            }
        }
        GD.Print("Validated D-pad/left-stick gameplay on device 0 and 3, cross-axis drift, deliberate diagonals, action buttons, deadzone, release/repeat and host batching.");
    }
}
