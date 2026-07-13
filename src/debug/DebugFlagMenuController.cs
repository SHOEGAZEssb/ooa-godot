using Godot;
using System;

namespace oracleofages;

public sealed class DebugFlagMenuController
{
    private const string InputAction = "debug_flags";
    private readonly DebugFlagScreen _screen;
    private readonly RoomSession _rooms;
    private readonly Player _player;
    private readonly Label _roomDebug;
    private readonly Func<bool> _canOpen;

    public bool IsActive { get; private set; }

    public DebugFlagMenuController(
        DebugFlagScreen screen,
        RoomSession rooms,
        Player player,
        Label roomDebug,
        Func<bool> canOpen)
    {
        _screen = screen;
        _rooms = rooms;
        _player = player;
        _roomDebug = roomDebug;
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
            _screen.ToggleSelectedFlag();
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
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        _roomDebug.Visible = false;
        _screen.Open(_rooms.ActiveGroup, _rooms.CurrentRoom.Id);
        IsActive = true;
    }

    private void Close()
    {
        _screen.Close();
        IsActive = false;
        _roomDebug.Visible = true;
        _player.SetPhysicsProcess(true);
        _player.SetProcess(true);
    }

    private static void EnsureInputAction()
    {
        if (InputMap.HasAction(InputAction))
            return;
        InputMap.AddAction(InputAction);
        InputMap.ActionAddEvent(InputAction, new InputEventKey { PhysicalKeycode = Key.F1 });
    }
}
