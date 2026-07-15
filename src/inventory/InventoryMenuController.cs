using Godot;
using System;

namespace oracleofages;

public sealed class InventoryMenuController
{
    private enum Phase
    {
        Closed, OpeningFadeOut, OpeningFadeIn, InventoryOpen, SaveOpen,
        SaveSelectionDelay, ClosingFadeOut, ClosingFadeIn
    }

    // fastFadeoutToWhite / fastFadeinFromWhite advance the original $20-step
    // palette range by $03 per update, stopping on the 11th update.
    public const float FastFadeFrames = 11.0f;
    public const int SaveSelectionDelayFrames = 30;

    private readonly InventoryScreen _screen;
    private readonly SaveQuitScreen _saveScreen;
    private readonly ColorRect _fade;
    private readonly Player _player;
    private readonly Label _roomDebug;
    private readonly Func<bool> _canOpen;
    private readonly Action _save;
    private readonly Action _quitToTitle;
    private Phase _phase;
    private float _phaseFrame;
    private bool _openingSaveMenu;

    public bool IsActive => _phase != Phase.Closed;
    public bool IsOpen => _phase is Phase.InventoryOpen or Phase.SaveOpen;
    public bool SaveMenuOpen => _phase is Phase.SaveOpen or Phase.SaveSelectionDelay;
    internal bool CanOpenForValidation => _phase == Phase.Closed && _canOpen();
    internal int SaveRequests { get; private set; }
    internal int QuitRequests { get; private set; }

    public InventoryMenuController(
        InventoryScreen screen,
        SaveQuitScreen saveScreen,
        ColorRect fade,
        Player player,
        Label roomDebug,
        Func<bool> canOpen,
        Action save,
        Action quitToTitle)
    {
        _screen = screen;
        _saveScreen = saveScreen;
        _fade = fade;
        _player = player;
        _roomDebug = roomDebug;
        _canOpen = canOpen;
        _save = save;
        _quitToTitle = quitToTitle;
    }

    public void Update(double delta)
    {
        if (_phase == Phase.Closed)
        {
            bool inventoryPressed = Input.IsActionJustPressed("inventory");
            bool mapPressed = Input.IsActionJustPressed("map");
            if (inventoryPressed && mapPressed && _canOpen())
                BeginOpening(openSaveMenu: true);
            else if (inventoryPressed && _canOpen())
                BeginOpening(openSaveMenu: false);
            return;
        }

        if (_phase == Phase.InventoryOpen)
        {
            if (_screen.PageTransitionActive)
            {
                _screen.UpdatePageTransition(delta);
                return;
            }
            if (Input.IsActionJustPressed("inventory"))
            {
                BeginClosing();
                return;
            }
            if (Input.IsActionJustPressed("map"))
            {
                _screen.BeginNextSubscreen();
                return;
            }
            if (Input.IsActionJustPressed("attack"))
            {
                if (_screen.SaveAndQuitSelected)
                    OpenSaveMenuFromInventory();
                else if (_screen.Subscreen == InventoryScreen.InventorySubscreen.SecondaryItems)
                    _screen.EquipSelectedRing();
                else
                    _screen.EquipToA();
                return;
            }
            if (Input.IsActionJustPressed("item"))
            {
                _screen.EquipToB();
                return;
            }
            HandleDirectionInput();
            return;
        }

        if (_phase == Phase.SaveOpen)
        {
            if (Input.IsActionJustPressed("item"))
            {
                BeginClosing();
                return;
            }
            if (Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("inventory"))
            {
                SelectSaveOption();
                return;
            }
            if (Input.IsActionJustPressed("move_up"))
                _saveScreen.Move(-1);
            else if (Input.IsActionJustPressed("move_down"))
                _saveScreen.Move(1);
            return;
        }

        if (_phase == Phase.SaveSelectionDelay)
        {
            _phaseFrame += (float)(delta * 60.0);
            _saveScreen.DelayCounter = Math.Max(0,
                SaveSelectionDelayFrames - Mathf.FloorToInt(_phaseFrame));
            if (_phaseFrame < SaveSelectionDelayFrames)
                return;
            if (_saveScreen.Cursor == 2)
            {
                QuitRequests++;
                _saveScreen.Close();
                _phase = Phase.Closed;
                _quitToTitle();
            }
            else
                BeginClosing();
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
                    if (_openingSaveMenu)
                        _saveScreen.Open();
                    else
                        _screen.Open();
                    StartPhase(Phase.OpeningFadeIn);
                }
                break;
            case Phase.OpeningFadeIn:
                SetFade(1.0f - progress);
                if (progress >= 1.0f)
                {
                    SetFade(0.0f);
                    StartPhase(_openingSaveMenu ? Phase.SaveOpen : Phase.InventoryOpen);
                }
                break;
            case Phase.ClosingFadeOut:
                SetFade(progress);
                if (progress >= 1.0f)
                {
                    _screen.Close();
                    _saveScreen.Close();
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

    internal void BeginOpeningForValidation() => BeginOpening(openSaveMenu: false);
    internal void BeginSaveOpeningForValidation() => BeginOpening(openSaveMenu: true);
    internal void BeginClosingForValidation() => BeginClosing();
    internal void OpenSaveMenuFromInventoryForValidation() => OpenSaveMenuFromInventory();
    internal void SelectSaveOptionForValidation() => SelectSaveOption();

    internal void OpenImmediatelyForValidation()
    {
        FreezeGameplay();
        _screen.Open();
        _saveScreen.Close();
        _openingSaveMenu = false;
        SetFade(0.0f);
        _phase = Phase.InventoryOpen;
        _phaseFrame = 0.0f;
    }

    internal void OpenSaveImmediatelyForValidation()
    {
        FreezeGameplay();
        _screen.Close();
        _saveScreen.Open();
        _openingSaveMenu = true;
        SetFade(0.0f);
        _phase = Phase.SaveOpen;
        _phaseFrame = 0.0f;
    }

    internal void CloseImmediatelyForValidation()
    {
        _screen.Close();
        _saveScreen.Close();
        FinishClosing();
    }

    private void BeginOpening(bool openSaveMenu)
    {
        FreezeGameplay();
        _openingSaveMenu = openSaveMenu;
        StartPhase(Phase.OpeningFadeOut);
    }

    private void OpenSaveMenuFromInventory()
    {
        _screen.Close();
        _saveScreen.Open();
        _openingSaveMenu = true;
        SetFade(1.0f);
        StartPhase(Phase.OpeningFadeIn);
    }

    private void SelectSaveOption()
    {
        if (_phase != Phase.SaveOpen)
            return;
        if (_saveScreen.Cursor != 0)
        {
            SaveRequests++;
            _save();
        }
        _saveScreen.DelayCounter = SaveSelectionDelayFrames;
        StartPhase(Phase.SaveSelectionDelay);
    }

    private void HandleDirectionInput()
    {
        if (Input.IsActionJustPressed("move_right"))
            _screen.MoveCursor(Vector2I.Right);
        else if (Input.IsActionJustPressed("move_left"))
            _screen.MoveCursor(Vector2I.Left);
        else if (Input.IsActionJustPressed("move_up"))
            _screen.MoveCursor(Vector2I.Up);
        else if (Input.IsActionJustPressed("move_down"))
            _screen.MoveCursor(Vector2I.Down);
    }

    private void BeginClosing() => StartPhase(Phase.ClosingFadeOut);

    private void FreezeGameplay()
    {
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
        _roomDebug.Visible = false;
    }

    private void FinishClosing()
    {
        SetFade(0.0f);
        _phase = Phase.Closed;
        _phaseFrame = 0.0f;
        _openingSaveMenu = false;
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
