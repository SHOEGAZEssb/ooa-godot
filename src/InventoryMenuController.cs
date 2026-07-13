using Godot;
using System;

namespace oracleofages;

public sealed class InventoryMenuController
{
    private readonly InventoryScreen _screen;
    private readonly Player _player;
    private readonly Label _roomDebug;
    private readonly Func<bool> _canOpen;

    public bool IsActive { get; private set; }

    public InventoryMenuController(
        InventoryScreen screen,
        Player player,
        Label roomDebug,
        Func<bool> canOpen)
    {
        _screen = screen;
        _player = player;
        _roomDebug = roomDebug;
        _canOpen = canOpen;
    }

    public void Update()
    {
        if (!IsActive)
        {
            if (Input.IsActionJustPressed("inventory") && _canOpen())
                Open();
            return;
        }

        if (Input.IsActionJustPressed("inventory"))
        {
            Close();
            return;
        }

        if (Input.IsActionJustPressed("attack"))
        {
            _screen.EquipToA();
            return;
        }

        if (Input.IsActionJustPressed("item"))
        {
            _screen.EquipToB();
            return;
        }

        if (Input.IsActionJustPressed("move_right"))
            _screen.MoveCursor(Vector2I.Right);
        else if (Input.IsActionJustPressed("move_left"))
            _screen.MoveCursor(Vector2I.Left);
        else if (Input.IsActionJustPressed("move_up"))
            _screen.MoveCursor(Vector2I.Up);
        else if (Input.IsActionJustPressed("move_down"))
            _screen.MoveCursor(Vector2I.Down);
    }

    internal void OpenImmediatelyForValidation()
    {
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        _roomDebug.Visible = false;
        _screen.Open();
        IsActive = true;
    }

    internal void CloseImmediatelyForValidation() => Close();

    private void Open()
    {
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        _roomDebug.Visible = false;
        _screen.Open();
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
}
