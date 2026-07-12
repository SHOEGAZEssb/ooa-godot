using Godot;
using System;

namespace oracleofages;

public partial class DialogueBox : Node2D
{
    private const int LinesPerPage = 3;
    private const int CharactersPerLine = 16;
    private const int PanelX = 8;
    private const int PanelWidth = 18 * 8;
    private const int PanelHeight = 5 * 8;

    // PALH_0e / paletteData4920: normal textbox text uses color 2 and the
    // background uses color 3. The continue arrow uses color 0.
    private static readonly Color TextColor = GbcColor(0x08, 0x1f, 0x00);
    private static readonly Color ArrowColor = GbcColor(0x1f, 0x0e, 0x04);
    private static readonly Color BackgroundColor = GbcColor(0x00, 0x00, 0x00);

    private Texture2D _fontTexture = null!;
    private string[] _lines = Array.Empty<string>();
    private int _page;
    private ulong _openedFrame;
    private bool _open;

    public bool IsOpen => _open;

    public override void _Ready()
    {
        _fontTexture = BuildFontTexture();
    }

    public void ShowMessage(string message, float linkY)
    {
        _lines = message.Replace("\r", "").Split('\n');
        _page = 0;
        _openedFrame = Engine.GetProcessFrames();
        _open = true;

        // Port of initTextbox: Link above $48 puts the box at tilemap offset
        // $0140 (y=80); otherwise it uses $0020 (y=8).
        Position = new Vector2(0, linkY < 0x48 ? 80 : 8);
        Visible = true;
        QueueRedraw();
    }

    public void Close()
    {
        _open = false;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!_open)
            return;

        QueueRedraw(); // Blink the original-style continue marker.
        if (Engine.GetProcessFrames() == _openedFrame ||
            (!Input.IsActionJustPressed("attack") && !Input.IsActionJustPressed("item")))
            return;

        if ((_page + 1) * LinesPerPage < _lines.Length)
        {
            _page++;
            QueueRedraw();
        }
        else
        {
            Close();
        }
    }

    public override void _Draw()
    {
        if (!_open)
            return;

        DrawRect(new Rect2(PanelX, 0, PanelWidth, PanelHeight), BackgroundColor);

        int firstLine = _page * LinesPerPage;
        for (int lineIndex = 0;
             lineIndex < LinesPerPage && firstLine + lineIndex < _lines.Length;
             lineIndex++)
        {
            DrawFontLine(_lines[firstLine + lineIndex], new Vector2(16, lineIndex * 8));
        }

        // updateTextboxArrow alternates tiles $02/$03 every 16 frames. Tile
        // $02 is blank; represent tile $03 with its small red down marker.
        if (((Engine.GetProcessFrames() >> 4) & 1) == 0)
        {
            DrawPolygon(
                new[] { new Vector2(146, 34), new Vector2(151, 34), new Vector2(148, 38) },
                new[] { ArrowColor });
        }
    }

    private void DrawFontLine(string line, Vector2 destination)
    {
        int length = Math.Min(line.Length, CharactersPerLine);
        for (int column = 0; column < length; column++)
        {
            int code = GetFontCode(line[column]);
            if (code == 0x20)
                continue;

            Rect2 source = new(
                (code & 0x0f) * 8,
                (code >> 4) * 16,
                8,
                16);
            DrawTextureRectRegion(
                _fontTexture,
                new Rect2(destination + new Vector2(column * 8, 0), new Vector2(8, 16)),
                source);
        }
    }

    private static int GetFontCode(char character)
    {
        return character switch
        {
            '↑' => 0x15,
            '↓' => 0x16,
            '←' => 0x17,
            '→' => 0x18,
            <= (char)0xff => character,
            _ => 0x3f
        };
    }

    private static Texture2D BuildFontTexture()
    {
        Texture2D sourceTexture = GD.Load<Texture2D>("res://assets/oracle/gfx/gfx_font.png");
        Image source = sourceTexture.GetImage();
        Image output = Image.CreateEmpty(source.GetWidth(), source.GetHeight(), false, Image.Format.Rgba8);

        for (int y = 0; y < source.GetHeight(); y++)
        for (int x = 0; x < source.GetWidth(); x++)
        {
            Color pixel = source.GetPixel(x, y);
            output.SetPixel(x, y, pixel.R > 0.5f ? TextColor : Colors.Transparent);
        }

        return ImageTexture.CreateFromImage(output);
    }

    private static Color GbcColor(int red, int green, int blue)
    {
        return new Color(red / 31.0f, green / 31.0f, blue / 31.0f);
    }
}
