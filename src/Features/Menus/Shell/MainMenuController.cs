using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Ports the usable fileSelectMode states: three files, Copy/Erase, standard
/// game creation, name entry, and the message-speed confirmation before play.
/// Secret and Game Link remain deterministic notices until those systems exist.
/// </summary>
public sealed class MainMenuController
{
    public const int WhiteFadeFrames = 32;

    private readonly MainMenuScreen _screen;
    private readonly Func<int, OracleSaveData?> _load;
    private readonly Func<int, OracleSaveData, SaveResult> _save;
    private readonly Action<int> _erase;
    private readonly Action<int, OracleSaveData> _startGame;
    private readonly Action<int>? _playSound;
    private readonly OracleSaveData?[] _slots = new OracleSaveData?[OracleSaveStore.SlotCount];
    private int _sourceSlot = -1;
    private double _titleTicks;
    private double _menuTicks;
    private double _fadeTicks;
    private FadeDestination _fadeDestination;
    private OracleSaveData? _pendingSave;
    private int _pendingSlot;
    private int _repeatKeys;
    private int _repeatCounter;
    private int _eraseTimer;
    private int _eraseHealth;
    private bool _erasing;
    private static readonly string[] ButtonActions =
        ["attack", "item", "map", "inventory", "move_right", "move_left", "move_up", "move_down"];

    public bool IsActive { get; private set; } = true;
    internal Page CurrentPage => _screen.CurrentPage;
    internal int Cursor => _screen.Cursor;
    internal string? LastSaveError { get; private set; }

    public MainMenuController(
        MainMenuScreen screen,
        Action<int, OracleSaveData> startGame,
        Func<int, OracleSaveData?>? load = null,
        Func<int, OracleSaveData, SaveResult>? save = null,
        Action<int>? erase = null,
        Action<int>? playSound = null,
        bool startAtFileSelect = false)
    {
        _screen = screen;
        _startGame = startGame;
        _load = load ?? OracleSaveStore.LoadSlot;
        _save = save ?? OracleSaveStore.SaveSlot;
        _erase = erase ?? OracleSaveStore.EraseSlot;
        _playSound = playSound;
        ReloadSlots();
        // FrontendIntroController transfers this shared screen at the end of
        // the title's 32-update fade, when its palette offset is fully white.
        // fileSelectMode initialization loads the file-select palettes at that
        // boundary, so the new owner must clear the inherited title offset.
        _screen.SetWhiteFade(0.0f);
        if (startAtFileSelect)
        {
            _screen.ShowFileSelect();
            _playSound?.Invoke(OracleSoundEngine.MusFileSelect);
        }
        else
        {
            _screen.ShowTitle();
            _playSound?.Invoke(OracleSoundEngine.MusTitlescreen);
        }
    }

    public void Update(double delta)
    {
        if (!IsActive)
            return;

        if (_screen.SaveErrorVisible)
        {
            if (Input.IsActionJustPressed("attack") ||
                Input.IsActionJustPressed("item") ||
                Input.IsActionJustPressed("inventory"))
            {
                _screen.ClearSaveError();
            }
            return;
        }

        if (_fadeDestination != FadeDestination.None)
        {
            UpdateFade(delta);
            return;
        }

        if (_screen.CurrentPage == Page.Title)
        {
            _titleTicks += delta * 60.0;
            _screen.SetTitleBlink((((int)_titleTicks >> 5) & 1) == 0);
            if (Input.IsActionJustPressed("inventory"))
                BeginTitleStart();
            return;
        }

        _menuTicks += delta * 60.0;
        _screen.SetActorFrame((((int)_menuTicks >> 4) & 1) != 0);

        if (_erasing)
        {
            // bank2.s:fileSelectMode4 @mode3 owns its own counter, separate
            // from wTmpcbb6 (the animated Link's clock).
            _eraseTimer = (_eraseTimer - 1) & 0xff;
            if ((_eraseTimer & 1) != 0) return;
            if (_eraseHealth == 0)
            {
                _erase(_screen.SelectedSlot);
                _erasing = false;
                OpenFileSelect();
                return;
            }
            _screen.SetEraseHealth(--_eraseHealth);
            if ((_eraseHealth & 3) == 0)
                _playSound?.Invoke(OracleSoundEngine.SndGainHeart);
            return;
        }

        int pressed = 0, held = 0;
        for (int bit = 0; bit < ButtonActions.Length; bit++)
        {
            if (Input.IsActionJustPressed(ButtonActions[bit])) pressed |= 1 << bit;
            if (Input.IsActionPressed(ButtonActions[bit])) held |= 1 << bit;
        }
        DispatchInput(pressed, held);
    }

    private void DispatchInput(int pressed, int held)
    {
        // These are distinct bank2.s dispatchers, with different priorities.
        // Navigation consumes the update even at a clamped endpoint.
        switch (_screen.CurrentPage)
        {
            case Page.NameEntry:
                UpdateNameInput(pressed, held);
                return;
            case Page.NewFileOptions:
                if ((pressed & 0x80) != 0) Move(Vector2I.Down);
                else if ((pressed & 0x40) != 0) Move(Vector2I.Up);
                else if ((pressed & 0x06) != 0) Back();
                else if ((pressed & 0x09) != 0) Accept();
                return;
            case Page.TextSpeed:
                if ((pressed & 0x06) != 0) Back();
                else if ((pressed & 0x10) != 0) Move(Vector2I.Right);
                else if ((pressed & 0x20) != 0) Move(Vector2I.Left);
                else if ((pressed & 0x09) != 0) Accept();
                return;
            case Page.CopyDestination:
            case Page.CopyConfirm:
            case Page.EraseConfirm:
                if ((pressed & 0x02) != 0) { Back(); return; }
                break;
            case Page.Notice:
                if ((pressed & 0x02) != 0) Back();
                else if ((pressed & 0x09) != 0) Accept();
                return;
        }
        if (_screen.CurrentPage is not (Page.CopyConfirm or Page.EraseConfirm))
        {
            bool moved = (pressed & 0xc0) != 0;
            if (moved) Move((pressed & 0x40) != 0 ? Vector2I.Up : Vector2I.Down);
            else if ((pressed & 0x09) != 0) { Accept(); return; }
            // fileSelectMode1 alone falls through to the bottom-row handler
            // after fileSelectUpdateInput, including a vertical move to Quit.
            if (_screen.CurrentPage != Page.FileSelect || _screen.Cursor != 3) return;
        }
        if (_screen.CurrentPage is Page.CopyConfirm or Page.EraseConfirm ||
            (_screen.CurrentPage == Page.FileSelect && _screen.Cursor == 3))
        {
            if ((pressed & 0x20) != 0) { Move(Vector2I.Left); return; }
            if ((pressed & 0x10) != 0) { Move(Vector2I.Right); return; }
        }
        if ((pressed & 0x09) != 0) Accept();
    }

    private void UpdateNameInput(int pressed, int held)
    {
        // bank0.s:getInputWithAutofire, then bank2.s:runTextInput's
        // highest-set-bit dispatch (Down, Up, Left, Right, Start, Select, B, A).
        int directions = held & 0xf0;
        if ((_repeatKeys & directions) == 0) _repeatCounter = 0;
        else if (++_repeatCounter >= 0x28)
        {
            _repeatCounter = (_repeatCounter & 0x1f) | 0x80;
            if ((_repeatCounter & 3) == 0) pressed = held;
        }
        _repeatKeys = directions;
        if (pressed == 0) return;
        int button = 7;
        while ((pressed & (1 << button)) == 0) button--;
        if (button >= 4)
        {
            _screen.MoveNameCursor(button switch
            {
                7 => Vector2I.Down, 6 => Vector2I.Up,
                5 => Vector2I.Left, _ => Vector2I.Right
            });
            _playSound?.Invoke(OracleSoundEngine.SndMenuMove);
        }
        else if (button == 1) Back();
        else if (button == 0) Accept();
        else
        {
            _playSound?.Invoke(OracleSoundEngine.SndSelectItem);
            // US Select deliberately only makes the selection sound.
            if (button == 3 && _screen.SelectNameOkay()) CommitNameEntry();
        }
    }

    internal void OpenFileSelect()
    {
        ReloadSlots();
        _screen.ShowFileSelect();
    }

    internal void BeginTitleStart()
    {
        _playSound?.Invoke(OracleSoundEngine.SndSelectItem);
        _playSound?.Invoke(OracleSoundEngine.SndCtrlFastFadeOut);
        BeginFade(FadeDestination.FileSelect);
    }

    internal void Move(Vector2I direction)
    {
        int cursor = _screen.Cursor;
        int choice = _screen.Choice;
        int nameCursor = _screen.NameCursor;
        int textSpeed = _screen.TextSpeed;
        switch (_screen.CurrentPage)
        {
            case Page.FileSelect:
            case Page.CopySource:
            case Page.CopyDestination:
            case Page.EraseSelect:
                if (direction.Y != 0)
                {
                    int next = (_screen.Cursor + direction.Y + 4) & 3;
                    if (_screen.CurrentPage == Page.CopyDestination && next == _sourceSlot)
                        next = (next + direction.Y + 4) & 3;
                    _screen.SetCursor(next);
                }
                else if (_screen.Cursor == 3 && direction.X != 0 &&
                    _screen.CurrentPage == Page.FileSelect)
                    _screen.SetChoice(direction.X < 0 ? 0 : 1);
                break;
            case Page.NewFileOptions:
                if (direction.Y != 0)
                    _screen.SetCursor((_screen.Cursor + direction.Y + 3) % 3);
                break;
            case Page.NameEntry:
                _screen.MoveNameCursor(direction);
                break;
            case Page.TextSpeed:
                if (direction.X != 0)
                    _screen.SetTextSpeed(Math.Clamp(_screen.TextSpeed + direction.X, 0, 4));
                break;
            case Page.CopyConfirm:
            case Page.EraseConfirm:
                if (direction.X != 0)
                    _screen.SetChoice(direction.X < 0 ? 0 : 1);
                break;
        }
        if (_screen.Cursor != cursor || _screen.Choice != choice ||
            _screen.NameCursor != nameCursor || _screen.TextSpeed != textSpeed)
        {
            _playSound?.Invoke(OracleSoundEngine.SndMenuMove);
        }
    }

    internal void Accept()
    {
        if (_erasing) return;
        if (_screen.SaveErrorVisible)
        {
            _screen.ClearSaveError();
            return;
        }
        if (_screen.CurrentPage != Page.EraseConfirm)
            _playSound?.Invoke(OracleSoundEngine.SndSelectItem);
        switch (_screen.CurrentPage)
        {
            case Page.FileSelect:
                AcceptFileSelect();
                break;
            case Page.NewFileOptions:
                if (_screen.Cursor == 0)
                {
                    _repeatKeys = _repeatCounter = 0;
                    _screen.ShowNameEntry(_screen.SelectedSlot);
                }
                else
                    _screen.ShowNotice(_screen.Cursor == 1
                        ? "SECRET ENTRY\nIS NOT YET SUPPORTED"
                        : "GAME LINK\nIS NOT AVAILABLE");
                break;
            case Page.NameEntry:
                AcceptNameEntry();
                break;
            case Page.TextSpeed:
                StartSelectedFile();
                break;
            case Page.CopySource:
                AcceptCopySource();
                break;
            case Page.CopyDestination:
                AcceptCopyDestination();
                break;
            case Page.CopyConfirm:
                if (_screen.Choice == 1 && _sourceSlot >= 0)
                {
                    OracleSaveData source = _slots[_sourceSlot]!;
                    OracleSaveData.TryDeserialize(source.Serialize(), out OracleSaveData? copy);
                    if (!TrySave(_screen.SelectedSlot, copy!))
                        break;
                }
                OpenFileSelect();
                break;
            case Page.EraseSelect:
                if (_screen.Cursor == 3)
                    OpenFileSelect();
                else
                    _screen.ShowEraseConfirm(_screen.Cursor);
                break;
            case Page.EraseConfirm:
                if (_screen.Choice == 1)
                {
                    _erasing = true;
                    _eraseHealth = _slots[_screen.SelectedSlot]?.MaxHealthQuarters ?? 0;
                    _screen.SetEraseHealth(_eraseHealth);
                }
                else OpenFileSelect();
                break;
            case Page.Notice:
                _screen.ShowNewFileOptions(_screen.SelectedSlot);
                break;
        }
    }

    internal void Back()
    {
        if (_erasing) return;
        if (_screen.SaveErrorVisible)
        {
            _screen.ClearSaveError();
            return;
        }
        switch (_screen.CurrentPage)
        {
            case Page.NewFileOptions:
                OpenFileSelect();
                break;
            case Page.TextSpeed:
                // @textSpeedMenu_checkInput decrements only the substate;
                // it does not reinitialize the selected file cursor.
                _screen.ShowFileSelect();
                _screen.SetCursor(_screen.SelectedSlot);
                break;
            case Page.NameEntry:
                _playSound?.Invoke(OracleSoundEngine.SndClink);
                _screen.DeleteNameCharacter();
                break;
            case Page.CopyDestination:
                _playSound?.Invoke(OracleSoundEngine.SndClink);
                _screen.ShowCopySource();
                _screen.SetCursor(_sourceSlot);
                break;
            case Page.CopyConfirm:
                _playSound?.Invoke(OracleSoundEngine.SndClink);
                _screen.ShowCopyDestination(_sourceSlot);
                _screen.SetCursor(_screen.SelectedSlot);
                break;
            case Page.EraseConfirm:
                _playSound?.Invoke(OracleSoundEngine.SndClink);
                _screen.ShowEraseSelect();
                _screen.SetCursor(_screen.SelectedSlot);
                break;
            case Page.Notice:
                _screen.ShowNewFileOptions(_screen.SelectedSlot);
                break;
        }
    }

    private void AcceptFileSelect()
    {
        if (_screen.Cursor == 3)
        {
            if (_screen.Choice == 0)
                _screen.ShowCopySource();
            else
                _screen.ShowEraseSelect();
            return;
        }

        int slot = _screen.Cursor;
        _screen.SetSelectedSlot(slot);
        OracleSaveData? save = _slots[slot];
        if (save is null)
            _screen.ShowNewFileOptions(slot);
        else
            _screen.ShowTextSpeed(slot, save.TextSpeed);
    }

    private void AcceptNameEntry()
    {
        if (_screen.TryGetSelectedNameCharacter(out char character))
        {
            _screen.AppendNameCharacter(character);
            return;
        }

        switch (_screen.NameLowerChoice)
        {
            case 0: _screen.MoveNameEntryPosition(-1); break;
            case 1: _screen.MoveNameEntryPosition(1); break;
            case 2: CommitNameEntry(); break;
        }
    }

    private void CommitNameEntry()
    {
        if (_screen.EnteredName.Length == 0)
        {
            OpenFileSelect();
            return;
        }

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        save.SetLinkName(_screen.EnteredName);
        if (!TrySave(_screen.SelectedSlot, save))
            return;
        OpenFileSelect();
    }

    private void StartSelectedFile()
    {
        int slot = _screen.SelectedSlot;
        OracleSaveData save = _slots[slot]!;
        save.SetTextSpeed(_screen.TextSpeed);
        if (!TrySave(slot, save))
            return;
        _pendingSlot = slot;
        _pendingSave = save;
        BeginFade(FadeDestination.Gameplay);
    }

    private void AcceptCopySource()
    {
        if (_screen.Cursor == 3)
        {
            OpenFileSelect();
            return;
        }
        if (_slots[_screen.Cursor] is null)
        {
            _playSound?.Invoke(OracleSoundEngine.SndError);
            return;
        }
        _sourceSlot = _screen.Cursor;
        _screen.ShowCopyDestination(_sourceSlot);
    }

    private void AcceptCopyDestination()
    {
        if (_screen.Cursor == 3)
        {
            Back();
            return;
        }
        if (_screen.Cursor == _sourceSlot)
            return;
        _screen.ShowCopyConfirm(_screen.Cursor);
    }

    private void ReloadSlots()
    {
        for (int slot = 0; slot < _slots.Length; slot++)
            _slots[slot] = _load(slot);
        _screen.SetSlots(_slots);
    }

    private bool TrySave(int slot, OracleSaveData save)
    {
        SaveResult result = _save(slot, save);
        if (result.Success)
        {
            LastSaveError = null;
            return true;
        }
        LastSaveError = result.ErrorMessage;
        _screen.ShowSaveError();
        return false;
    }

    private void BeginFade(FadeDestination destination)
    {
        _fadeDestination = destination;
        _fadeTicks = 0.0;
        _screen.SetWhiteFade(0.0f);
    }

    private void UpdateFade(double delta)
    {
        _fadeTicks = Math.Min(WhiteFadeFrames, _fadeTicks + delta * 60.0);
        _screen.SetWhiteFade((float)(_fadeTicks / WhiteFadeFrames));
        if (_fadeTicks < WhiteFadeFrames)
            return;

        FadeDestination destination = _fadeDestination;
        _fadeDestination = FadeDestination.None;
        _screen.SetWhiteFade(0.0f);
        if (destination == FadeDestination.FileSelect)
        {
            _playSound?.Invoke(OracleSoundEngine.MusFileSelect);
            OpenFileSelect();
            return;
        }

        IsActive = false;
        _startGame(_pendingSlot, _pendingSave!);
    }
}

internal enum FadeDestination
{
    None,
    FileSelect,
    Gameplay
}
