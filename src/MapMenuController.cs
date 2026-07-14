using Godot;

namespace oracleofages;

/// <summary>
/// MENU_MAP lifecycle: Select triggers the original fast white fade, swaps the
/// saved gameplay screen for the map, and B/Select performs the reverse swap.
/// The development fast-travel action reuses that lifecycle with a revealed
/// overworld map and commits the room change only while the screen is white.
/// </summary>
public sealed class MapMenuController
{
    private enum Phase { Closed, OpeningFadeOut, OpeningFadeIn, Open, ClosingFadeOut, ClosingFadeIn }

    public const float FastFadeFrames = 11.0f;

    private readonly MapScreen _screen;
    private readonly ColorRect _fade;
    private readonly DialogueBox _dialogue;
    private readonly Player _player;
    private readonly Label _roomDebug;
    private readonly System.Func<bool> _canOpen;
    private readonly System.Action<int, int> _fastTravel;
    private Phase _phase;
    private float _phaseFrame;
    private bool _debugFastTravel;
    private bool _travelPending;
    private int _travelGroup;
    private int _travelRoom;

    public bool IsActive => _phase != Phase.Closed;
    public bool IsOpen => _phase == Phase.Open;

    public MapMenuController(MapScreen screen, ColorRect fade, DialogueBox dialogue, Player player,
        Label roomDebug, System.Func<bool> canOpen, System.Action<int, int> fastTravel)
    {
        _screen = screen;
        _fade = fade;
        _dialogue = dialogue;
        _player = player;
        _roomDebug = roomDebug;
        _canOpen = canOpen;
        _fastTravel = fastTravel;
    }

    public void Update(double delta)
    {
        if (_phase == Phase.Closed)
        {
            if (Input.IsActionJustPressed("debug_map_travel") && _canOpen())
                BeginOpening(debugFastTravel: true);
            else if (Input.IsActionJustPressed("map") && _canOpen())
                BeginOpening(debugFastTravel: false);
            return;
        }

        _screen.Update(delta);
        if (_phase == Phase.Open)
        {
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
            return;
        }

        _phaseFrame += (float)(delta * 60.0);
        float progress = Mathf.Clamp(_phaseFrame / FastFadeFrames, 0.0f, 1.0f);
        switch (_phase)
        {
            case Phase.OpeningFadeOut:
                SetFade(progress);
                if (progress >= 1.0f)
                {
                    _screen.Open(_debugFastTravel);
                    StartPhase(Phase.OpeningFadeIn);
                }
                break;
            case Phase.OpeningFadeIn:
                SetFade(1.0f - progress);
                if (progress >= 1.0f)
                {
                    SetFade(0.0f);
                    StartPhase(Phase.Open);
                }
                break;
            case Phase.ClosingFadeOut:
                SetFade(progress);
                if (progress >= 1.0f)
                {
                    _screen.Close();
                    if (_travelPending)
                        _fastTravel(_travelGroup, _travelRoom);
                    StartPhase(Phase.ClosingFadeIn);
                }
                break;
            case Phase.ClosingFadeIn:
                SetFade(1.0f - progress);
                if (progress >= 1.0f)
                    FinishClosing();
                break;
        }
    }

    internal void OpenImmediatelyForValidation()
    {
        _debugFastTravel = false;
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        _screen.Open(debugFastTravel: false);
        _roomDebug.Visible = false;
        SetFade(0.0f);
        _phase = Phase.Open;
        _phaseFrame = 0.0f;
    }

    internal void CloseImmediatelyForValidation()
    {
        _screen.Close();
        _roomDebug.Visible = true;
        FinishClosing();
    }

    internal void OpenDebugImmediatelyForValidation()
    {
        _debugFastTravel = true;
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        _screen.Open(debugFastTravel: true);
        _roomDebug.Visible = false;
        SetFade(0.0f);
        _phase = Phase.Open;
        _phaseFrame = 0.0f;
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
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        _roomDebug.Visible = false;
        StartPhase(Phase.OpeningFadeOut);
    }

    private void BeginClosing() => StartPhase(Phase.ClosingFadeOut);

    private void FinishClosing()
    {
        SetFade(0.0f);
        _phase = Phase.Closed;
        _phaseFrame = 0.0f;
        _debugFastTravel = false;
        _travelPending = false;
        _roomDebug.Visible = true;
        _player.SetPhysicsProcess(true);
        _player.SetProcess(true);
    }

    private void StartPhase(Phase phase)
    {
        _phase = phase;
        _phaseFrame = 0.0f;
    }

    private void SetFade(float alpha)
    {
        _fade.Color = new Color(1, 1, 1, Mathf.Clamp(alpha, 0.0f, 1.0f));
    }
}
