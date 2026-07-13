using Godot;
using System;

namespace oracleofages;

public partial class DialogueBox : Node2D
{
    private const int LinesPerPage = 2;
    private const int LineSpacing = 16;
    private const int CharactersPerLine = 16;
    private const int PanelX = 8;
    private const int PanelWidth = 18 * 8;
    private const int PanelHeight = 5 * 8;
    private const int TextAreaHeight = LinesPerPage * LineSpacing;

    // PALH_0e / paletteData4920: normal textbox text uses color 2 and the
    // background uses color 3. The continue arrow uses color 0.
    private static readonly Color TextColor = GbcColor(0x08, 0x1f, 0x00);
    private static readonly Color ArrowColor = GbcColor(0x1f, 0x0e, 0x04);
    private static readonly Color BackgroundColor = GbcColor(0x00, 0x00, 0x00);

    private Texture2D _fontTexture = null!;
    private string[] _lines = Array.Empty<string>();
    private int _page;
    private ulong _openedFrame;
    private double _arrowFrameCounter;
    private double _textScrollTickAccumulator;
    private float _textScrollOffset;
    private int _textScrollState;
    private bool _open;
    private bool _consumeClosingInput;
    private bool _scrollingText;

    public bool IsOpen => _open;
    public bool BlocksPlayerInput => _open || _consumeClosingInput;
    internal bool ArrowVisible => (((int)_arrowFrameCounter >> 4) & 1) == 0;
    internal int VisibleLinesPerPage => LinesPerPage;
    internal int TextLineSpacing => LineSpacing;
    internal bool IsScrollingText => _scrollingText;
    internal float TextScrollOffset => _textScrollOffset;
    internal string CurrentMessage => string.Join("\n", _lines);

    public override void _Ready()
    {
        _fontTexture = BuildFontTexture();
    }

    public void ShowMessage(string message, float linkY)
    {
        ShowMessage(message, linkY, 0);
    }

    public void ShowMessage(string message, float linkY, int textPosition)
    {
        _lines = message.Replace("\r", "").Split('\n');
        _page = 0;
        _openedFrame = Engine.GetProcessFrames();
        _open = true;
        _consumeClosingInput = false;
        _scrollingText = false;
        _textScrollTickAccumulator = 0.0;
        _textScrollOffset = 0.0f;
        _textScrollState = 0;

        // Port of initTextbox: Link above $48 puts the box at tilemap offset
        // $0140 (y=80); otherwise it uses $0020 (y=8).
        // Text command \pos(2) explicitly selects the lower textbox. Without
        // a command, initTextbox chooses the side opposite Link.
        Position = new Vector2(0, textPosition == 2 || linkY < 0x48 ? 80 : 8);
        Visible = true;
        QueueRedraw();
    }

    public void Close()
    {
        _open = false;
        _scrollingText = false;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        // wFrameCounter advances at the GBC's update rate. Using elapsed time
        // keeps the 16-frame arrow toggle independent of the monitor refresh rate.
        _arrowFrameCounter += delta * 60.0;
        if (_consumeClosingInput &&
            !Input.IsActionPressed("attack") && !Input.IsActionPressed("item"))
        {
            _consumeClosingInput = false;
        }

        if (!_open)
            return;

        QueueRedraw(); // Blink the original-style continue marker.
        if (_scrollingText)
        {
            UpdateTextScroll(delta);
            return;
        }
        if (Engine.GetProcessFrames() == _openedFrame ||
            (!Input.IsActionJustPressed("attack") && !Input.IsActionJustPressed("item")))
            return;

        AdvanceOrClose();
    }

    internal void AdvanceOrClose()
    {
        if (!_open)
            return;

        if ((_page + 1) * LinesPerPage < _lines.Length)
        {
            _scrollingText = true;
            _textScrollTickAccumulator = 0.0;
            _textScrollState = 0;
            // standardTextStateb runs on the button-press frame itself.
            _textScrollOffset = 8.0f;
            QueueRedraw();
        }
        else
        {
            Close();
            // The original main loop cannot run Link's interaction code again
            // for the same wKeysJustPressed value. Hold the closing press until
            // both face buttons have been released to preserve that ordering.
            _consumeClosingInput = true;
        }
    }

    public override void _Draw()
    {
        if (!_open)
            return;

        DrawRect(new Rect2(PanelX, 0, PanelWidth, PanelHeight), BackgroundColor);

        int firstLine = _page * LinesPerPage;
        if (_scrollingText)
        {
            float offset = TextScrollOffset;
            for (int lineIndex = 0; lineIndex < LinesPerPage * 2; lineIndex++)
            {
                int textLine = firstLine + lineIndex;
                if (textLine >= _lines.Length)
                    break;
                // The engine redraws each incoming line only after its pair of
                // 8px map shifts; it is not visible below the old text early.
                if (lineIndex == 2 && offset < 16.0f)
                    continue;
                if (lineIndex == 3 && offset < 32.0f)
                    continue;
                DrawFontLineClipped(
                    _lines[textLine],
                    new Vector2(16, lineIndex * LineSpacing - offset));
            }
        }
        else
        {
            for (int lineIndex = 0;
                 lineIndex < LinesPerPage && firstLine + lineIndex < _lines.Length;
                 lineIndex++)
            {
                DrawFontLine(_lines[firstLine + lineIndex], new Vector2(16, lineIndex * LineSpacing));
            }
        }

        // updateTextboxArrow alternates tiles $02/$03 every 16 frames. Tile
        // $02 is blank; represent tile $03 with its small red down marker.
        if (!_scrollingText && ArrowVisible)
        {
            DrawPolygon(
                new[] { new Vector2(146, 34), new Vector2(151, 34), new Vector2(148, 38) },
                new[] { ArrowColor });
        }
    }

    internal void AdvanceArrowClockForValidation(double delta)
    {
        _arrowFrameCounter += delta * 60.0;
    }

    internal void AdvanceTextScrollForValidation(double delta)
    {
        UpdateTextScroll(delta);
    }

    private void UpdateTextScroll(double delta)
    {
        if (!_scrollingText)
            return;

        _textScrollTickAccumulator += delta * 60.0;
        while (_scrollingText && _textScrollTickAccumulator >= 1.0)
        {
            _textScrollTickAccumulator -= 1.0;
            switch (_textScrollState++)
            {
                case 0: // state 6/c: DMA the first 8px shift
                    break;
                case 1: // state 7/d: shift the second tile row
                    _textScrollOffset = 16.0f;
                    break;
                case 2: // state 8/e: redraw the next line
                    break;
                case 3: // state b for the following line
                    _textScrollOffset = 24.0f;
                    break;
                case 4: // state c: DMA
                    break;
                case 5: // state d: second tile-row shift
                    _textScrollOffset = 32.0f;
                    break;
                default:
                    _page++;
                    _scrollingText = false;
                    _textScrollOffset = 0.0f;
                    _textScrollState = 0;
                    break;
            }
        }
        QueueRedraw();
    }

    private void DrawFontLineClipped(string line, Vector2 destination)
    {
        int length = Math.Min(line.Length, CharactersPerLine);
        float visibleTop = Mathf.Max(0.0f, destination.Y);
        float visibleBottom = Mathf.Min(TextAreaHeight, destination.Y + 16.0f);
        float visibleHeight = visibleBottom - visibleTop;
        if (visibleHeight <= 0.0f)
            return;

        float sourceYOffset = visibleTop - destination.Y;
        for (int column = 0; column < length; column++)
        {
            int code = GetFontCode(line[column]);
            if (code == 0x20)
                continue;
            Rect2 source = new(
                (code & 0x0f) * 8,
                (code >> 4) * 16 + sourceYOffset,
                8,
                visibleHeight);
            Rect2 target = new(
                destination.X + column * 8,
                visibleTop,
                8,
                visibleHeight);
            DrawTextureRectRegion(_fontTexture, target, source);
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
