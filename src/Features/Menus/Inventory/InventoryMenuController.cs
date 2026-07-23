using Godot;
using System;

namespace oracleofages;

/// <summary>
/// MENU_INVENTORY and MENU_SAVEQUIT-specific behavior. Their common
/// wMenuLoadState fade/pause lifecycle is owned by OracleMenuLifecycle.
/// </summary>
public sealed class InventoryMenuController : IOracleMenuLifecycleClient
{

    public const float FastFadeFrames = OracleMenuLifecycle.FastFadeUpdates;
    public const int SaveSelectionDelayFrames = 30;

    private readonly InventoryScreen _screen;
    private readonly SaveQuitScreen _saveScreen;
    private readonly OracleMenuLifecycle _lifecycle;
    private readonly Func<bool> _normalMenuUnlocked;
    private readonly Func<bool> _canOpen;
    private readonly Func<SaveResult> _save;
    private readonly Action _quitToTitle;
    private readonly Action _continueAfterGameOver;
    private readonly Action<int> _playSound;
    private readonly FixedUpdateAccumulator _saveDelayUpdates = new();
    private OpenMenu _openMenu;
    private bool _saveSelectionDelay;
    private bool _gameOver;
    private int _saveDelayElapsed;

    public bool IsActive => _lifecycle.IsOwnedBy(this);
    public bool IsOpen => _lifecycle.IsOpenFor(this) && !_saveSelectionDelay;
    public bool SaveMenuOpen =>
        _lifecycle.IsOpenFor(this) && _openMenu == OpenMenu.SaveQuit;
    internal bool GameOver => _gameOver;
    internal bool CanOpenForValidation => _lifecycle.IsIdle && _canOpen();
    internal int SaveRequests { get; private set; }
    internal int QuitRequests { get; private set; }
    internal string? LastSaveError { get; private set; }
    string IOracleMenuLifecycleClient.MenuName =>
        _openMenu == OpenMenu.SaveQuit ? "MENU_SAVEQUIT" : "MENU_INVENTORY";

    internal InventoryMenuController(
        InventoryScreen screen,
        SaveQuitScreen saveScreen,
        OracleMenuLifecycle lifecycle,
        Func<bool> normalMenuUnlocked,
        Func<bool> canOpen,
        Func<SaveResult> save,
        Action quitToTitle,
        Action<int> playSound,
        Action? continueAfterGameOver = null)
    {
        _screen = screen;
        _saveScreen = saveScreen;
        _lifecycle = lifecycle;
        _normalMenuUnlocked = normalMenuUnlocked;
        _canOpen = canOpen;
        _save = save;
        _quitToTitle = quitToTitle;
        _playSound = playSound;
        _continueAfterGameOver = continueAfterGameOver ?? (() => { });
    }

    public void Update(double delta)
    {
        if (!IsActive)
        {
            bool inventoryPressed = Input.IsActionJustPressed("inventory");
            bool mapPressed = Input.IsActionJustPressed("map");
            // b2_updateMenus handles this shared Start/Select gate before it
            // dispatches MENU_INVENTORY or MENU_MAP. Keep it here, in the
            // first normal-menu controller updated by GameRoot, so a chord
            // still requests SND_ERROR ($5a) exactly once.
            if ((inventoryPressed || mapPressed) && !_normalMenuUnlocked())
            {
                _playSound(OracleSoundEngine.SndError);
                return;
            }
            if (inventoryPressed && mapPressed && _canOpen())
                BeginOpening(openSaveMenu: true);
            else if (inventoryPressed && _canOpen())
                BeginOpening(openSaveMenu: false);
            return;
        }

        if (!_lifecycle.IsOpenFor(this))
        {
            _lifecycle.Update(this, delta);
            return;
        }

        if (_saveSelectionDelay)
        {
            UpdateSaveSelectionDelay(delta);
            return;
        }

        if (_openMenu == OpenMenu.Inventory)
        {
            UpdateInventoryInput(delta);
            return;
        }

        UpdateSaveInput();
    }

    internal void BeginOpeningForValidation() => BeginOpening(openSaveMenu: false);
    internal void BeginSaveOpeningForValidation() => BeginOpening(openSaveMenu: true);
    internal void BeginGameOverForValidation() => BeginGameOver();
    internal void BeginClosingForValidation() => BeginClosing();
    internal void OpenSaveMenuFromInventoryForValidation() => OpenSaveMenuFromInventory();
    internal void SelectSaveOptionForValidation() => SelectSaveOption();
    internal bool CancelSaveMenuForValidation() => CancelSaveMenu();
    internal bool MoveSaveCursorForValidation(int direction) =>
        MoveSaveCursor(direction);
    internal bool BeginNextSubscreenForValidation() => BeginNextSubscreen();
    internal bool MoveCursorForValidation(Vector2I direction) => MoveCursor(direction);
    internal bool EquipToAForValidation() => EquipToA();
    internal bool EquipToBForValidation() => EquipToB();

    internal void OpenImmediatelyForValidation()
    {
        _openMenu = OpenMenu.Inventory;
        _gameOver = false;
        ResetSaveDelay();
        _lifecycle.OpenImmediately(this);
    }

    internal void OpenSaveImmediatelyForValidation()
    {
        _openMenu = OpenMenu.SaveQuit;
        _gameOver = false;
        ResetSaveDelay();
        _lifecycle.OpenImmediately(this);
    }

    internal void CloseImmediatelyForValidation() => _lifecycle.CloseImmediately(this);

    private void UpdateInventoryInput(double delta)
    {
        if (_screen.PageTransitionActive)
        {
            _screen.UpdatePageTransition(delta);
            return;
        }
        _screen.UpdateInventoryText(delta);
        if (Input.IsActionJustPressed("inventory"))
        {
            BeginClosing();
            return;
        }
        if (Input.IsActionJustPressed("map"))
        {
            BeginNextSubscreen();
            return;
        }
        if (Input.IsActionJustPressed("attack"))
        {
            if (_screen.SaveAndQuitSelected)
                OpenSaveMenuFromInventory();
            else if (_screen.Subscreen == InventorySubscreen.SecondaryItems)
                EquipSelectedRing();
            else
                EquipToA();
            return;
        }
        if (Input.IsActionJustPressed("item"))
        {
            EquipToB();
            return;
        }
        HandleDirectionInput();
    }

    private void UpdateSaveInput()
    {
        if (_saveScreen.SaveErrorVisible)
        {
            if (Input.IsActionJustPressed("attack") ||
                Input.IsActionJustPressed("item") ||
                Input.IsActionJustPressed("inventory"))
            {
                _saveScreen.ClearSaveError();
            }
            return;
        }
        if (Input.IsActionJustPressed("item"))
        {
            CancelSaveMenu();
            return;
        }
        if (Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("inventory"))
        {
            SelectSaveOption();
            return;
        }
        if (Input.IsActionJustPressed("move_up"))
            MoveSaveCursor(-1);
        else if (Input.IsActionJustPressed("move_down"))
            MoveSaveCursor(1);
    }

    private void UpdateSaveSelectionDelay(double delta)
    {
        int updates = _saveDelayUpdates.Consume(delta);
        for (int update = 0; update < updates; update++)
        {
            _saveDelayElapsed++;
            _saveScreen.DelayCounter = Math.Max(
                0, SaveSelectionDelayFrames - _saveDelayElapsed);
            if (_saveDelayElapsed < SaveSelectionDelayFrames)
                continue;

            _saveSelectionDelay = false;
            if (_gameOver)
            {
                bool quit = _saveScreen.Cursor == 2;
                if (quit)
                    QuitRequests++;
                _lifecycle.CloseImmediately(this);
                if (quit)
                    _quitToTitle();
                else
                    _continueAfterGameOver();
                return;
            }
            if (_saveScreen.Cursor == 2)
            {
                QuitRequests++;
                _lifecycle.CloseImmediately(this);
                _quitToTitle();
            }
            else
            {
                BeginClosing();
            }
            return;
        }
    }

    private void BeginOpening(bool openSaveMenu)
    {
        _openMenu = openSaveMenu ? OpenMenu.SaveQuit : OpenMenu.Inventory;
        _gameOver = false;
        ResetSaveDelay();
        _lifecycle.TryBeginOpening(this);
    }

    internal void BeginGameOver()
    {
        _openMenu = OpenMenu.SaveQuit;
        _gameOver = true;
        ResetSaveDelay();
        if (!_lifecycle.TryBeginOpeningFromWhite(this))
        {
            _gameOver = false;
            throw new InvalidOperationException(
                "Game over could not acquire MENU_SAVEQUIT ownership.");
        }
    }

    private bool BeginNextSubscreen()
    {
        if (_screen.PageTransitionActive)
            return false;

        // inventoryMenuState3 starts SND_OPENMENU ($54) on the update that
        // advances wInventorySubmenu and begins the horizontal page scroll.
        _screen.BeginNextSubscreen();
        _playSound(OracleSoundEngine.SndOpenMenu);
        return true;
    }

    private void OpenSaveMenuFromInventory()
    {
        if (!_lifecycle.IsOpenFor(this) || _openMenu != OpenMenu.Inventory)
            throw new InvalidOperationException(
                "MENU_INVENTORY attempted to open MENU_SAVEQUIT outside its active state.");
        _screen.Close();
        _gameOver = false;
        _saveScreen.Open();
        _openMenu = OpenMenu.SaveQuit;
        ResetSaveDelay();
        _lifecycle.BeginFadeInFromWhite(this);
    }

    private void SelectSaveOption()
    {
        if (!_lifecycle.IsOpenFor(this) || _openMenu != OpenMenu.SaveQuit || _saveSelectionDelay)
            return;
        if (_saveScreen.SaveErrorVisible)
        {
            _saveScreen.ClearSaveError();
            return;
        }
        if (_saveScreen.Cursor != 0)
        {
            SaveRequests++;
            SaveResult result = _save();
            if (!result.Success)
            {
                LastSaveError = result.ErrorMessage;
                _saveScreen.ShowSaveError();
                return;
            }
            LastSaveError = null;
        }

        _playSound(OracleSoundEngine.SndSelectItem);
        _saveScreen.DelayCounter = SaveSelectionDelayFrames;
        _saveSelectionDelay = true;
        _saveDelayElapsed = 0;
        _saveDelayUpdates.Reset();
    }

    private bool CancelSaveMenu()
    {
        if (_gameOver ||
            !_lifecycle.IsOpenFor(this) ||
            _openMenu != OpenMenu.SaveQuit ||
            _saveSelectionDelay)
        {
            return false;
        }

        BeginClosing();
        return true;
    }

    private bool MoveSaveCursor(int direction)
    {
        if (!_lifecycle.IsOpenFor(this) ||
            _openMenu != OpenMenu.SaveQuit ||
            _saveSelectionDelay ||
            !_saveScreen.Move(direction))
        {
            return false;
        }

        _playSound(OracleSoundEngine.SndMenuMove);
        return true;
    }

    private void HandleDirectionInput()
    {
        if (Input.IsActionJustPressed("move_right"))
            MoveCursor(Vector2I.Right);
        else if (Input.IsActionJustPressed("move_left"))
            MoveCursor(Vector2I.Left);
        else if (Input.IsActionJustPressed("move_up"))
            MoveCursor(Vector2I.Up);
        else if (Input.IsActionJustPressed("move_down"))
            MoveCursor(Vector2I.Down);
    }

    private bool MoveCursor(Vector2I direction)
    {
        if (!_screen.MoveCursor(direction))
            return false;
        _playSound(OracleSoundEngine.SndMenuMove);
        return true;
    }

    private bool EquipToA()
    {
        if (!_screen.EquipToA())
            return false;
        _playSound(OracleSoundEngine.SndSelectItem);
        return true;
    }

    private bool EquipToB()
    {
        if (!_screen.EquipToB())
            return false;
        _playSound(OracleSoundEngine.SndSelectItem);
        return true;
    }

    private bool EquipSelectedRing()
    {
        if (!_screen.EquipSelectedRing())
            return false;
        _playSound(OracleSoundEngine.SndSelectItem);
        return true;
    }

    private void BeginClosing() => _lifecycle.BeginClosing(this);

    private void ResetSaveDelay()
    {
        _saveSelectionDelay = false;
        _saveDelayElapsed = 0;
        _saveDelayUpdates.Reset();
    }

    void IOracleMenuLifecycleClient.OpenAtWhite()
    {
        if (_openMenu == OpenMenu.SaveQuit)
        {
            _screen.Close();
            _saveScreen.Open(_gameOver);
        }
        else
        {
            _saveScreen.Close();
            // menuStateFadeIntoMenu requests SND_OPENMENU ($54) after the
            // fast fade reaches white, immediately before loading inventory.
            _playSound(OracleSoundEngine.SndOpenMenu);
            _screen.Open();
        }
    }

    void IOracleMenuLifecycleClient.CloseAtWhite()
    {
        _screen.Close();
        _saveScreen.Close();
    }

    void IOracleMenuLifecycleClient.LifecycleClosed()
    {
        _openMenu = OpenMenu.Inventory;
        _gameOver = false;
        ResetSaveDelay();
    }
}

internal enum OpenMenu
{
    Inventory,
    SaveQuit
}
