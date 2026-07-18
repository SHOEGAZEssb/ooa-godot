using Godot;
using System;

namespace oracleofages;

public sealed class DebugFlagMenuController
{
    private const string InputAction = "debug_flags";
    private readonly DebugFlagScreen _screen;
    private readonly RoomSession _rooms;
    private readonly GameplayPauseController _pause;
    private readonly Func<bool> _canOpen;
    private GameplayPauseController.PauseLease? _pauseLease;

    public bool IsActive { get; private set; }

    public DebugFlagMenuController(
        DebugFlagScreen screen,
        RoomSession rooms,
        GameplayPauseController pause,
        Func<bool> canOpen)
    {
        _screen = screen;
        _rooms = rooms;
        _pause = pause;
        _canOpen = canOpen;
        EnsureInputAction();
    }

    public void Update()
    {
        if (Input.IsActionJustPressed(InputAction))
        {
            if (IsActive)
                Close();
            else if (_canOpen())
                Open();
            return;
        }

        if (!IsActive)
            return;
        if (Input.IsActionJustPressed("attack"))
            _screen.ActivateSelection();
        else if (Input.IsActionJustPressed("move_up"))
            _screen.MoveVertical(-1);
        else if (Input.IsActionJustPressed("move_down"))
            _screen.MoveVertical(1);
        else if (Input.IsActionJustPressed("move_left"))
            _screen.MoveHorizontal(-1);
        else if (Input.IsActionJustPressed("move_right"))
            _screen.MoveHorizontal(1);
    }

    internal void OpenImmediatelyForValidation() => Open();
    internal void CloseImmediatelyForValidation() => Close();

    private void Open()
    {
        _pauseLease = _pause.TryAcquire(this);
        if (_pauseLease is null)
            return;
        _screen.Open(_rooms.ActiveGroup, _rooms.CurrentRoom.Id);
        IsActive = true;
    }

    private void Close()
    {
        _screen.Close();
        IsActive = false;
        _pauseLease?.Dispose();
        _pauseLease = null;
    }

    private static void EnsureInputAction()
    {
        if (InputMap.HasAction(InputAction))
            return;
        InputMap.AddAction(InputAction);
        InputMap.ActionAddEvent(InputAction, new InputEventKey { PhysicalKeycode = Key.F1 });
    }
}
