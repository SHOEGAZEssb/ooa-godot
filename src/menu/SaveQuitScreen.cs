using Godot;
using System;
using static oracleofages.OracleGraphicsData;
using static oracleofages.OracleTileRenderer;

namespace oracleofages;

/// <summary>
/// Original non-game-over save menu: Continue, Save and Continue, Save and Quit.
/// </summary>
public partial class SaveQuitScreen : Node2D
{
    private const int MapStride = 32;
    private Texture2D _background = null!;
    private Image _fileSprites = null!;
    private Color[,] _spritePalette = null!;
    private Label _saveError = null!;
    private int _delayCounter;

    public int Cursor { get; private set; }
    public bool SaveErrorVisible => _saveError.Visible;
    public int DelayCounter
    {
        get => _delayCounter;
        set
        {
            _delayCounter = Math.Max(0, value);
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        _fileSprites = LoadPng("res://assets/oracle/menu/spr_fileselect_decorations.png");
        _spritePalette = LoadPalette("res://assets/oracle/menu/palette_file_sprites.bin");
        _background = BuildBackground();
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
    }

    public void Open()
    {
        Cursor = 0;
        DelayCounter = 0;
        _saveError.Visible = false;
        Visible = true;
        QueueRedraw();
    }

    public void Close()
    {
        Visible = false;
        DelayCounter = 0;
        _saveError.Visible = false;
    }

    public void Move(int direction)
    {
        int next = Cursor + Math.Sign(direction);
        if (next is < 0 or > 2)
            return;
        Cursor = next;
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
            OracleTileRenderer.DrawOamTile(
                this, _fileSprites, 0x20, 0x28, 4,
                new Vector2(33, 56 + Cursor * 24),
                false, false, _spritePalette, inverted: false);
        }
    }

    private Texture2D BuildBackground()
    {
        (byte[] map, byte[] flags) = FileMenuPresentation.BuildLayout(
            "map_save_menu_middle.bin", "flags_save_menu_middle.bin",
            "map_save_menu_bottom.bin", "flags_save_menu_bottom.bin",
            bottomLength: 128);

        Color[,] palette = LoadPalette("res://assets/oracle/menu/palette_file_bg.bin");
        var sources = new (Image Image, int FirstTile, int Bank, bool Interleaved)[]
        {
            (LoadPng("res://assets/oracle/gfx/gfx_hud.png"), 0x00, 0, false),
            (LoadPng("res://assets/oracle/gfx/gfx_hud.png"), 0x00, 1, false),
            (LoadPng("res://assets/oracle/menu/gfx_savescreen.png"), 0x80, 1, true),
            (LoadPng("res://assets/oracle/menu/gfx_fileselect.png"), 0x20, 1, false)
        };
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
