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

    private enum FadeDestination { None, FileSelect, Gameplay }

    private readonly MainMenuScreen _screen;
    private readonly Func<int, OracleSaveData?> _load;
    private readonly Action<int, OracleSaveData> _save;
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

    public bool IsActive { get; private set; } = true;
    internal MainMenuScreen.Page CurrentPage => _screen.CurrentPage;
    internal int Cursor => _screen.Cursor;

    public MainMenuController(
        MainMenuScreen screen,
        Action<int, OracleSaveData> startGame,
        Func<int, OracleSaveData?>? load = null,
        Action<int, OracleSaveData>? save = null,
        Action<int>? erase = null,
        Action<int>? playSound = null)
    {
        _screen = screen;
        _startGame = startGame;
        _load = load ?? OracleSaveStore.LoadSlot;
        _save = save ?? OracleSaveStore.SaveSlot;
        _erase = erase ?? OracleSaveStore.EraseSlot;
        _playSound = playSound;
        ReloadSlots();
        _screen.ShowTitle();
        _playSound?.Invoke(OracleSoundEngine.MusTitlescreen);
    }

    public void Update(double delta)
    {
        if (!IsActive)
            return;

        if (_fadeDestination != FadeDestination.None)
        {
            UpdateFade(delta);
            return;
        }

        if (_screen.CurrentPage == MainMenuScreen.Page.Title)
        {
            _titleTicks += delta * 60.0;
            _screen.SetTitleBlink((((int)_titleTicks >> 5) & 1) == 0);
            if (Input.IsActionJustPressed("inventory"))
                BeginTitleStart();
            return;
        }

        _menuTicks += delta * 60.0;
        _screen.SetActorFrame((((int)_menuTicks >> 4) & 1) != 0);

        Vector2I movement = Vector2I.Zero;
        if (Input.IsActionJustPressed("move_up")) movement = Vector2I.Up;
        else if (Input.IsActionJustPressed("move_down")) movement = Vector2I.Down;
        else if (Input.IsActionJustPressed("move_left")) movement = Vector2I.Left;
        else if (Input.IsActionJustPressed("move_right")) movement = Vector2I.Right;
        if (movement != Vector2I.Zero)
            Move(movement);

        if (Input.IsActionJustPressed("item"))
            Back();
        else if (Input.IsActionJustPressed("inventory") &&
            _screen.CurrentPage == MainMenuScreen.Page.NameEntry)
            CommitNameEntry();
        else if (Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("inventory"))
            Accept();
    }

    internal void OpenFileSelect()
    {
        ReloadSlots();
        _screen.ShowFileSelect();
    }

    internal void BeginTitleStart() => BeginFade(FadeDestination.FileSelect);

    internal void Move(Vector2I direction)
    {
        int cursor = _screen.Cursor;
        int choice = _screen.Choice;
        int nameCursor = _screen.NameCursor;
        int textSpeed = _screen.TextSpeed;
        switch (_screen.CurrentPage)
        {
            case MainMenuScreen.Page.FileSelect:
            case MainMenuScreen.Page.CopySource:
            case MainMenuScreen.Page.CopyDestination:
            case MainMenuScreen.Page.EraseSelect:
                if (direction.Y != 0)
                    _screen.SetCursor((_screen.Cursor + direction.Y + 4) & 3);
                else if (_screen.Cursor == 3 && direction.X != 0 &&
                    _screen.CurrentPage == MainMenuScreen.Page.FileSelect)
                    _screen.SetChoice(_screen.Choice ^ 1);
                break;
            case MainMenuScreen.Page.NewFileOptions:
                if (direction.Y != 0)
                    _screen.SetCursor((_screen.Cursor + direction.Y + 3) % 3);
                break;
            case MainMenuScreen.Page.NameEntry:
                _screen.MoveNameCursor(direction);
                break;
            case MainMenuScreen.Page.TextSpeed:
                if (direction.X != 0)
                    _screen.SetTextSpeed(Math.Clamp(_screen.TextSpeed + direction.X, 0, 4));
                break;
            case MainMenuScreen.Page.CopyConfirm:
            case MainMenuScreen.Page.EraseConfirm:
                if (direction.X != 0 || direction.Y != 0)
                    _screen.SetChoice(_screen.Choice ^ 1);
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
        _playSound?.Invoke(OracleSoundEngine.SndSelectItem);
        switch (_screen.CurrentPage)
        {
            case MainMenuScreen.Page.FileSelect:
                AcceptFileSelect();
                break;
            case MainMenuScreen.Page.NewFileOptions:
                if (_screen.Cursor == 0)
                    _screen.ShowNameEntry(_screen.SelectedSlot);
                else
                    _screen.ShowNotice(_screen.Cursor == 1
                        ? "SECRET ENTRY\nIS NOT YET SUPPORTED"
                        : "GAME LINK\nIS NOT AVAILABLE");
                break;
            case MainMenuScreen.Page.NameEntry:
                AcceptNameEntry();
                break;
            case MainMenuScreen.Page.TextSpeed:
                StartSelectedFile();
                break;
            case MainMenuScreen.Page.CopySource:
                AcceptCopySource();
                break;
            case MainMenuScreen.Page.CopyDestination:
                AcceptCopyDestination();
                break;
            case MainMenuScreen.Page.CopyConfirm:
                if (_screen.Choice == 1 && _sourceSlot >= 0)
                {
                    OracleSaveData source = _slots[_sourceSlot]!;
                    OracleSaveData.TryDeserialize(source.Serialize(), out OracleSaveData? copy);
                    _save(_screen.SelectedSlot, copy!);
                }
                OpenFileSelect();
                break;
            case MainMenuScreen.Page.EraseSelect:
                if (_screen.Cursor == 3)
                    OpenFileSelect();
                else if (_slots[_screen.Cursor] is not null)
                    _screen.ShowEraseConfirm(_screen.Cursor);
                break;
            case MainMenuScreen.Page.EraseConfirm:
                if (_screen.Choice == 1)
                    _erase(_screen.SelectedSlot);
                OpenFileSelect();
                break;
            case MainMenuScreen.Page.Notice:
                _screen.ShowNewFileOptions(_screen.SelectedSlot);
                break;
        }
    }

    internal void Back()
    {
        switch (_screen.CurrentPage)
        {
            case MainMenuScreen.Page.NewFileOptions:
            case MainMenuScreen.Page.TextSpeed:
            case MainMenuScreen.Page.CopySource:
            case MainMenuScreen.Page.EraseSelect:
                OpenFileSelect();
                break;
            case MainMenuScreen.Page.NameEntry:
                _screen.DeleteNameCharacter();
                break;
            case MainMenuScreen.Page.CopyDestination:
                _screen.ShowCopySource();
                break;
            case MainMenuScreen.Page.CopyConfirm:
                _screen.ShowCopyDestination(_sourceSlot);
                break;
            case MainMenuScreen.Page.EraseConfirm:
                _screen.ShowEraseSelect();
                break;
            case MainMenuScreen.Page.Notice:
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
            return;

        OracleSaveData save = OracleSaveData.CreateStandardGame();
        save.SetLinkName(_screen.EnteredName);
        _save(_screen.SelectedSlot, save);
        OpenFileSelect();
        _screen.SetCursor(_screen.SelectedSlot);
    }

    private void StartSelectedFile()
    {
        int slot = _screen.SelectedSlot;
        OracleSaveData save = _slots[slot]!;
        save.SetTextSpeed(_screen.TextSpeed);
        _save(slot, save);
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
            return;
        _sourceSlot = _screen.Cursor;
        _screen.ShowCopyDestination(_sourceSlot);
    }

    private void AcceptCopyDestination()
    {
        if (_screen.Cursor == 3)
        {
            _screen.ShowCopySource();
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
