using Godot;

namespace oracleofages;

/// <summary>
/// MENU_MAP-specific input and screen behavior. The shared bank-2-style menu
/// load state, fast palette fade, and gameplay pause are owned by
/// OracleMenuLifecycle.
/// </summary>
public sealed partial class MapMenuController : IOracleMenuLifecycleClient
{
    public const float FastFadeFrames = OracleMenuLifecycle.FastFadeUpdates;

    private readonly MapScreen _screen;
    private readonly DialogueBox _dialogue;
    private readonly OracleMenuLifecycle _lifecycle;
    private readonly System.Func<bool> _canOpen;
    private readonly System.Func<bool> _normalMenuUnlocked;
    private readonly System.Action<int, int> _fastTravel;
    private readonly System.Action<int> _playSound;
    private readonly System.Action<int> _setMusicVolume;
    private bool _debugFastTravel;
    private bool _travelPending;
    private int _travelGroup;
    private int _travelRoom;
    private InventoryMenuController? _saveQuit;
    internal void ConfigureSaveQuit(InventoryMenuController menu) => _saveQuit = menu;

    public bool IsActive => _lifecycle.IsOwnedBy(this);
    public bool IsOpen => _lifecycle.IsOpenFor(this);
    string IOracleMenuLifecycleClient.MenuName => _gale ? "MENU_GALE_SEED" : "MENU_MAP";
    bool IOracleMenuLifecycleClient.CompletesClosingAtWhite => _gale && _galeTravel;
    int IOracleMenuLifecycleClient.ClosingFadeUpdates => _gale && _galeTravel ? 32 : OracleMenuLifecycle.FastFadeUpdates;

    internal MapMenuController(
        MapScreen screen,
        DialogueBox dialogue,
        OracleMenuLifecycle lifecycle,
        System.Func<bool> canOpen,
        System.Func<bool> normalMenuUnlocked,
        System.Action<int, int> fastTravel,
        System.Action<int> playSound,
        System.Action<int> setMusicVolume)
    {
        _screen = screen;
        _dialogue = dialogue;
        _lifecycle = lifecycle;
        _canOpen = canOpen;
        _normalMenuUnlocked = normalMenuUnlocked;
        _fastTravel = fastTravel;
        _playSound = playSound;
        _setMusicVolume = setMusicVolume;
    }

    public void Update(double delta)
    {
        if (!IsActive)
        {
            if (Input.IsActionJustPressed("debug_map_travel") && _canOpen())
                BeginOpening(debugFastTravel: true);
            else if (Input.IsActionJustPressed("map") && _normalMenuUnlocked() && _canOpen())
                BeginOpening(debugFastTravel: false);
            return;
        }

        if (!IsOpen)
        {
            if (!_gale && !_debugFastTravel && _lifecycle.CurrentPhase == Phase.OpeningFadeOut &&
                Input.IsActionPressed("inventory") && Input.IsActionPressed("map"))
            {
                _saveQuit!.TakeOverMapOpening(this);
                _saveQuit.Update(delta);
                return;
            }
            // menuStateFadeOutOfMenu retains the last OAM without running
            // mapMenu_drawSprites; only opening fade-in animates the map.
            if (_lifecycle.CurrentPhase == Phase.OpeningFadeIn) _screen.Update(delta);
            _lifecycle.Update(this, delta);
            return;
        }
        // mapMenu_state1 checks input before submitting/advancing popup OAM.
        try { UpdateOpenInput(); }
        finally { _screen.Update(delta); }
    }

    private void UpdateOpenInput()
    {
        if (_gale)
        {
            UpdateGaleInput();
            return;
        }
        if (_dialogue.BlocksPlayerInput)
            return;
        if (_screen.Mode == MapMode.Dungeon)
        {
            if (Input.IsActionJustPressed("map") || Input.IsActionJustPressed("item"))
            {
                BeginClosing();
                return;
            }
            bool scrolling = _screen.IsScrolling;
            _screen.AdvanceDungeonInput();
            if (!scrolling && _screen.HandleDirectionInput(_lifecycle.DirectionInputWithAutofire()))
                _playSound(OracleSoundEngine.SndMenuMove);
            return;
        }
        if (_debugFastTravel && Input.IsActionJustPressed("debug_map_travel"))
            _screen.CycleDebugPage();
        else if (_debugFastTravel && Input.IsActionJustPressed("attack") &&
            _screen.TryGetFastTravelTarget(out int group, out int room))
        {
            _travelPending = true;
            _travelGroup = group;
            _travelRoom = room;
            BeginClosing();
        }
        else if (_screen.HandleDirectionInput(_lifecycle.DirectionInputWithAutofire()))
            _playSound(OracleSoundEngine.SndMenuMove);
        else if (!_debugFastTravel && Input.IsActionJustPressed("attack"))
        {
            // A consumes this update even if an unvisited room has no text.
            if (_screen.TryGetSelectedAreaText(out MapText text))
                _dialogue.ShowMessage(text.Message,
                    DialogueScreenContext.FullScreen(_screen.SelectedMarkerY),
                    _screen.CursorRoom < 0x80 ? 3 : 0, textboxFlags: 0x09);
        }
        else if (Input.IsActionJustPressed("map") || Input.IsActionJustPressed("item"))
            BeginClosing();
    }

    internal void BeginOpeningForValidation() => BeginOpening(debugFastTravel: false);

    internal bool NavigateForValidation(Vector2I direction)
    {
        if (!IsOpen || !_screen.Navigate(direction))
            return false;
        _playSound(OracleSoundEngine.SndMenuMove);
        return true;
    }

    internal void OpenImmediatelyForValidation()
    {
        _debugFastTravel = false;
        _travelPending = false;
        _lifecycle.OpenImmediately(this);
    }

    internal bool CanOpenNormalForValidation =>
        _lifecycle.IsIdle && _normalMenuUnlocked() && _canOpen();

    internal void CloseImmediatelyForValidation() => _lifecycle.CloseImmediately(this);

    internal void OpenDebugImmediatelyForValidation()
    {
        _debugFastTravel = true;
        _travelPending = false;
        _lifecycle.OpenImmediately(this);
    }

    internal bool BeginTravelToSelectionForValidation()
    {
        if (!_screen.TryGetFastTravelTarget(out int group, out int room))
            return false;
        _travelPending = true;
        _travelGroup = group;
        _travelRoom = room;
        BeginClosing();
        return true;
    }

    private void BeginOpening(bool debugFastTravel)
    {
        _debugFastTravel = debugFastTravel;
        _travelPending = false;
        _lifecycle.TryBeginOpening(this);
    }

    private void BeginClosing()
    {
        if (!_gale || !_galeTravel) _playSound(OracleSoundEngine.SndCloseMenu);
        _lifecycle.BeginClosing(this);
    }

    void IOracleMenuLifecycleClient.OpenAtWhite()
    {
        // menuStateFadeIntoMenu requests SND_OPENMENU ($54) after the fast
        // fade reaches white, immediately before loading MENU_MAP.
        _playSound(OracleSoundEngine.SndOpenMenu);
        _setMusicVolume(2);
        _screen.Open(_debugFastTravel);
        if (_gale)
        {
            RefreshGaleSelection();
        }
    }

    void IOracleMenuLifecycleClient.CloseAtWhite()
    {
        _screen.Close();
        if (_gale && _galeTravel) _galeWarp!(_galeDatabase!.Get(_galeRooms!, _galeIndex));
        if (_travelPending)
            _fastTravel(_travelGroup, _travelRoom);
    }

    void IOracleMenuLifecycleClient.LifecycleClosed()
    {
        _setMusicVolume(3);
        if (_gale)
        {
            if (!_galeTravel) _galeCancel!();
        }
        _gale = false;
        _debugFastTravel = false;
        _travelPending = false;
    }
}
