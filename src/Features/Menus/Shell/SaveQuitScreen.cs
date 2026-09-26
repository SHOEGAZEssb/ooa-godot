using Godot;
using System;
using System.Collections.Generic;
using static oracleofages.OracleGraphicsData;
using static oracleofages.OracleTileRenderer;

namespace oracleofages;

/// <summary>
/// Original save actions with an experimental Options entry outside game over.
/// </summary>
public partial class SaveQuitScreen : Node2D
{
    private const int MapStride = 32;
    private Texture2D _standardBackground = null!;
    private Texture2D _gameOverBackground = null!;
    private Texture2D _background = null!;
    private Image _fileSprites = null!;
    private Color[,] _spritePalette = null!;
    private Label _saveError = null!;
    private int _delayCounter;
    private readonly Texture2D[] _optionsBackgrounds = new Texture2D[8];

    public bool OptionsOpen { get; private set; }
    public int OptionsCursor { get; private set; }

    public int Cursor { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool SaveErrorVisible => _saveError.Visible;
    internal ulong BackgroundPixelHash =>
        OracleGraphicsCache.PixelHash(_background.GetImage());
    internal bool BackgroundIsOpaque
    {
        get
        {
            using Image image = _background.GetImage();
            for (int y = 0; y < image.GetHeight(); y++)
            for (int x = 0; x < image.GetWidth(); x++)
            {
                if (image.GetPixel(x, y).A < 0.99f)
                    return false;
            }
            return true;
        }
    }
    public int DelayCounter
    {
        get => _delayCounter;
        set
        {
            _delayCounter = Math.Max(0, value);
            QueueRedraw();
        }
    }

    private bool _resourcesPrepared;
    public override void _Ready()
    {
        foreach (bool _ in PrepareResources()) { }
    }

    internal System.Collections.Generic.IEnumerable<bool> PrepareResources()
    {
        if (_resourcesPrepared) yield break;
        _fileSprites = LoadPng("res://assets/oracle/menu/spr_fileselect_decorations.png");
        _spritePalette = LoadPalette("res://assets/oracle/menu/palette_file_sprites.bin");
        _standardBackground = BuildBackground(gameOver: false);
        yield return false;
        _gameOverBackground = BuildBackground(gameOver: true);
        yield return false;
        using (Image original = _standardBackground.GetImage())
        {
            for (int i = 0; i < 8; i++)
            {
                _optionsBackgrounds[i] = SaveOptionsPresentation.BuildOptions(
                    original, (i & 1) != 0, (i & 2) != 0, (i & 4) != 0);
                yield return false;
            }
            _standardBackground = SaveOptionsPresentation.BuildSave(original);
        }
        _background = _standardBackground;
        _saveError = new Label
        {
            Text = "SAVE FAILED\nCHECK STORAGE\nA/B: RETRY",
            Position = new Vector2(24, 43),
            Size = new Vector2(112, 58),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = false,
            ZIndex = 1
        };
        AddChild(_saveError);
        _resourcesPrepared = true;
    }

    public void Open(bool gameOver = false)
    {
        IsGameOver = gameOver;
        OptionsOpen = false;
        _background = gameOver
            ? _gameOverBackground
            : _standardBackground;
        Cursor = 0;
        DelayCounter = 0;
        _saveError.Visible = false;
        Visible = true;
        QueueRedraw();
    }

    public void Close()
    {
        Visible = false;
        OptionsOpen = false;
        IsGameOver = false;
        _background = _standardBackground;
        DelayCounter = 0;
        _saveError.Visible = false;
    }

    public bool Move(int direction)
    {
        if (OptionsOpen)
        {
            int option = OptionsCursor + Math.Sign(direction);
            if (option is < 0 or > 2)
                return false;
            OptionsCursor = option;
            QueueRedraw();
            return true;
        }
        int next = Cursor + Math.Sign(direction);
        if (next < 0 || next > (IsGameOver ? 2 : 3))
            return false;
        Cursor = next;
        QueueRedraw();
        return true;
    }

    internal void OpenOptions()
    {
        OptionsOpen = true;
        OptionsCursor = 0;
        QueueRedraw();
    }

    internal void RefreshOptions(bool noclip, bool overlay, bool hudBottom)
    {
        Texture2D next = _optionsBackgrounds[(noclip ? 1 : 0) | (overlay ? 2 : 0) | (hudBottom ? 4 : 0)];
        if (_background == next)
            return;
        _background = next;
        QueueRedraw();
    }

    internal void CloseOptions()
    {
        OptionsOpen = false;
        _background = _standardBackground;
        QueueRedraw();
    }

    public void ShowSaveError()
    {
        _saveError.Visible = true;
        QueueRedraw();
    }

    public void ClearSaveError()
    {
        _saveError.Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Visible)
            return;
        DrawTexture(_background, Vector2.Zero);
        FileMenuPresentation.DrawDecorations(
            this, _fileSprites, _spritePalette);
        if (!SaveErrorVisible && (DelayCounter & 0x04) == 0)
        {
            MenuOamPart acorn =
                MenuPresentationDatabase.Shared.FileOam("save-quit-acorn")[0];
            OracleTileRenderer.DrawOamTile(
                this,
                _fileSprites,
                0x20,
                acorn.Tile,
                acorn.Attributes & 0x07,
                FileMenuPresentation.OamScreenPosition(
                    acorn, yOffset: OptionsOpen ? OptionsCursor * 24 : Cursor * 24 - (IsGameOver ? 0 : 8)),
                (acorn.Attributes & 0x20) != 0,
                (acorn.Attributes & 0x40) != 0,
                _spritePalette,
                inverted: false);
        }
    }

    private Texture2D BuildBackground(bool gameOver)
    {
        (byte[] map, byte[] flags) = FileMenuPresentation.BuildLayout(
            "map_save_menu_middle.bin", "flags_save_menu_middle.bin",
            "map_save_menu_bottom.bin", "flags_save_menu_bottom.bin",
            bottomLength: 128);

        Color[,] palette = LoadPalette(gameOver
            ? "res://assets/oracle/menu/palette_file_erase_bg.bin"
            : "res://assets/oracle/menu/palette_file_bg.bin");
        var sources = new List<
            (Image Image, int FirstTile, int Bank, bool Interleaved)>
        {
            (LoadPng("res://assets/oracle/gfx/gfx_hud.png"), 0x00, 0, false),
            (LoadPng("res://assets/oracle/gfx/gfx_hud.png"), 0x00, 1, false),
            (LoadPng("res://assets/oracle/menu/gfx_savescreen.png"),
                0x80, 1, true),
            (LoadPng("res://assets/oracle/menu/gfx_fileselect.png"), 0x20, 1, false)
        };
        if (gameOver)
        {
            // saveQuitMenu_state0 loads GFXH_SAVE_MENU_GFX at $8801 first,
            // then GFXH_GAME_OVER_GFX at the same address. gfx_gameover is
            // only $20 tiles long: it replaces the title tiles $80-$9f while
            // the option graphics $a0-$ff remain from gfx_savescreen.
            sources.Insert(
                3,
                (LoadPng("res://assets/oracle/menu/gfx_gameover.png"),
                    0x80, 1, true));
        }
        Image output = Image.CreateEmpty(160, 144, false, Image.Format.Rgba8);
        for (int row = 0; row < 18; row++)
        for (int column = 0; column < 20; column++)
        {
            int offset = row * MapStride + column;
            int bank = flags[offset] >> 3 & 1;
            (Image Image, int FirstTile, int Bank, bool Interleaved)? selected = null;
            foreach (var source in sources)
            {
                int count = source.Image.GetWidth() / 8 * (source.Image.GetHeight() / 8);
                if (source.Bank == bank && map[offset] >= source.FirstTile &&
                    map[offset] < source.FirstTile + count)
                    selected = source;
            }
            if (selected is not { } tileSource)
                continue;
            DrawBackgroundTile(output, tileSource.Image, map[offset] - tileSource.FirstTile,
                flags[offset], palette, column * 8, row * 8, tileSource.Interleaved);
        }
        return ImageTexture.CreateFromImage(output);
    }

}
