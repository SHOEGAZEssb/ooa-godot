using Godot;
using System;

namespace oracleofages;

public partial class MainMenuScreen : Node2D
{
    public enum Page
    {
        Title, FileSelect, NewFileOptions, NameEntry, TextSpeed,
        CopySource, CopyDestination, CopyConfirm, EraseSelect, EraseConfirm, Notice
    }

    private const int MapStride = 32;
    private readonly OracleSaveData?[] _slots = new OracleSaveData?[3];
    private Texture2D _title = null!;
    private Texture2D _fileMenu = null!;
    private Texture2D _copyMenu = null!;
    private Texture2D _eraseMenu = null!;
    private Texture2D _newFileMenu = null!;
    private Texture2D[] _nameEntryMenus = null!;
    private Texture2D _textSpeedMenu = null!;
    private Texture2D _font = null!;
    private Texture2D _fileFont = null!;
    private Texture2D _nameEntryFont = null!;
    private Texture2D _nameKeyboardGlyphFont = null!;
    private Texture2D[] _fileHudTileTextures = null!;
    private Image _hudTiles = null!;
    private Image _titleSprites = null!;
    private Image _fileSprites = null!;
    private Image _linkSprites = null!;
    private Image _nayruSprites = null!;
    private Color[,] _titleSpritePalette = null!;
    private Color[,] _fileSpritePalette = null!;
    private Color[,] _fileBgPalette = null!;
    private ShaderMaterial _fadeMaterial = null!;
    private bool _titleBlink = true;
    private bool _actorFrame;
    private string _notice = "";
    private readonly char[] _enteredName = new string(' ', 5).ToCharArray();
    private int _nameEntryPosition;
    private int _nameLowerChoice;

    public Page CurrentPage { get; private set; }
    public int Cursor { get; private set; }
    public int Choice { get; private set; }
    public int SelectedSlot { get; private set; }
    public int TextSpeed { get; private set; }
    public int NameCursor { get; private set; }
    public string EnteredName => new string(_enteredName).TrimEnd(' ');

    public override void _Ready()
    {
        _fadeMaterial = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = """
                    shader_type canvas_item;
                    uniform float fade_offset = 0.0;
                    void fragment() {
                        vec4 pixel = texture(TEXTURE, UV) * COLOR;
                        // Compatibility rendering exposes canvas texture RGB in
                        // its squared transfer space once a custom shader is used.
                        // Restore the palette value before applying the original
                        // 5-bit white-fade component offset.
                        pixel.rgb = sqrt(pixel.rgb);
                        pixel.rgb = min(pixel.rgb + vec3(fade_offset / 31.0), vec3(1.0));
                        COLOR = pixel;
                    }
                    """
            }
        };
        Material = _fadeMaterial;
        _titleSprites = LoadPng("res://assets/oracle/menu/spr_titlescreen_sprites.png");
        _fileSprites = LoadPng("res://assets/oracle/menu/spr_fileselect_decorations.png");
        _linkSprites = LoadPng("res://assets/oracle/gfx/spr_link.png");
        _nayruSprites = LoadPng("res://assets/oracle/menu/spr_nayru_1.png");
        _hudTiles = LoadPng("res://assets/oracle/gfx/gfx_hud.png");
        _titleSpritePalette = LoadPalette("res://assets/oracle/menu/palette_title_sprites.bin");
        _fileSpritePalette = LoadPalette("res://assets/oracle/menu/palette_file_sprites.bin");
        _fileBgPalette = LoadPalette("res://assets/oracle/menu/palette_file_bg.bin");
        _font = BuildFontTexture(new Color(0x08 / 31.0f, 0x1f / 31.0f, 0x00));
        // gfx_font is 1bpp with white glyph pixels and a black background.
        // File-menu palette 6 therefore displays names as white on black.
        _fileFont = BuildFontTexture(_fileBgPalette[6, 0]);
        // The name buffer's expanded 2bpp tiles use color 3 for the yellow
        // field and color 0 for the glyph, opposite the filename strips.
        _nameEntryFont = BuildFontTexture(_fileBgPalette[5, 0]);
        _nameKeyboardGlyphFont = BuildFontTexture(_fileSpritePalette[1, 2]);
        _fileHudTileTextures = BuildHudTileTextures();
        _title = BuildTitleTexture();
        _fileMenu = BuildFileMenuTexture();
        _copyMenu = BuildCopyMenuTexture();
        _eraseMenu = BuildEraseMenuTexture();
        _newFileMenu = BuildNewFileMenuTexture();
        _nameEntryMenus = new[] {
            BuildNameEntryTexture(0), BuildNameEntryTexture(1), BuildNameEntryTexture(2)
        };
        _textSpeedMenu = BuildTextSpeedMenuTexture();
    }

    public override void _Draw()
    {
        if (CurrentPage == Page.Title)
        {
            DrawTexture(_title, Vector2.Zero);
            DrawTitleLogoSprites();
            if (_titleBlink)
                DrawPressStartSprites();
            return;
        }

        Texture2D background = CurrentPage switch
        {
            Page.CopySource or Page.CopyDestination or Page.CopyConfirm => _copyMenu,
            Page.EraseSelect or Page.EraseConfirm => _eraseMenu,
            Page.NewFileOptions => _newFileMenu,
            Page.NameEntry => _nameEntryMenus[SelectedSlot],
            Page.TextSpeed => _textSpeedMenu,
            _ => _fileMenu
        };
        DrawTexture(background, Vector2.Zero);
        DrawFileDecorations();
        switch (CurrentPage)
        {
            case Page.FileSelect: DrawFileSelect(showActorAndSummary: true); break;
            case Page.CopySource: DrawFileSelect(showActorAndSummary: false); break;
            case Page.CopyDestination: DrawFileSelect(showActorAndSummary: false); break;
            case Page.EraseSelect: DrawFileSelect(showActorAndSummary: true); break;
            case Page.NewFileOptions: DrawNewFileOptions(); break;
            case Page.NameEntry: DrawNameEntry(); break;
            case Page.TextSpeed: DrawTextSpeed(); break;
            case Page.CopyConfirm: DrawConfirm(); break;
            case Page.EraseConfirm: DrawConfirm(); break;
            case Page.Notice: DrawNotice(); break;
        }
    }

    public void SetSlots(OracleSaveData?[] slots)
    {
        Array.Copy(slots, _slots, _slots.Length);
        QueueRedraw();
    }

    public void ShowTitle() { CurrentPage = Page.Title; QueueRedraw(); }
    public void SetTitleBlink(bool visible) { _titleBlink = visible; QueueRedraw(); }
    public void SetActorFrame(bool secondFrame) { _actorFrame = secondFrame; QueueRedraw(); }
    public void SetWhiteFade(float progress)
    {
        float offset = Math.Min(31.0f, MathF.Floor(Math.Clamp(progress, 0.0f, 1.0f) * 32.0f));
        _fadeMaterial.SetShaderParameter("fade_offset", offset);
        QueueRedraw();
    }
    public void ShowFileSelect() { CurrentPage = Page.FileSelect; Cursor = 0; Choice = 0; QueueRedraw(); }
    public void ShowNewFileOptions(int slot) { CurrentPage = Page.NewFileOptions; SelectedSlot = slot; Cursor = 0; QueueRedraw(); }
    public void ShowNameEntry(int slot) => ShowNameEntry(slot, string.Empty);

    internal void ShowNameEntry(int slot, string initialName)
    {
        CurrentPage = Page.NameEntry;
        SelectedSlot = slot;
        Array.Fill(_enteredName, ' ');
        int length = Math.Min(initialName.Length, _enteredName.Length);
        for (int index = 0; index < length; index++)
            _enteredName[index] = initialName[index];
        _nameEntryPosition = Math.Min(length, _enteredName.Length - 1);
        _nameLowerChoice = 0;
        NameCursor = 0;
        QueueRedraw();
    }
    public void ShowTextSpeed(int slot, int speed) { CurrentPage = Page.TextSpeed; SelectedSlot = slot; Cursor = slot; TextSpeed = Math.Clamp(speed, 0, 4); QueueRedraw(); }
    public void ShowCopySource() { CurrentPage = Page.CopySource; Cursor = 0; QueueRedraw(); }
    public void ShowCopyDestination(int source) { CurrentPage = Page.CopyDestination; Cursor = (source + 1) % 3; QueueRedraw(); }
    public void ShowCopyConfirm(int destination) { CurrentPage = Page.CopyConfirm; SelectedSlot = destination; Choice = 0; QueueRedraw(); }
    public void ShowEraseSelect() { CurrentPage = Page.EraseSelect; Cursor = 0; QueueRedraw(); }
    public void ShowEraseConfirm(int slot) { CurrentPage = Page.EraseConfirm; SelectedSlot = slot; Choice = 0; QueueRedraw(); }
    public void ShowNotice(string text) { CurrentPage = Page.Notice; _notice = text; QueueRedraw(); }
    public void SetCursor(int cursor) { Cursor = cursor; QueueRedraw(); }
    public void SetChoice(int choice) { Choice = choice; QueueRedraw(); }
    public void SetSelectedSlot(int slot) => SelectedSlot = slot;
    public void SetTextSpeed(int speed) { TextSpeed = speed; QueueRedraw(); }

    public void AppendNameCharacter(char character)
    {
        _enteredName[_nameEntryPosition] = character;
        _nameEntryPosition = Math.Min(4, _nameEntryPosition + 1);
        QueueRedraw();
    }

    public void DeleteNameCharacter()
    {
        _enteredName[_nameEntryPosition] = ' ';
        _nameEntryPosition = Math.Max(0, _nameEntryPosition - 1);
        QueueRedraw();
    }

    public void MoveNameCursor(Vector2I direction)
    {
        if (direction.X != 0)
        {
            if (NameCursor < 0x50)
            {
                int row = NameCursor & 0xf0;
                int column = NameCursor & 0x0f;
                do column = (column + direction.X + 16) & 0x0f;
                while (column >= 12);
                NameCursor = row | column;
            }
            else
            {
                _nameLowerChoice = (_nameLowerChoice + direction.X + 3) % 3;
                NameCursor = 0x50 + new[] { 0, 3, 6 }[_nameLowerChoice];
            }
        }
        else if (direction.Y != 0)
        {
            int column = NameCursor & 0x0f;
            int row = NameCursor & 0xf0;
            do row = (row + direction.Y * 0x10 + 0x80) & 0x70;
            while (row >= 0x60);
            NameCursor = row | column;
            if (row == 0x50)
            {
                _nameLowerChoice = column < 3 ? 0 : column < 6 ? 1 : 2;
            }
        }
        QueueRedraw();
    }

    public void MoveNameEntryPosition(int delta)
    {
        _nameEntryPosition = Math.Clamp(_nameEntryPosition + delta, 0, 4);
        QueueRedraw();
    }

    public int NameLowerChoice => _nameLowerChoice;

    public bool TryGetSelectedNameCharacter(out char character)
    {
        character = ' ';
        if (NameCursor >= 0x50)
            return false;
        int row = NameCursor >> 4;
        int column = NameCursor & 0x0f;
        int alphabetIndex = row * 6 + column % 6;
        if (alphabetIndex < 26)
            character = (char)((column < 6 ? 'A' : 'a') + alphabetIndex);
        return true;
    }

    internal Color FileNameStripColorForValidation => _fileMenu.GetImage().GetPixel(24, 48);
    internal Color FilePanelColorForValidation => _fileMenu.GetImage().GetPixel(80, 72);
    internal Color DeathTileBackgroundColorForValidation =>
        _fileHudTileTextures[0x10].GetImage().GetPixel(0, 0);
    internal Color NameEntryFieldColorForValidation =>
        _nameEntryMenus[0].GetImage().GetPixel(80, 8);
    internal Color NameEntryPanelColorForValidation =>
        _nameEntryMenus[0].GetImage().GetPixel(72, 8);
    internal bool NameEntryHighlightIsGlyphMaskForValidation
    {
        get
        {
            Image image = _nameKeyboardGlyphFont.GetImage();
            int opaque = 0;
            int transparent = 0;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                Color pixel = image.GetPixel(8 + x, 64 + y); // ASCII 'A'
                if (pixel.A > 0.5f)
                {
                    opaque++;
                    Color expected = _fileSpritePalette[1, 2];
                    const float rgba8Tolerance = 1.0f / 255.0f + 0.0001f;
                    if (MathF.Abs(pixel.R - expected.R) > rgba8Tolerance ||
                        MathF.Abs(pixel.G - expected.G) > rgba8Tolerance ||
                        MathF.Abs(pixel.B - expected.B) > rgba8Tolerance)
                        return false;
                }
                else
                    transparent++;
            }
            return opaque > 0 && transparent > 0;
        }
    }
    internal static Vector2 TextSpeedCursorPositionForValidation(int speed) =>
        new(41 + Math.Clamp(speed, 0, 4) * 16, 128);
    internal static int InterleavedSourceTileForValidation(int tile, int columns) =>
        SourceTileIndex(tile, columns, interleaved: true);

    private void DrawFileSelect(bool showActorAndSummary)
    {
        DrawFileNames();
        if (showActorAndSummary && Cursor < 3 && _slots[Cursor] is OracleSaveData selected)
            DrawFileSummary(selected);
        if (showActorAndSummary)
            DrawSelectedFileActor();

        Vector2 cursor = Cursor < 3
            ? new Vector2(8, 52 + Cursor * 24)
            : new Vector2(Choice == 0 ? 34 : 90, 122);
        DrawAcorn(cursor);
    }

    private void DrawFileNames()
    {
        for (int slot = 0; slot < 3; slot++)
        {
            if (_slots[slot] is not OracleSaveData save)
                continue;

            // Saves created by development builds predating the menu have no
            // encoded name. Keep them visibly selectable without rewriting them.
            string name = save.LinkName.Length > 0 ? save.LinkName : "LINK";
            DrawText(name, new Vector2(24, 48 + slot * 24), _fileFont);
        }
    }

    private void DrawSelectedFileActor()
    {
        if (Cursor >= 3)
            return;

        OracleSaveData? save = _slots[Cursor];
        if (save is null)
        {
            DrawLinkPart(0x04, 0x58, 0x00);
            DrawLinkPart(0x06, 0x60, 0x00);
            return;
        }

        if (save.IsCompleted)
        {
            DrawLinkPart(_actorFrame ? 0x02 : 0x00, 0x58, _actorFrame ? 0x20 : 0x00);
            DrawLinkPart(_actorFrame ? 0x00 : 0x02, 0x60, _actorFrame ? 0x20 : 0x00);
            DrawNayruPart(_actorFrame ? 0x08 : 0x06, 0x68, _actorFrame ? 0x29 : 0x09);
            DrawNayruPart(_actorFrame ? 0x06 : 0x08, 0x70, _actorFrame ? 0x29 : 0x09);
            return;
        }

        if (save.IsLinkedGame)
        {
            DrawLinkPart(_actorFrame ? 0x18 : 0x12, 0x58, 0x20);
            DrawLinkPart(_actorFrame ? 0x16 : 0x10, 0x60, 0x20);
            DrawLinkPart(0x14, 0x64, 0x22);
            return;
        }

        DrawLinkPart(_actorFrame ? 0x02 : 0x00, 0x58, _actorFrame ? 0x20 : 0x00);
        DrawLinkPart(_actorFrame ? 0x00 : 0x02, 0x60, _actorFrame ? 0x20 : 0x00);
    }

    private void DrawLinkPart(int tile, int oamX, int flags) =>
        DrawOamTileFromSource(_linkSprites, 0x20 + tile, flags & 7,
            new Vector2(oamX - 8, 0x4e - 16),
            (flags & 0x20) != 0, (flags & 0x40) != 0, _fileSpritePalette);

    private void DrawNayruPart(int tile, int oamX, int flags) =>
        DrawOamTileFromSource(_nayruSprites, tile - 0x06, flags & 7,
            new Vector2(oamX - 8, 0x4e - 16),
            (flags & 0x20) != 0, (flags & 0x40) != 0, _fileSpritePalette);

    private void DrawFileSummary(OracleSaveData save)
    {
        int deaths = Math.Clamp(save.DeathCount, 0, 999);
        DrawHudTile(0x10 + deaths / 100, new Vector2(112, 72));
        DrawHudTile(0x10 + deaths / 10 % 10, new Vector2(120, 72));
        DrawHudTile(0x10 + deaths % 10, new Vector2(128, 72));

        int hearts = Math.Clamp(save.MaxHealthQuarters / 4, 0, 20);
        int heartsPerRow = save.MaxHealthQuarters >= 14 * 4 + 1 ? 8 : 7;
        for (int heart = 0; heart < hearts; heart++)
        {
            DrawHudTile(0x0a, new Vector2(
                80 + heart % heartsPerRow * 8,
                80 + heart / heartsPerRow * 8));
        }
    }

    private void DrawNewFileOptions()
    {
        DrawAcorn(new Vector2(32, 56 + Cursor * 24));
    }

    private void DrawNameEntry()
    {
        DrawText(new string(_enteredName), new Vector2(80, 8), _nameEntryFont);

        if (NameCursor < 0x50)
        {
            int row = NameCursor >> 4;
            int column = NameCursor & 0x0f;
            int mappedColumn = column + (column >= 6 ? 2 : 0);
            Vector2 offset = new(mappedColumn * 8, row * 16);
            if (TryGetSelectedNameCharacter(out char selectedCharacter) &&
                selectedCharacter != ' ')
            {
                // Tile $2a is a solid sprite-palette-1 color-2 block with its
                // OBJ-priority bit set. The GBC hides it behind the keyboard's
                // nonzero black pixels, exposing blue only through the color-0
                // letter. Drawing that glyph in the sprite color reproduces
                // the hardware mask without covering the black background.
                DrawText(selectedCharacter.ToString(), new Vector2(24, 40) + offset,
                    _nameKeyboardGlyphFont);
            }
            DrawOamTile(_fileSprites, 0x20, 0x2c, 2,
                new Vector2(24, 42) + offset, false, false,
                _fileSpritePalette, inverted: false);
        }
        else
        {
            int xOffset = new[] { 24, 48, 120 }[_nameLowerChoice];
            DrawOamTile(_fileSprites, 0x20, 0x2c, 1,
                new Vector2(xOffset, 122), false, false,
                _fileSpritePalette, inverted: false);
            DrawOamTile(_fileSprites, 0x20, 0x2c, 1,
                new Vector2(xOffset + 8, 122), false, false,
                _fileSpritePalette, inverted: false);
        }

        DrawOamTile(_fileSprites, 0x20, 0x2c, 2,
            new Vector2(80 + _nameEntryPosition * 8, 10), false, false,
            _fileSpritePalette, inverted: false);
    }

    private void DrawTextSpeed()
    {
        DrawFileNames();
        if (_slots[SelectedSlot] is OracleSaveData selected)
            DrawFileSummary(selected);
        DrawSelectedFileActor();
        DrawAcorn(new Vector2(8, 52 + SelectedSlot * 24));
        // bank2.s uses OAM tile $2e, palette 1 at ($31,$90), with a
        // 16-pixel X offset for each wTextSpeed value. The tile is the final
        // 8x16 cell in spr_fileselect_decorations loaded for this menu.
        DrawOamTile(_fileSprites, 0x20, 0x2e, 1,
            TextSpeedCursorPositionForValidation(TextSpeed), false, false,
            _fileSpritePalette, inverted: false);
    }

    private void DrawConfirm()
    {
        DrawFileNames();
        if (CurrentPage == Page.EraseConfirm)
        {
            DrawSelectedFileActor();
            if (_slots[SelectedSlot] is OracleSaveData selected)
                DrawFileSummary(selected);
        }
        DrawAcorn(new Vector2(8, 52 + SelectedSlot * 24));
        DrawAcorn(new Vector2(Choice == 0 ? 34 : 90, 122));
    }

    private void DrawNotice()
    {
        DrawRect(new Rect2(16, 38, 128, 68), _fileBgPalette[2, 3]);
        string[] lines = _notice.Split('\n');
        for (int index = 0; index < lines.Length; index++)
            DrawText(lines[index], new Vector2(24, 51 + index * 18));
        DrawText("A/B: BACK", new Vector2(44, 88));
    }

    private void DrawPanel(string heading, string detail)
    {
        DrawRect(new Rect2(16, 28, 128, 92), _fileBgPalette[2, 3]);
        DrawText(heading, new Vector2(28, 34));
        DrawText(detail, new Vector2(48, 54));
    }

    private void DrawText(string text, Vector2 position, Texture2D? font = null)
    {
        font ??= _font;
        int length = Math.Min(text.Length, 18);
        for (int index = 0; index < length; index++)
        {
            int code = text[index] == '♥' ? '*' : text[index];
            if (code == ' ')
                continue;
            Rect2 source = new((code & 0x0f) * 8, (code >> 4) * 16, 8, 16);
            DrawTextureRectRegion(font,
                new Rect2(position + new Vector2(index * 8, 0), new Vector2(8, 16)), source);
        }
    }

    private void DrawHudTile(int tile, Vector2 position)
    {
        DrawTexture(_fileHudTileTextures[tile], position);
    }

    private Texture2D[] BuildHudTileTextures()
    {
        int columns = _hudTiles.GetWidth() / 8;
        int count = columns * (_hudTiles.GetHeight() / 8);
        var textures = new Texture2D[count];
        for (int tile = 0; tile < count; tile++)
        {
            Image output = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                Color pixel = _hudTiles.GetPixel(
                    tile % columns * 8 + x, tile / columns * 8 + y);
                output.SetPixel(x, y, _fileBgPalette[6, Shade(pixel)]);
            }
            textures[tile] = ImageTexture.CreateFromImage(output);
        }
        return textures;
    }

    private void DrawTitleLogoSprites()
    {
        int[,] sprites = {
            {0x48,0x90,0x62,0x06},{0x42,0x8e,0x68,0x06},{0x51,0x7a,0x56,0x04},
            {0x50,0x82,0x74,0x04},{0x58,0x7a,0x6a,0x07},{0x58,0x82,0x6c,0x07},
            {0x58,0x8a,0x6e,0x07},{0x54,0x8a,0x54,0x03},{0x54,0x82,0x52,0x03},
            {0x54,0x7a,0x50,0x03},{0x64,0x7a,0x70,0x03},{0x64,0x82,0x72,0x03},
            {0x64,0x8a,0x70,0x23},{0x40,0x86,0x66,0x06},{0x40,0x7f,0x64,0x06},
            {0x41,0x70,0x60,0x06},{0x55,0x76,0x5a,0x06},{0x44,0x68,0x5e,0x26}
        };
        DrawOamList(_titleSprites, 0x38, _titleSpritePalette, sprites);
    }

    private void DrawPressStartSprites()
    {
        int[,] sprites = {
            {0x80,0x2c,0x38,0},{0x80,0x34,0x3a,0},{0x80,0x3c,0x3c,0},
            {0x80,0x44,0x3e,0},{0x80,0x4c,0x3e,0},{0x80,0x5c,0x3e,0},
            {0x80,0x64,0x40,0},{0x80,0x6c,0x42,0},{0x80,0x74,0x3a,0},
            {0x80,0x7c,0x40,0}
        };
        DrawOamList(_titleSprites, 0x38, _titleSpritePalette, sprites);
    }

    private void DrawFileDecorations()
    {
        int[,] sprites = {
            {0x23,0x0a,0x20,5},{0x23,0x12,0x22,5},{0x33,0x06,0x20,5},
            {0x33,0x0e,0x22,5},{0x0f,0x07,0x26,5},{0x3b,0x16,0x20,0x25},
            {0x3b,0x0e,0x22,0x25},{0x17,0x0a,0x24,0x25},{0x21,0x96,0x20,5},
            {0x21,0x9e,0x22,5},{0x17,0x9b,0x26,0x65},{0x14,0x9d,0x24,5},
            {0x31,0xa2,0x20,0x25},{0x31,0x9a,0x22,0x25},{0x39,0x92,0x20,5},
            {0x39,0x9a,0x22,5}
        };
        DrawOamList(_fileSprites, 0x20, _fileSpritePalette, sprites, inverted: false);
    }

    private void DrawAcorn(Vector2 position) =>
        DrawOamTile(_fileSprites, 0x20, 0x28, 4, position, false, false,
            _fileSpritePalette, inverted: false);

    private void DrawOamList(Image source, int tileBase, Color[,] palette, int[,] sprites,
        bool inverted = true)
    {
        for (int index = 0; index < sprites.GetLength(0); index++)
        {
            int flags = sprites[index, 3];
            DrawOamTile(source, tileBase, sprites[index, 2], flags & 7,
                new Vector2(sprites[index, 1] - 8, sprites[index, 0] - 16),
                (flags & 0x20) != 0, (flags & 0x40) != 0, palette, inverted);
        }
    }

    private void DrawOamTile(Image source, int tileBase, int tile, int paletteIndex,
        Vector2 position, bool flipX, bool flipY, Color[,] palette, bool inverted = true)
    {
        DrawOamTileFromSource(source, tile - tileBase, paletteIndex,
            position, flipX, flipY, palette, inverted);
    }

    private void DrawOamTileFromSource(Image source, int sourceTile, int paletteIndex,
        Vector2 position, bool flipX, bool flipY, Color[,] palette, bool inverted = true)
    {
        int columns = source.GetWidth() / 8;
        int cell = sourceTile / 2;
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sy = flipY ? 15 - y : y;
            Color pixel = source.GetPixel(cell % columns * 8 + (flipX ? 7 - x : x),
                cell / columns * 16 + sy);
            int color = SpriteColorIndex(pixel, inverted);
            if (pixel.A < 0.1f || color == 0)
                continue;
            DrawRect(new Rect2(position + new Vector2(x, y), Vector2.One),
                palette[paletteIndex, color]);
        }
    }

    internal static int SpriteColorIndexForValidation(Color pixel, bool inverted) =>
        SpriteColorIndex(pixel, inverted);

    private static int SpriteColorIndex(Color pixel, bool inverted)
    {
        int shade = Shade(pixel);
        return inverted ? 3 - shade : shade;
    }

    private static Texture2D BuildScreenTexture(byte[] map, byte[] flags, Color[,] palette,
        params (string Path, int Destination, int Bank, bool Interleaved)[] sources)
    {
        var tiles = new (Image? Source, int Tile)[2, 256];
        foreach ((string path, int destination, int bank, bool interleaved) in sources)
        {
            Image source = LoadPng(path);
            int firstTile = destination >= 0x9000
                ? (destination - 0x9000) / 16
                : 0x80 + (destination - 0x8800) / 16;
            int count = source.GetWidth() / 8 * (source.GetHeight() / 8);
            for (int tile = 0; tile < count; tile++)
            {
                int sourceTile = SourceTileIndex(tile, source.GetWidth() / 8, interleaved);
                tiles[bank, (firstTile + tile) & 0xff] = (source, sourceTile);
            }
        }

        Image output = Image.CreateEmpty(160, 144, false, Image.Format.Rgba8);
        for (int row = 0; row < 18; row++)
        for (int column = 0; column < 20; column++)
        {
            int offset = row * MapStride + column;
            byte attributes = flags[offset];
            (Image? source, int tile) = tiles[(attributes >> 3) & 1, map[offset]];
            if (source is null)
            {
                int paletteIndex = attributes & 7;
                int bank = (attributes >> 3) & 1;
                // $c0-$e1 are the live 8x16 filename tiles written into VRAM
                // by textInput_updateEntryCursor. Their untouched pixels are
                // font color 1 (black), not palette color 0 (white).
                bool filenameBuffer = bank == 1 && paletteIndex == 6 &&
                    map[offset] is >= 0xc0 and <= 0xe1;
                bool nameEntryBuffer = bank == 1 && paletteIndex == 5 &&
                    map[offset] is >= 0xc0 and <= 0xc9;
                int shade = filenameBuffer || nameEntryBuffer ? 3 : 0;
                Color blank = palette[paletteIndex, shade];
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    output.SetPixel(column * 8 + x, row * 8 + y, blank);
                continue;
            }
            DrawBackgroundTile(output, source, tile, attributes, palette, column * 8, row * 8);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static int SourceTileIndex(int tile, int columns, bool interleaved)
    {
        if (!interleaved)
            return tile;
        int cell = tile / 2;
        return (cell / columns * 2 + (tile & 1)) * columns + cell % columns;
    }

    private Texture2D BuildTitleTexture() => BuildScreenTexture(
        ReadBytes("res://assets/oracle/menu/map_titlescreen.bin", 576),
        ReadBytes("res://assets/oracle/menu/flags_titlescreen.bin", 576),
        LoadPalette("res://assets/oracle/menu/palette_title_bg.bin"),
        ("res://assets/oracle/menu/gfx_titlescreen_1.png", 0x8800, 0, false),
        ("res://assets/oracle/menu/gfx_titlescreen_2.png", 0x8d00, 0, false),
        ("res://assets/oracle/menu/gfx_titlescreen_3.png", 0x9300, 0, false),
        ("res://assets/oracle/menu/gfx_titlescreen_4.png", 0x9400, 0, false),
        ("res://assets/oracle/menu/gfx_titlescreen_5.png", 0x8800, 1, false),
        ("res://assets/oracle/menu/gfx_titlescreen_6.png", 0x8cd0, 1, false));

    private Texture2D BuildFileMenuTexture()
    {
        (byte[] map, byte[] flags) = BuildFileLayout(
            "map_file_menu_middle.bin", "flags_file_menu_middle.bin",
            "map_file_menu_bottom.bin", "flags_file_menu_bottom.bin");
        return BuildScreenTexture(map, flags, _fileBgPalette,
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 0, false),
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 1, false),
            ("res://assets/oracle/menu/gfx_messagespeed.png", 0x9200, 0, false),
            ("res://assets/oracle/menu/gfx_fileselect.png", 0x9200, 1, false),
            ("res://assets/oracle/menu/gfx_pickafile_2.png", 0x8800, 1, true),
            ("res://assets/oracle/menu/gfx_copy.png", 0x8a00, 1, true),
            ("res://assets/oracle/menu/gfx_erase.png", 0x8aa0, 1, true));
    }

    private Texture2D BuildCopyMenuTexture()
    {
        (byte[] map, byte[] flags) = BuildFileLayout(
            "map_file_menu_copy.bin", "flags_file_menu_copy.bin",
            "map_file_menu_bottom.bin", "flags_file_menu_bottom.bin");
        return BuildScreenTexture(map, flags, _fileBgPalette,
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 0, false),
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 1, false),
            ("res://assets/oracle/menu/gfx_messagespeed.png", 0x9200, 0, false),
            ("res://assets/oracle/menu/gfx_fileselect.png", 0x9200, 1, false),
            ("res://assets/oracle/menu/gfx_copywhatwhere.png", 0x8800, 1, true),
            ("res://assets/oracle/menu/gfx_quit_2.png", 0x8a00, 1, true),
            ("res://assets/oracle/menu/gfx_copy.png", 0x8aa0, 1, true));
    }

    private Texture2D BuildEraseMenuTexture()
    {
        (byte[] map, byte[] flags) = BuildFileLayout(
            "map_file_menu_middle.bin", "flags_file_menu_middle.bin",
            "map_file_menu_bottom.bin", "flags_file_menu_bottom.bin");
        return BuildScreenTexture(map, flags, _fileBgPalette,
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 0, false),
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 1, false),
            ("res://assets/oracle/menu/gfx_messagespeed.png", 0x9200, 0, false),
            ("res://assets/oracle/menu/gfx_fileselect.png", 0x9200, 1, false),
            ("res://assets/oracle/menu/gfx_pickafile.png", 0x8800, 1, true),
            ("res://assets/oracle/menu/gfx_quit_2.png", 0x8a00, 1, true),
            ("res://assets/oracle/menu/gfx_erase.png", 0x8aa0, 1, true));
    }

    private Texture2D BuildNewFileMenuTexture()
    {
        (byte[] map, byte[] flags) = BuildFileLayout(
            "map_save_menu_middle.bin", "flags_save_menu_middle.bin",
            "map_save_menu_bottom.bin", "flags_save_menu_bottom.bin", bottomLength: 128);
        return BuildScreenTexture(map, flags, _fileBgPalette,
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 0, false),
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 1, false),
            ("res://assets/oracle/menu/gfx_messagespeed.png", 0x9200, 0, false),
            ("res://assets/oracle/menu/gfx_fileselect.png", 0x9200, 1, false),
            ("res://assets/oracle/menu/gfx_newfilescreen.png", 0x8800, 1, true));
    }

    private Texture2D BuildNameEntryTexture(int slot)
    {
        byte[] map = new byte[576];
        byte[] flags = new byte[576];
        Overlay(map, ReadBytes("res://assets/oracle/menu/map_name_entry_top.bin", 160), 0);
        Overlay(flags, ReadBytes("res://assets/oracle/menu/flags_name_entry_top.bin", 160), 0);
        Overlay(map, ReadBytes("res://assets/oracle/menu/map_name_entry_middle.bin", 320), 0xa0);
        Overlay(flags, ReadBytes("res://assets/oracle/menu/flags_name_entry_middle.bin", 320), 0xa0);
        Overlay(map, ReadBytes("res://assets/oracle/menu/map_name_entry_bottom.bin", 128),
            0x1e0, 96);
        Overlay(flags, ReadBytes("res://assets/oracle/menu/flags_name_entry_bottom.bin", 128),
            0x1e0, 96);

        // label_02_038 writes hActiveFileSlot+$20 into this tile before the
        // $9c00 tilemap upload, producing the original FILE 1/2/3 digit.
        map[0x49] = (byte)(0x20 + slot);

        Texture2D baseTexture = BuildScreenTexture(map, flags, _fileBgPalette,
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 0, false),
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 1, false),
            ("res://assets/oracle/menu/gfx_name.png", 0x8800, 1, true),
            ("res://assets/oracle/menu/gfx_fileselect.png", 0x9200, 1, false));

        // UNCMP_GFXH_0b expands characters $40-$7a from the 1bpp font into
        // paired 2bpp tiles at bank-0 $8800. Reproduce that VRAM layout so
        // the imported name-entry map supplies all five keyboard rows.
        Image output = baseTexture.GetImage();
        Image font = LoadPng("res://assets/oracle/gfx/gfx_font.png");
        for (int row = 0; row < 18; row++)
        for (int column = 0; column < 20; column++)
        {
            int offset = row * MapStride + column;
            byte attributes = flags[offset];
            byte tile = map[offset];
            if ((attributes & 0x08) != 0 || tile < 0x80)
                continue;
            int sourceTile = 128 + SourceTileIndex(tile - 0x80, 16, interleaved: true);
            DrawBackgroundTile(output, font, sourceTile, attributes, _fileBgPalette,
                column * 8, row * 8);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildTextSpeedMenuTexture()
    {
        (byte[] map, byte[] flags) = BuildFileLayout(
            "map_file_menu_middle.bin", "flags_file_menu_middle.bin",
            "map_file_menu_bottom.bin", "flags_file_menu_bottom.bin");
        Overlay(map, ReadBytes(
            "res://assets/oracle/menu/map_file_menu_message_speed.bin", 128), 0x1c0);
        Overlay(flags, ReadBytes(
            "res://assets/oracle/menu/flags_file_menu_message_speed.bin", 128), 0x1c0);
        return BuildScreenTexture(map, flags, _fileBgPalette,
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 0, false),
            ("res://assets/oracle/gfx/gfx_hud.png", 0x9000, 1, false),
            ("res://assets/oracle/menu/gfx_messagespeed.png", 0x9200, 0, false),
            ("res://assets/oracle/menu/gfx_fileselect.png", 0x9200, 1, false),
            ("res://assets/oracle/menu/gfx_pickafile_2.png", 0x8800, 1, true),
            ("res://assets/oracle/menu/gfx_copy.png", 0x8a00, 1, true),
            ("res://assets/oracle/menu/gfx_erase.png", 0x8aa0, 1, true));
    }

    private static (byte[] Map, byte[] Flags) BuildFileLayout(
        string middleMap, string middleFlags, string bottomMap, string bottomFlags,
        int bottomLength = 96)
    {
        byte[] map = new byte[576];
        byte[] flags = new byte[576];
        Overlay(map, ReadBytes("res://assets/oracle/menu/map_file_menu_top.bin", 160), 0);
        Overlay(flags, ReadBytes("res://assets/oracle/menu/flags_file_menu_top.bin", 160), 0);
        Overlay(map, ReadBytes($"res://assets/oracle/menu/{middleMap}", 320), 0xa0);
        Overlay(flags, ReadBytes($"res://assets/oracle/menu/{middleFlags}", 320), 0xa0);
        // The save-menu data includes a fourth, off-screen row; the visible 160x144
        // background ends after the first three rows beginning at tilemap offset $1e0.
        Overlay(map, ReadBytes($"res://assets/oracle/menu/{bottomMap}", bottomLength), 0x1e0, 96);
        Overlay(flags, ReadBytes($"res://assets/oracle/menu/{bottomFlags}", bottomLength), 0x1e0, 96);
        return (map, flags);
    }

    private static void DrawBackgroundTile(Image output, Image source, int sourceTile,
        byte flags, Color[,] palette, int destinationX, int destinationY)
    {
        bool flipX = (flags & 0x20) != 0;
        bool flipY = (flags & 0x40) != 0;
        int paletteIndex = flags & 7;
        int columns = source.GetWidth() / 8;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            Color pixel = source.GetPixel(sourceTile % columns * 8 + (flipX ? 7 - x : x),
                sourceTile / columns * 8 + (flipY ? 7 - y : y));
            output.SetPixel(destinationX + x, destinationY + y, palette[paletteIndex, Shade(pixel)]);
        }
    }

    private static Texture2D BuildFontTexture(Color color)
    {
        Image source = LoadPng("res://assets/oracle/gfx/gfx_font.png");
        Image output = Image.CreateEmpty(source.GetWidth(), source.GetHeight(), false, Image.Format.Rgba8);
        for (int y = 0; y < source.GetHeight(); y++)
        for (int x = 0; x < source.GetWidth(); x++)
            output.SetPixel(x, y, source.GetPixel(x, y).R > 0.5f ? color : Colors.Transparent);
        return ImageTexture.CreateFromImage(output);
    }

    private static int Shade(Color pixel) =>
        Math.Clamp(Mathf.RoundToInt((1.0f - pixel.R) * 3.0f), 0, 3);

    private static Image LoadPng(string path)
    {
        Image image = new();
        Error error = image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path));
        if (error != Error.Ok)
            throw new InvalidOperationException($"Could not load menu graphics {path}: {error}.");
        return image;
    }

    private static byte[] ReadBytes(string path, int expected)
    {
        byte[] bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length != expected)
            throw new InvalidOperationException($"{path} should contain {expected} bytes, got {bytes.Length}.");
        return bytes;
    }

    private static Color[,] LoadPalette(string path)
    {
        byte[] bytes = ReadBytes(path, 8 * 4 * 3);
        var result = new Color[8, 4];
        for (int palette = 0; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++)
        {
            int offset = (palette * 4 + shade) * 3;
            result[palette, shade] = new Color(bytes[offset] / 31.0f,
                bytes[offset + 1] / 31.0f, bytes[offset + 2] / 31.0f);
        }
        return result;
    }

    private static void Overlay(byte[] destination, byte[] source, int offset, int? count = null) =>
        Array.Copy(source, 0, destination, offset, count ?? source.Length);
}
