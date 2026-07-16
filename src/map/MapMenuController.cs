using Godot;

namespace oracleofages;

/// <summary>
/// MENU_MAP-specific input and screen behavior. The shared bank-2-style menu
/// load state, fast palette fade, and gameplay pause are owned by
/// OracleMenuLifecycle.
/// </summary>
public sealed class MapMenuController : IOracleMenuLifecycleClient
{
    public const float FastFadeFrames = OracleMenuLifecycle.FastFadeUpdates;

    private readonly MapScreen _screen;
    private readonly DialogueBox _dialogue;
    private readonly OracleMenuLifecycle _lifecycle;
    private readonly System.Func<bool> _canOpen;
    private readonly System.Func<bool> _normalMenuUnlocked;
    private readonly System.Action<int, int> _fastTravel;
    private bool _debugFastTravel;
    private bool _travelPending;
    private int _travelGroup;
    private int _travelRoom;

    public bool IsActive => _lifecycle.IsOwnedBy(this);
    public bool IsOpen => _lifecycle.IsOpenFor(this);
    string IOracleMenuLifecycleClient.MenuName => "MENU_MAP";

    internal MapMenuController(
        MapScreen screen,
        DialogueBox dialogue,
        OracleMenuLifecycle lifecycle,
        System.Func<bool> canOpen,
        System.Func<bool> normalMenuUnlocked,
        System.Action<int, int> fastTravel)
    {
        _screen = screen;
        _dialogue = dialogue;
        _lifecycle = lifecycle;
        _canOpen = canOpen;
        _normalMenuUnlocked = normalMenuUnlocked;
        _fastTravel = fastTravel;
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

        // Preserve the existing map marker/popup cadence while the common
        // palette thread is fading into or out of MENU_MAP.
        _screen.Update(delta);
        if (!IsOpen)
        {
            _lifecycle.Update(this, delta);
            return;
        }

        if (_dialogue.BlocksPlayerInput)
            return;
        if (_debugFastTravel && Input.IsActionJustPressed("debug_map_travel"))
            _screen.ToggleDebugWorld();
        else if (_debugFastTravel && Input.IsActionJustPressed("attack") &&
            _screen.TryGetFastTravelTarget(out int group, out int room))
        {
            _travelPending = true;
            _travelGroup = group;
            _travelRoom = room;
            BeginClosing();
        }
        else if (!_debugFastTravel && Input.IsActionJustPressed("attack") &&
            _screen.TryGetSelectedAreaText(out MapDataDatabase.MapText text))
        {
            _dialogue.ShowMessage(text.Message, _screen.SelectedMarkerY, text.Position);
        }
        else if (Input.IsActionJustPressed("map") || Input.IsActionJustPressed("item"))
            BeginClosing();
        else
            _screen.HandleDirectionInput();
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

    private void BeginClosing() => _lifecycle.BeginClosing(this);

    void IOracleMenuLifecycleClient.OpenAtWhite() => _screen.Open(_debugFastTravel);

    void IOracleMenuLifecycleClient.CloseAtWhite()
    {
        _screen.Close();
        if (_travelPending)
            _fastTravel(_travelGroup, _travelRoom);
    }

    void IOracleMenuLifecycleClient.LifecycleClosed()
    {
        _debugFastTravel = false;
        _travelPending = false;
    }
}
