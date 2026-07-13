using Godot;

namespace oracleofages;

/// <summary>
/// MENU_MAP lifecycle: Select triggers the original fast white fade, swaps the
/// saved gameplay screen for the map, and B/Select performs the reverse swap.
/// </summary>
public sealed class MapMenuController
{
    private enum Phase { Closed, OpeningFadeOut, OpeningFadeIn, Open, ClosingFadeOut, ClosingFadeIn }

    public const float FastFadeFrames = 11.0f;

    private readonly MapScreen _screen;
    private readonly ColorRect _fade;
    private readonly Player _player;
    private readonly Label _roomDebug;
    private readonly System.Func<bool> _canOpen;
    private Phase _phase;
    private float _phaseFrame;

    public bool IsActive => _phase != Phase.Closed;
    public bool IsOpen => _phase == Phase.Open;

    public MapMenuController(MapScreen screen, ColorRect fade, Player player,
        Label roomDebug, System.Func<bool> canOpen)
    {
        _screen = screen;
        _fade = fade;
        _player = player;
        _roomDebug = roomDebug;
        _canOpen = canOpen;
    }

    public void Update(double delta)
    {
        if (_phase == Phase.Closed)
        {
            if (Input.IsActionJustPressed("map") && _canOpen())
                BeginOpening();
            return;
        }

        _screen.Update(delta);
        if (_phase == Phase.Open)
        {
            if (Input.IsActionJustPressed("map") || Input.IsActionJustPressed("item"))
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
                    _screen.Open();
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
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        _screen.Open();
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

    private void BeginOpening()
    {
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
