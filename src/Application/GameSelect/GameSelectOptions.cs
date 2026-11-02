
namespace Oracles;

internal partial class GameSelectOptions : Control
{
    private Func<int, OracleGame, OracleSaveData?> _load = null!;
    private Func<int, OracleSaveData, SaveResult> _store = null!;
    private Control _board = null!;
    private TextureButton[] _buttons = null!;
    private FileDialog _files = null!;
    private Control _replace = null!;
    private Control _notice = null!;
    private Texture2D? _background;
    private Texture2D _font = null!;
    private Texture2D _acorn = null!;
    private Color _textColor;
    private Color _woodInk;
    private Color _noticeColor;
    private OracleGame _game;
    private int _slot;
    private int _cursor;
    private int _replaceChoice;
    private string _slotName = string.Empty;
    private string[] _noticeLines = Array.Empty<string>();
    private int _noticePage;
    private bool _exporting;
    private bool _pickingFile;
    private bool _suppressInput;
    private OracleSaveData? _pending;
    internal string Status { get; private set; } = string.Empty;
    internal int SelectedSlot => _slot;
    internal Texture2D Background => _background!;
    internal event Action<int>? SoundRequested;

    public override void _Ready()
    {
        _board = GetNode<Control>("Board");
        _buttons = new[] { _board.GetNode<TextureButton>("Slot"), _board.GetNode<TextureButton>("Import"),
            _board.GetNode<TextureButton>("Export"), _board.GetNode<TextureButton>("Back") };
        _files = GetNode<FileDialog>("Files");
        _replace = _board.GetNode<Control>("Replace");
        _notice = _board.GetNode<Control>("Notice");
        for (int index = 0; index < _buttons.Length; index++)
        {
            int button = index;
            _buttons[index].MouseEntered += () => Select(button);
            _buttons[index].Pressed += () => Activate(button);
        }
        _board.GetNode<TextureButton>("PreviousSlot").MouseEntered += () => Select(0);
        _board.GetNode<TextureButton>("NextSlot").MouseEntered += () => Select(0);
        _board.GetNode<TextureButton>("PreviousSlot").Pressed += () => ChangeSlot(-1);
        _board.GetNode<TextureButton>("NextSlot").Pressed += () => ChangeSlot(1);
        _replace.GetNode<TextureButton>("Import").Pressed += ConfirmImport;
        _replace.GetNode<TextureButton>("Cancel").Pressed += () =>
        { SoundRequested?.Invoke(SoundId.SndSelectItem); CancelImport(); };
        _replace.GetNode<TextureButton>("Import").MouseEntered += () => SelectReplacement(0);
        _replace.GetNode<TextureButton>("Cancel").MouseEntered += () => SelectReplacement(1);
        _notice.GetNode<TextureButton>("Dismiss").Pressed += AdvanceNotice;
        _files.FileSelected += SelectFile;
        _files.Canceled += () => { _pickingFile = false; _pending = null; _suppressInput = true; };
        Resized += ResizeBoard;
        GetViewport().SizeChanged += ResizeBoard;
    }

    internal void Configure(Func<int, OracleGame, OracleSaveData?> load,
        Func<int, OracleSaveData, SaveResult> store)
    {
        _load = load;
        _store = store;
    }

    internal void Open(OracleGame game)
    {
        if (_background is null || _game != game)
        {
            string root = OracleAssetPack.ForGame(game);
            _background = new NativeMenuPresentation(root).BuildSaveTransfer();
            _font = OracleTileRenderer.BuildMonochromeFontTexture(root + "gfx/gfx_font.png");
            Color[,] palette = OracleGraphicsData.LoadPalette(root + "menu/palette_file_bg.bin");
            _textColor = palette[6, 0];
            _woodInk = palette[3, 3];
            _noticeColor = palette[2, 3];
            MenuOamPart acorn = FileMenuPresentation.LoadOam(root)["acorn-cursor"][0];
            _acorn = OracleTileRenderer.GetOamCellTexture(
                OracleGraphicsCache.LoadImage(root + "menu/spr_fileselect_decorations.png"),
                acorn.Tile - 0x20, (byte)acorn.Attributes,
                OracleGraphicsData.LoadPalette(root + "menu/palette_file_sprites.bin"),
                sourceGrayscaleInverted: false);
        }
        _game = game;
        _slot = 0;
        _cursor = 0;
        _suppressInput = false;
        _notice.Hide();
        Status = string.Empty;
        RefreshSlot();
        ResizeBoard();
        Show();
    }

    internal void Close()
    {
        _files.Hide();
        _replace.Hide();
        _notice.Hide();
        _pending = null;
        _pickingFile = false;
        Hide();
    }

    private void ResizeBoard()
    {
        float pixelsPerUnit = GetWindow().Size.X / GetViewportRect().Size.X;
        float scale = Math.Max(1, Mathf.Floor(Math.Min(Size.X / 160, Size.Y / 144) * pixelsPerUnit)) / pixelsPerUnit;
        _board.Scale = Vector2.One * scale;
        _board.Position = ((Size - new Vector2(160, 144) * scale) * pixelsPerUnit / 2).Round() / pixelsPerUnit;
        QueueRedraw();
    }

    internal void AdvanceFrame()
    {
        if (_suppressInput) { _suppressInput = false; return; }
        if (_pickingFile) return;
        bool cancel = Input.IsActionJustPressed("item");
        bool confirm = Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("inventory");
        if (_notice.Visible)
        {
            if (cancel) { _notice.Hide(); QueueRedraw(); }
            else if (confirm) AdvanceNotice();
            return;
        }
        if (_replace.Visible)
        {
            if (cancel) CancelImport();
            else if (confirm)
            {
                if (_replaceChoice == 0) ConfirmImport();
                else { SoundRequested?.Invoke(SoundId.SndSelectItem); CancelImport(); }
            }
            else if (Input.IsActionJustPressed("move_left") || Input.IsActionJustPressed("move_right"))
                SelectReplacement(_replaceChoice ^ 1);
            return;
        }
        if (cancel) { Close(); return; }
        int direction = (Input.IsActionJustPressed("move_down") ? 1 : 0) -
            (Input.IsActionJustPressed("move_up") ? 1 : 0);
        Select((_cursor + direction + _buttons.Length) % _buttons.Length);
        if (_cursor == 0)
        {
            int slotDirection = (Input.IsActionJustPressed("move_right") ? 1 : 0) -
                (Input.IsActionJustPressed("move_left") ? 1 : 0);
            if (slotDirection != 0) ChangeSlot(slotDirection);
        }
        QueueRedraw();
        if (confirm) Activate(_cursor);
    }

    private void ChangeSlot(int direction)
    {
        if (_pickingFile || _replace.Visible || _notice.Visible) return;
        _cursor = 0;
        _slot = (_slot + direction + OracleSaveStore.SlotCount) % OracleSaveStore.SlotCount;
        SoundRequested?.Invoke(SoundId.SndMenuMove);
        RefreshSlot();
    }

    private void Select(int cursor)
    {
        if (_cursor == cursor) return;
        _cursor = cursor;
        SoundRequested?.Invoke(SoundId.SndMenuMove);
        QueueRedraw();
    }

    private void SelectReplacement(int choice)
    {
        if (_replaceChoice == choice) return;
        _replaceChoice = choice;
        SoundRequested?.Invoke(SoundId.SndMenuMove);
        QueueRedraw();
    }

    private void RefreshSlot()
    {
        _slotName = _load(_slot, _game)?.LinkName ?? "EMPTY";
        QueueRedraw();
    }

    private void Activate(int button)
    {
        if (_pickingFile || _replace.Visible || _notice.Visible) return;
        _cursor = button;
        if (button == 0) { ChangeSlot(1); return; }
        SoundRequested?.Invoke(SoundId.SndSelectItem);
        if (button == 3) { Close(); return; }
        _exporting = button == 2;
        _pending = _exporting ? _load(_slot, _game) : null;
        if (_exporting && _pending is null)
        {
            ShowStatus("This slot has no readable save to export.");
            return;
        }
        _files.Title = _exporting ? "Export Save" : "Import Save";
        _files.FileMode = _exporting ? FileDialog.FileModeEnum.SaveFile : FileDialog.FileModeEnum.OpenFile;
        _files.CurrentFile = _exporting ? $"oracle_of_{_game.ToString().ToLowerInvariant()}_slot_{_slot + 1}.sav" : "";
        _pickingFile = true;
        _files.PopupCenteredClamped(new Vector2I(640, 400));
    }

    private void SelectFile(string path)
    {
        _pickingFile = false;
        _suppressInput = true;
        if (_exporting)
        {
            SaveResult exported = OracleSaveStore.Export(_pending!, path);
            _pending = null;
            ShowStatus(exported.Success ? "Save exported." : exported.ErrorMessage);
            return;
        }
        SaveResult imported = OracleSaveStore.ReadImport(path, _game, out _pending);
        if (!imported.Success) { ShowStatus(imported.ErrorMessage); return; }
        _replaceChoice = 0;
        _replace.Show();
        QueueRedraw();
    }

    private void CancelImport()
    {
        _replace.Hide();
        _pending = null;
        _suppressInput = true;
        QueueRedraw();
    }

    private void ConfirmImport()
    {
        if (_pending is null) return;
        SoundRequested?.Invoke(SoundId.SndSelectItem);
        _replace.Hide();
        SaveResult result = _store(_slot, _pending);
        _pending = null;
        _suppressInput = true;
        ShowStatus(result.Success ? "Save imported." : result.ErrorMessage);
        RefreshSlot();
    }

    private void ShowStatus(string text)
    {
        Status = text;
        var lines = new List<string>();
        string remaining = text.Replace('\n', ' ');
        while (remaining.Length > 16)
        {
            int split = remaining.LastIndexOf(' ', 16);
            if (split <= 0) split = 16;
            lines.Add(remaining[..split]);
            remaining = remaining[split..].TrimStart();
        }
        lines.Add(remaining);
        _noticeLines = lines.ToArray();
        _noticePage = 0;
        _notice.Show();
        QueueRedraw();
    }

    private void AdvanceNotice()
    {
        SoundRequested?.Invoke(SoundId.SndSelectItem);
        _noticePage += 3;
        if (_noticePage >= _noticeLines.Length) _notice.Hide();
        _suppressInput = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_background is null) return;
        DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);
        DrawSetTransform(_board.Position, scale: _board.Scale);
        DrawTexture(_background, Vector2.Zero);
        DrawText(_game == OracleGame.Ages ? "AGES" : "SEASONS", 28);
        DrawText($"{_slot + 1} {_slotName}", 48, color: _woodInk);
        if (!_replace.Visible && !_notice.Visible)
        {
            DrawTexture(_acorn, new Vector2(33, 48 + _cursor * 24));
            DrawText("<", 48, left: 16);
            DrawText(">", 48, left: 136);
        }
        if (_replace.Visible || _notice.Visible)
            DrawRect(new Rect2(16, 44, 128, 96), _noticeColor);
        if (_replace.Visible)
        {
            DrawText($"Import {_pending!.LinkName}?", 48);
            DrawText($"To file {_slot + 1}.", 64);
            DrawText("Replace save?", 80);
            DrawText("YES", 104, left: 56);
            DrawText("NO", 104, left: 104);
            DrawTexture(_acorn, new Vector2(_replaceChoice == 0 ? 40 : 88, 104));
        }
        if (_notice.Visible)
        {
            for (int line = 0; line < 3 && _noticePage + line < _noticeLines.Length; line++)
                DrawText(_noticeLines[_noticePage + line], 48 + line * 16, left: 16);
            DrawText(_noticePage + 3 < _noticeLines.Length ? "A:NEXT B:BACK" : "A/B:BACK", 104);
        }
    }

    private void DrawText(string text, int top, int? left = null, Color? color = null)
    {
        int pen = left ?? (160 - text.Length * 8) / 2;
        foreach (char letter in text)
        {
            int code = letter < 128 ? letter : '?';
            if (code != ' ')
                DrawTextureRectRegion(_font, new Rect2(pen, top, 8, 16),
                    new Rect2((code & 0x0f) * 8, (code >> 4) * 16, 8, 16), color ?? _textColor);
            pen += 8;
        }
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= ResizeBoard;
}
