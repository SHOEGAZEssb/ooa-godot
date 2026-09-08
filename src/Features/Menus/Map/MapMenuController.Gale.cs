using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class MapMenuController
{
    private GaleTreeWarpDatabase? _galeDatabase;
    private RoomSession? _galeRooms;
    private Action<GaleTreeWarp>? _galeWarp;
    private Action? _galeCancel;
    private Action<int>? _galeMusicVolume;
    private bool _gale;
    private bool _galeTravel;
    private int _galeIndex;
    private int _galeState;
    internal int GaleIndex => _galeIndex;
    internal int GaleState => _galeState;

    internal void ConfigureGale(RoomSession rooms, Action<GaleTreeWarp> warp, Action cancel, Action<int> musicVolume)
    {
        _galeRooms = rooms;
        _galeWarp = warp;
        _galeCancel = cancel;
        _galeMusicVolume = musicVolume;
    }

    internal void OpenGale()
    {
        _galeDatabase ??= new GaleTreeWarpDatabase();
        _galeIndex = _galeDatabase.Move(_galeRooms!, -1, 1);
        _gale = true;
        _galeTravel = false;
        _galeState = 1;
        _debugFastTravel = false;
        _travelPending = false;
        if (!_lifecycle.TryBeginOpening(this))
            throw new InvalidOperationException("MENU_GALE_SEED could not acquire the menu lifecycle.");
    }

    internal bool MoveGale(int offset)
    {
        int previous = _galeIndex;
        _galeIndex = _galeDatabase!.Move(_galeRooms!, _galeIndex, offset);
        RefreshGaleSelection();
        if (previous == _galeIndex) return false;
        _playSound(OracleSoundEngine.SndMenuMove);
        return true;
    }

    private void RefreshGaleSelection()
    {
        List<int> visited = [];
        for (int index = 0; index < 8; index++)
        {
            GaleTreeWarp entry = _galeDatabase!.Get(_galeRooms!, index);
            if (entry.Room == 0) break;
            if (_galeRooms!.HasVisited(_galeRooms.ActiveGroup, entry.Room)) visited.Add(entry.Room);
        }
        _screen.SelectGaleDestination(_galeDatabase!.Get(_galeRooms!, _galeIndex).Room, visited.ToArray());
    }

    internal void PromptGale(bool cancel)
    {
        _galeState = cancel ? 3 : 2;
        MapText text = _screen.GalePrompt(cancel);
        _dialogue.ShowChoiceMessage(text.Message, _screen.SelectedMarkerY, textPosition: text.Position);
    }

    private void UpdateGaleInput()
    {
        if (_galeState is 2 or 3)
        {
            if (!_dialogue.TryTakeChoiceResult(out int choice)) return;
            if (_galeState == 2 ? choice == 0 : choice != 0)
            {
                _galeTravel = _galeState == 2;
                if (_galeTravel) _galeMusicVolume!(3);
                BeginClosing();
            }
            else _galeState = 1;
            return;
        }
        if (_dialogue.BlocksPlayerInput) return;
        if (Input.IsActionJustPressed("item")) PromptGale(cancel: true);
        else if (Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("inventory")) PromptGale(cancel: false);
        else if (Input.IsActionJustPressed("move_right") || Input.IsActionJustPressed("move_down")) MoveGale(1);
        else if (Input.IsActionJustPressed("move_left") || Input.IsActionJustPressed("move_up")) MoveGale(-1);
    }
}
