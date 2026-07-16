using Godot;
using System;

namespace oracleofages;

/// <summary>
/// MENU_INVENTORY and MENU_SAVEQUIT-specific behavior. Their common
/// wMenuLoadState fade/pause lifecycle is owned by OracleMenuLifecycle.
/// </summary>
public sealed class InventoryMenuController : IOracleMenuLifecycleClient
{
    private enum OpenMenu { Inventory, SaveQuit }

    public const float FastFadeFrames = OracleMenuLifecycle.FastFadeUpdates;
    public const int SaveSelectionDelayFrames = 30;

    private readonly InventoryScreen _screen;
    private readonly SaveQuitScreen _saveScreen;
    private readonly OracleMenuLifecycle _lifecycle;
    private readonly Func<bool> _canOpen;
    private readonly Func<OracleSaveStore.SaveResult> _save;
    private readonly Action _quitToTitle;
    private readonly FixedUpdateAccumulator _saveDelayUpdates = new();
    private OpenMenu _openMenu;
    private bool _saveSelectionDelay;
    private int _saveDelayElapsed;

    public bool IsActive => _lifecycle.IsOwnedBy(this);
    public bool IsOpen => _lifecycle.IsOpenFor(this) && !_saveSelectionDelay;
    public bool SaveMenuOpen =>
        _lifecycle.IsOpenFor(this) && _openMenu == OpenMenu.SaveQuit;
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
        Func<bool> canOpen,
        Func<OracleSaveStore.SaveResult> save,
        Action quitToTitle)
    {
        _screen = screen;
        _saveScreen = saveScreen;
        _lifecycle = lifecycle;
        _canOpen = canOpen;
        _save = save;
        _quitToTitle = quitToTitle;
    }

    public void Update(double delta)
    {
        if (!IsActive)
        {
            bool inventoryPressed = Input.IsActionJustPressed("inventory");
            bool mapPressed = Input.IsActionJustPressed("map");
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
    internal void BeginClosingForValidation() => BeginClosing();
    internal void OpenSaveMenuFromInventoryForValidation() => OpenSaveMenuFromInventory();
    internal void SelectSaveOptionForValidation() => SelectSaveOption();

    internal void OpenImmediatelyForValidation()
    {
        _openMenu = OpenMenu.Inventory;
        ResetSaveDelay();
        _lifecycle.OpenImmediately(this);
    }

    internal void OpenSaveImmediatelyForValidation()
    {
        _openMenu = OpenMenu.SaveQuit;
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
        ResetSaveDelay();
        _lifecycle.TryBeginOpening(this);
    }

    private void OpenSaveMenuFromInventory()
    {
        if (!_lifecycle.IsOpenFor(this) || _openMenu != OpenMenu.Inventory)
            throw new InvalidOperationException(
                "MENU_INVENTORY attempted to open MENU_SAVEQUIT outside its active state.");
        _screen.Close();
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
            OracleSaveStore.SaveResult result = _save();
            if (!result.Success)
            {
                LastSaveError = result.ErrorMessage;
                _saveScreen.ShowSaveError();
                return;
            }
            LastSaveError = null;
        }

        _saveScreen.DelayCounter = SaveSelectionDelayFrames;
        _saveSelectionDelay = true;
        _saveDelayElapsed = 0;
        _saveDelayUpdates.Reset();
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
            _saveScreen.Open();
        }
        else
        {
            _saveScreen.Close();
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
        ResetSaveDelay();
    }
}
