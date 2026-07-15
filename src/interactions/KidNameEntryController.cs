using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Gameplay-owned use of MENU_KIDNAME ($07). The original routes this through
/// the file-menu text-input renderer, so it intentionally reuses that screen's
/// imported keyboard, palettes, cursors, and five-character buffer.
/// </summary>
internal sealed class KidNameEntryController(
    Node interfaceLayer,
    Action<int>? playSound = null)
{
    private MainMenuScreen? _screen;
    private string? _result;
    private ulong _openedFrame;

    public bool Active => _screen is not null;

    public void Open(string initialName)
    {
        CloseScreen();
        _result = null;
        _screen = new MainMenuScreen
        {
            Name = "KidNameEntry",
            ZIndex = 60
        };
        interfaceLayer.AddChild(_screen);
        _screen.ShowNameEntry(0, initialName);
        _openedFrame = Engine.GetProcessFrames();
    }

    public void Update()
    {
        if (_screen is null || Engine.GetProcessFrames() == _openedFrame)
            return;

        Vector2I movement = Vector2I.Zero;
        if (Input.IsActionJustPressed("move_up")) movement = Vector2I.Up;
        else if (Input.IsActionJustPressed("move_down")) movement = Vector2I.Down;
        else if (Input.IsActionJustPressed("move_left")) movement = Vector2I.Left;
        else if (Input.IsActionJustPressed("move_right")) movement = Vector2I.Right;
        if (movement != Vector2I.Zero)
        {
            _screen.MoveNameCursor(movement);
            playSound?.Invoke(OracleSoundEngine.SndMenuMove);
        }

        if (Input.IsActionJustPressed("item"))
        {
            _screen.DeleteNameCharacter();
            playSound?.Invoke(OracleSoundEngine.SndMenuMove);
        }
        else if (Input.IsActionJustPressed("inventory"))
        {
            Commit();
        }
        else if (Input.IsActionJustPressed("attack"))
        {
            Accept();
        }
    }

    public bool TryTakeResult(out string name)
    {
        name = _result ?? string.Empty;
        if (_result is null)
            return false;
        _result = null;
        return true;
    }

    internal MainMenuScreen? ScreenForValidation => _screen;

    internal void CommitForValidation(string name)
    {
        if (_screen is null)
            throw new InvalidOperationException("The kid-name menu is not open.");
        _result = name;
        CloseScreen();
    }

    private void Accept()
    {
        if (_screen!.TryGetSelectedNameCharacter(out char character))
        {
            _screen.AppendNameCharacter(character);
            playSound?.Invoke(OracleSoundEngine.SndSelectItem);
            return;
        }

        switch (_screen.NameLowerChoice)
        {
            case 0:
                _screen.MoveNameEntryPosition(-1);
                playSound?.Invoke(OracleSoundEngine.SndMenuMove);
                break;
            case 1:
                _screen.MoveNameEntryPosition(1);
                playSound?.Invoke(OracleSoundEngine.SndMenuMove);
                break;
            default:
                Commit();
                break;
        }
    }

    private void Commit()
    {
        _result = _screen!.EnteredName;
        playSound?.Invoke(OracleSoundEngine.SndSelectItem);
        CloseScreen();
    }

    private void CloseScreen()
    {
        if (_screen is null)
            return;
        _screen.GetParent()?.RemoveChild(_screen);
        _screen.QueueFree();
        _screen = null;
    }
}
