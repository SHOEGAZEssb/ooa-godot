using Godot;
using System;

namespace Oracles;

/// <summary>MENU_KIDNAME uses the shared file-menu keyboard and menu lifecycle.</summary>
internal sealed class KidNameEntryController(
    Node interfaceLayer, OracleMenuLifecycle lifecycle, OracleSaveData save,
    OracleRuntimeState memory, Action<int> playSound, Action<int> setMusicVolume) : IOracleMenuLifecycleClient
{
    private MainMenuScreen? _screen;
    private bool _submitted;

    public string MenuName => "MENU_KIDNAME";
    internal bool IsActive => lifecycle.IsOwnedBy(this);
    internal MainMenuScreen? Screen => _screen;

    internal bool Open()
    {
        if (!lifecycle.TryBeginOpening(this)) return false;
        _submitted = false;
        return true;
    }

    internal void Update(double delta)
    {
        if (!IsActive) return;
        if (!lifecycle.IsOpenFor(this))
        {
            lifecycle.Update(this, delta);
            return;
        }
        // bank2.s:runKidNameEntryMenu mode2 follows the submitting input update.
        if (_submitted)
        {
            string name = _screen!.EnteredName;
            memory.SetField("wTextInputResult", name.Length == 0 ? (byte)1 : (byte)0);
            if (name.Length != 0)
            {
                save.SetChildName(name);
                playSound(SoundId.SndSelectItem);
            }
            playSound(SoundId.SndCloseMenu);
            lifecycle.BeginClosing(this);
            return;
        }
        memory.SetField("wTextInputResult", 1);
        int directions = lifecycle.DirectionInputWithAutofire();
        if (directions != 0)
        {
            _screen!.MoveNameCursor((directions & 8) != 0 ? Vector2I.Down :
                (directions & 4) != 0 ? Vector2I.Up :
                (directions & 2) != 0 ? Vector2I.Left : Vector2I.Right);
            playSound(SoundId.SndMenuMove);
        }
        else if (Input.IsActionJustPressed("inventory"))
        {
            playSound(SoundId.SndSelectItem);
            _submitted = _screen!.SelectNameOkay();
        }
        else if (Input.IsActionJustPressed("map")) playSound(SoundId.SndSelectItem);
        else if (Input.IsActionJustPressed("item"))
        {
            _screen!.DeleteNameCharacter();
            playSound(SoundId.SndClink);
        }
        else if (Input.IsActionJustPressed("attack"))
        {
            playSound(SoundId.SndSelectItem);
            if (_screen!.TryGetSelectedNameCharacter(out char character))
                _screen.AppendNameCharacter(character);
            else if (_screen.NameLowerChoice == 0) _screen.MoveNameEntryPosition(-1);
            else if (_screen.NameLowerChoice == 1) _screen.MoveNameEntryPosition(1);
            else _submitted = true;
        }
    }

    internal void Cancel()
    {
        if (IsActive) lifecycle.CloseImmediately(this);
    }

    public void OpenAtWhite()
    {
        playSound(SoundId.SndOpenMenu);
        setMusicVolume(2);
        _screen = new MainMenuScreen
        {
            AssetRoot = OracleAssetPack.ForGame(save.Game), Name = "KidNameEntry",
            ZIndex = ScreenDrawPriority.NameEntryZIndex
        };
        interfaceLayer.AddChild(_screen);
        _screen.ShowKidNameEntry(save.ChildName);
    }

    public void CloseAtWhite()
    {
        if (_screen is null) return;
        _screen.GetParent()?.RemoveChild(_screen);
        _screen.QueueFree();
        _screen = null;
    }

    public void LifecycleClosed() => setMusicVolume(3);
}
