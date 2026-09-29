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
    private readonly Action<FileMenuInitialization>? _initialize;
    private readonly Action<Action> _present;
    private FileMenuInitialization? _pendingInitialization;
    private Page? _loadingPage;
    private int _pageRevision;
    private int _textSpeed;
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
    internal Page CurrentPage => _pendingInitialization switch
    {
        FileMenuInitialization.NewFileOptions => Page.NewFileOptions,
        FileMenuInitialization.NameEntry => Page.NameEntry,
        FileMenuInitialization.FileSelect => Page.FileSelect,
        _ => _loadingPage ?? _screen.CurrentPage
    };
    internal int Cursor => _screen.Cursor;
    internal string? LastSaveError { get; private set; }
    internal OracleSaveData? LoadedSlot(int slot) => _slots[slot];
    internal string RawEnteredName => _screen.RawEnteredName;
    internal bool PaletteWorkPending { get; private set; }

    public MainMenuController(
        MainMenuScreen screen,
        Action<int, OracleSaveData> startGame,
        Func<int, OracleSaveData?>? load = null,
        Func<int, OracleSaveData, SaveResult>? save = null,
        Action<int>? erase = null,
        Action<int>? playSound = null,
        bool startAtFileSelect = false,
        Action<FileMenuInitialization>? initialize = null,
        Action<Action>? present = null)
    {
        _screen = screen;
        _startGame = startGame;
        _load = load ?? OracleSaveStore.LoadSlot;
        _save = save ?? OracleSaveStore.SaveSlot;
        _erase = erase ?? OracleSaveStore.EraseSlot;
        _playSound = playSound;
        _initialize = initialize;
        _present = present ?? (action => action());
        ReloadSlots();
        // FrontendIntroController transfers this shared screen at the end of
        // the title's 32-update fade, when its palette offset is fully white.
        // fileSelectMode initialization loads the file-select palettes at that
        // boundary, so the new owner must clear the inherited title offset.
        if (startAtFileSelect)
        {
            PresentPage(Page.FileSelect, () =>
            {
                _screen.SetWhiteFade(0.0f);
                _screen.ShowFileSelect();
            });
            _playSound?.Invoke(SoundId.MusFileSelect);
        }
        else
        {
            PresentPage(Page.Title, () =>
            {
                _screen.SetWhiteFade(0.0f);
                _screen.ShowTitle();
            });
            _playSound?.Invoke(SoundId.MusTitlescreen);
        }
    }

    private void PresentPage(Page page, Action show)
    {
        int revision = ++_pageRevision;
        _loadingPage = page;
        _present(() =>
        {
            show();
            if (_pageRevision == revision) _loadingPage = null;
        });
    }

    public void Update(double delta)
    {
        PaletteWorkPending = false;
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

        if (CurrentPage != Page.Title || _pendingInitialization is not null)
        {
            // b2_fileSelectScreen increments wTmpcbb6 before mode dispatch,
            // including the modes' initialization updates.
            _menuTicks += delta * 60.0;
            _screen.SetActorFrame((((int)_menuTicks >> 4) & 1) != 0);
        }

        if (_pendingInitialization is { } initialization)
        {
            _pendingInitialization = null;
            // bank2.s:fileSelectMode5 state 0 performs the complete screen
            // load and then increments mode2. It never dispatches this poll's
            // keys to state 1, even if A/Start was released and pressed again.
            switch (initialization)
            {
                case FileMenuInitialization.NewFileOptions:
                    PresentPage(Page.NewFileOptions, () => _screen.ShowNewFileOptions(_screen.SelectedSlot));
                    break;
                case FileMenuInitialization.NameEntry:
                    _repeatKeys = _repeatCounter = 0;
                    PresentPage(Page.NameEntry, () => _screen.ShowNameEntry(_screen.SelectedSlot));
                    break;
                case FileMenuInitialization.NameCommit:
                    SaveEnteredName();
                    break;
                case FileMenuInitialization.FileSelect:
                    ReloadSlots();
                    PresentPage(Page.FileSelect, _screen.ShowFileSelect);
                    break;
                default: throw new InvalidOperationException($"Unsupported file-menu initialization {initialization}.");
            }
            _initialize?.Invoke(initialization);
            return;
        }

        if (CurrentPage == Page.Title)
        {
            _titleTicks += delta * 60.0;
            _screen.SetTitleBlink((((int)_titleTicks >> 5) & 1) == 0);
            if (Input.IsActionJustPressed("inventory"))
                BeginTitleStart();
            return;
        }

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
                _playSound?.Invoke(SoundId.SndGainHeart);
            return;
        }

        int pressed = 0, held = 0;
        for (int bit = 0; bit < ButtonActions.Length; bit++)
        {
            if (Input.IsActionJustPressed(ButtonActions[bit])) pressed |= 1 << bit;
            if (Input.IsActionPressed(ButtonActions[bit])) held |= 1 << bit;
        }
        Page inputPage = CurrentPage;
        DispatchInput(pressed, held);
        // fileSelectMode1 state 2 adds this sprite even on its Back path.
        if (inputPage == Page.TextSpeed) PresentTextSpeedCursor();
        else _present(() => _screen.SetTextSpeedCursorVisible(false));
    }

    private void DispatchInput(int pressed, int held)
    {
        // These are distinct bank2.s dispatchers, with different priorities.
        // Navigation consumes the update even at a clamped endpoint.
        switch (CurrentPage)
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
        if (CurrentPage is not (Page.CopyConfirm or Page.EraseConfirm))
        {
            bool moved = (pressed & 0xc0) != 0;
            if (moved) Move((pressed & 0x40) != 0 ? Vector2I.Up : Vector2I.Down);
            else if ((pressed & 0x09) != 0) { Accept(); return; }
            // fileSelectMode1 alone falls through to the bottom-row handler
            // after fileSelectUpdateInput, including a vertical move to Quit.
            if (CurrentPage != Page.FileSelect || _screen.Cursor != 3) return;
        }
        if (CurrentPage is Page.CopyConfirm or Page.EraseConfirm ||
            (CurrentPage == Page.FileSelect && _screen.Cursor == 3))
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
            _playSound?.Invoke(SoundId.SndMenuMove);
        }
        else if (button == 1) Back();
        else if (button == 0) Accept();
        else
        {
            _playSound?.Invoke(SoundId.SndSelectItem);
            // US Select deliberately only makes the selection sound.
            if (button == 3 && _screen.SelectNameOkay()) CommitNameEntry();
        }
    }

    internal void OpenFileSelect()
    {
        _pendingInitialization = FileMenuInitialization.FileSelect;
    }

    internal void BeginTitleStart()
    {
        _playSound?.Invoke(SoundId.SndSelectItem);
        _playSound?.Invoke(SoundId.SndCtrlFastFadeOut);
        BeginFade(FadeDestination.FileSelect);
    }

    internal void Move(Vector2I direction)
    {
        if (_pendingInitialization is not null) return;
        int cursor = _screen.Cursor;
        int choice = _screen.Choice;
        int nameCursor = _screen.NameCursor;
        int textSpeed = _textSpeed;
        switch (CurrentPage)
        {
            case Page.FileSelect:
            case Page.CopySource:
            case Page.CopyDestination:
            case Page.EraseSelect:
                if (direction.Y != 0)
                {
                    int next = (_screen.Cursor + direction.Y + 4) & 3;
                    if (CurrentPage == Page.CopyDestination && next == _sourceSlot)
                        next = (next + direction.Y + 4) & 3;
                    _screen.SetCursor(next);
                }
                else if (_screen.Cursor == 3 && direction.X != 0 &&
                    CurrentPage == Page.FileSelect)
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
                    _textSpeed = Math.Clamp(_textSpeed + direction.X, 0, 4);
                PresentTextSpeedCursor();
                break;
            case Page.CopyConfirm:
            case Page.EraseConfirm:
                if (direction.X != 0)
                    _screen.SetChoice(direction.X < 0 ? 0 : 1);
                break;
        }
        if (_screen.Cursor != cursor || _screen.Choice != choice ||
            _screen.NameCursor != nameCursor || _textSpeed != textSpeed)
        {
            _playSound?.Invoke(SoundId.SndMenuMove);
        }
    }

    internal void Accept()
    {
        if (_erasing || _pendingInitialization is not null) return;
        if (_screen.SaveErrorVisible)
        {
            _screen.ClearSaveError();
            return;
        }
        if (CurrentPage != Page.EraseConfirm)
            _playSound?.Invoke(SoundId.SndSelectItem);
        switch (CurrentPage)
        {
            case Page.FileSelect:
                AcceptFileSelect();
                break;
            case Page.NewFileOptions:
                if (_screen.Cursor == 0)
                {
                    _pendingInitialization = FileMenuInitialization.NameEntry;
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
        if (_erasing || _pendingInitialization is not null) return;
        if (_screen.SaveErrorVisible)
        {
            _screen.ClearSaveError();
            return;
        }
        switch (CurrentPage)
        {
            case Page.NewFileOptions:
                OpenFileSelect();
                break;
            case Page.TextSpeed:
                // @textSpeedMenu_checkInput decrements only the substate;
                // it does not reinitialize the selected file cursor.
                PresentPage(Page.FileSelect, _screen.RestoreFileSelect);
                break;
            case Page.NameEntry:
                _playSound?.Invoke(SoundId.SndClink);
                _screen.DeleteNameCharacter();
                break;
            case Page.CopyDestination:
                _playSound?.Invoke(SoundId.SndClink);
                _screen.ShowCopySource();
                _screen.SetCursor(_sourceSlot);
                break;
            case Page.CopyConfirm:
                _playSound?.Invoke(SoundId.SndClink);
                _screen.ShowCopyDestination(_sourceSlot);
                _screen.SetCursor(_screen.SelectedSlot);
                break;
            case Page.EraseConfirm:
                _playSound?.Invoke(SoundId.SndClink);
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
            _pendingInitialization = FileMenuInitialization.NewFileOptions;
        else
        {
            _textSpeed = save.TextSpeed;
            int speed = _textSpeed;
            PresentPage(Page.TextSpeed, () => _screen.ShowTextSpeed(slot, speed, cursorVisible: false));
            _initialize?.Invoke(FileMenuInitialization.SelectFile);
        }
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

    private void PresentTextSpeedCursor()
    {
        int speed = _textSpeed;
        _present(() =>
        {
            _screen.SetTextSpeed(speed);
            _screen.SetTextSpeedCursorVisible(true);
        });
    }

    private void CommitNameEntry()
    {
        _pendingInitialization = FileMenuInitialization.NameCommit;
    }

    private void SaveEnteredName()
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
        save.SetTextSpeed(_textSpeed);
        if (!TrySave(slot, save))
            return;
        _pendingSlot = slot;
        _pendingSave = save;
        BeginFade(FadeDestination.Gameplay);
        _initialize?.Invoke(FileMenuInitialization.StartFile);
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
            _playSound?.Invoke(SoundId.SndError);
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
        PaletteWorkPending = true;
        // THREAD_3 runs after the file thread: its first palette step is
        // already offset $01 on the update that requests fadeoutToWhite.
        _present(() => _screen.SetWhiteFade(1.0f / WhiteFadeFrames));
    }

    private void UpdateFade(double delta)
    {
        if (_fadeTicks < WhiteFadeFrames)
        {
            _fadeTicks = Math.Min(WhiteFadeFrames, _fadeTicks + delta * 60.0);
            PaletteWorkPending = _fadeTicks < WhiteFadeFrames - 1;
            float fade = (float)((_fadeTicks + 1) / WhiteFadeFrames);
            _present(() => _screen.SetWhiteFade(fade));
            // paletteFadeHandler01 stops on the $20 boundary without a
            // palette write. The earlier file thread sees completion next update.
            return;
        }

        FadeDestination destination = _fadeDestination;
        _fadeDestination = FadeDestination.None;
        if (destination == FadeDestination.FileSelect)
        {
            _playSound?.Invoke(SoundId.MusFileSelect);
            OpenFileSelect();
            return;
        }

        IsActive = false;
        _startGame(_pendingSlot, _pendingSave!);
    }
}

public enum FileMenuInitialization
{
    NewFileOptions,
    NameEntry,
    NameCommit,
    FileSelect,
    SelectFile,
    StartFile
}

internal enum FadeDestination
{
    None,
    FileSelect,
    Gameplay
}
