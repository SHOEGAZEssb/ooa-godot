using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
    private const int TextSoundCooldownFrames = 4;
    private static readonly Rect2 ContinueMarkerRect = new(144, 32, 8, 8);

    // getCharacterDisplayLength reads byte 2 of each eight-byte row in
    // textbox.s:textSpeedData for wTextSpeed values $00-$04.
    private static readonly int[] CharacterDisplayFrames = { 7, 5, 4, 3, 2 };

    // textbox.s:@textColorData selects either the common background palette 0
    // (paletteData48e0) or PALH_0e's background palette 1 (paletteData4920).
    // Default color 0 is palette 0 color 2: the original warm white.
    private static readonly Color NormalTextColor = GbcColor(0x1f, 0x1a, 0x11);
    private static readonly Color RedTextColor = GbcColor(0x1d, 0x01, 0x03);
    private static readonly Color SecondaryRedTextColor = GbcColor(0x1f, 0x0e, 0x04);
    private static readonly Color BlueTextColor = GbcColor(0x04, 0x15, 0x1f);
    private static readonly Color AlternateLightTextColor = GbcColor(0x08, 0x1f, 0x00);
    private static readonly Color BackgroundColor = GbcColor(0x00, 0x00, 0x00);

    private readonly List<TextSegment> _segments = new();
    private Texture2D _fontTexture = null!;
    private Texture2D _symbolTexture = null!;
    private Texture2D _continueMarkerTexture = null!;
    private Texture2D[] _heartPieceTextures = null!;
    private string _currentMessage = string.Empty;
    private int _segmentIndex;
    private int _firstLineIndex;
    private int _visibleGlyphs;
    private int _messageSpeed = 4;
    private ulong _openedFrame;
    private double _arrowFrameCounter;
    private double _characterFrameAccumulator;
    private int _characterDisplayTimer;
    private int _textSoundCooldownCounter;
    private double _textScrollTickAccumulator;
    private float _textScrollOffset;
    private int _textScrollState;
    private double _heartPieceTickAccumulator;
    private int _heartPieceTimer;
    private int _heartPieceDisplayCount;
    private int _heartPieceLine = -1;
    private int _heartPieceColumn = -1;
    private bool _heartPieceSetComplete;
    private bool _open;
    private bool _consumeClosingInput;
    private bool _scrollingText;
    private bool _choiceActive;
    private int _selectedChoice;
    private int? _choiceResult;
    private Action<int> _playSound = static _ => { };
    private Func<int> _heartPieceCount = static () => 0;

    internal event Action? HeartPieceSetFilled;
    internal event Action? HeartPieceSetAccepted;

    public bool IsOpen => _open;
    public bool BlocksPlayerInput => _open || _consumeClosingInput;
    public int MessageSpeed
    {
        get => _messageSpeed;
        set
        {
            if (value is < 0 or > 4)
                throw new ArgumentOutOfRangeException(nameof(value));
            _messageSpeed = value;
        }
    }

    internal bool ArrowVisible => IsAwaitingNextMessage && _heartPieceTimer == 0 &&
        (((int)_arrowFrameCounter >> 4) & 1) != 0;
    internal bool HasNextMessage => _open && HasContinuation;
    internal bool IsPageComplete => _open && CurrentWindowGlyphCount == _visibleGlyphs;
    internal int VisibleLinesPerPage => LinesPerPage;
    internal int TextLineSpacing => LineSpacing;
    internal int CharacterDisplayFrameLength => CharacterDisplayFrames[_messageSpeed];
    internal int VisibleGlyphCount => _visibleGlyphs;
    internal bool IsScrollingText => _scrollingText;
    internal float TextScrollOffset => _textScrollOffset;
    internal string CurrentMessage => _currentMessage;
    internal bool ChoiceActive => _choiceActive;
    internal int SelectedChoice => _selectedChoice;
    internal bool HeartPieceDisplayActive => _heartPieceLine >= 0;
    internal int HeartPieceDisplayCount => _heartPieceDisplayCount;
    internal int HeartPieceDisplayTimer => _heartPieceTimer;
    internal static Color DefaultTextColorForValidation => NormalTextColor;
    internal static Rect2 ContinueMarkerRectForValidation => ContinueMarkerRect;

    private TextSegment CurrentSegment => _segments[_segmentIndex];
    private bool HasAnotherLine => _firstLineIndex + LinesPerPage < CurrentSegment.Lines.Count;
    private bool HasContinuation => HasAnotherLine || _segmentIndex + 1 < _segments.Count;
    private bool IsAwaitingNextMessage => _open && !_scrollingText &&
        IsPageComplete && HasContinuation;
    private int CurrentWindowGlyphCount =>
        CurrentLine(0).Glyphs.Count + CurrentLine(1).Glyphs.Count;

    public override void _Ready()
    {
        _fontTexture = BuildFontTexture("res://assets/oracle/gfx/gfx_font.png");
        _symbolTexture = BuildFontTexture("res://assets/oracle/gfx/gfx_font_jp.png");
        _continueMarkerTexture = BuildContinueMarkerTexture();
        _heartPieceTextures = BuildHeartPieceTextures();
    }

    internal void SetSoundPlayer(Action<int> playSound)
    {
        ArgumentNullException.ThrowIfNull(playSound);
        _playSound = playSound;
    }

    internal void SetHeartPieceCountProvider(Func<int> heartPieceCount)
    {
        ArgumentNullException.ThrowIfNull(heartPieceCount);
        _heartPieceCount = heartPieceCount;
    }

    public void ShowMessage(string message, float linkY)
    {
        ShowMessage(message, linkY, 0);
    }

    public void ShowMessage(string message, float linkY, int textPosition)
    {
        ArgumentNullException.ThrowIfNull(message);
        _segments.Clear();
        _segments.AddRange(ParseMessage(message));
        _currentMessage = PlainText(message);
        _segmentIndex = 0;
        _firstLineIndex = 0;
        _visibleGlyphs = 0;
        _openedFrame = Engine.GetProcessFrames();
        _open = true;
        _consumeClosingInput = false;
        _scrollingText = false;
        _choiceActive = false;
        _selectedChoice = 0;
        _choiceResult = null;
        _arrowFrameCounter = 0.0;
        _characterFrameAccumulator = 0.0;
        _characterDisplayTimer = CharacterDisplayFrames[_messageSpeed];
        _textSoundCooldownCounter = 0;
        _textScrollTickAccumulator = 0.0;
        _textScrollOffset = 0.0f;
        _textScrollState = 0;
        ResetHeartPieceDisplay();

        // Port of initTextbox: Link above $48 puts the box at tilemap offset
        // $0140 (y=80); otherwise it uses $0020 (y=8).
        // Text command \pos(2) explicitly selects the lower textbox. Without
        // a command, initTextbox chooses the side opposite Link.
        Position = new Vector2(0, textPosition == 2 || linkY < 0x48 ? 80 : 8);
        Visible = true;
        QueueRedraw();
    }

    internal void ShowChoiceMessage(
        string message,
        float linkY,
        int initialChoice = 0,
        int? textPosition = null)
    {
        if (textPosition.HasValue)
            ShowMessage(message, linkY, textPosition.Value);
        else
            ShowMessage(message, linkY);
        _choiceActive = true;
        _selectedChoice = Math.Max(0, initialChoice);
    }

    internal bool TryTakeChoiceResult(out int choice)
    {
        choice = _choiceResult ?? 0;
        if (!_choiceResult.HasValue)
            return false;
        _choiceResult = null;
        return true;
    }

    internal void SubmitChoiceForValidation(int choice)
    {
        if (!_choiceActive)
            throw new InvalidOperationException("No dialogue choice is active.");
        _selectedChoice = choice;
        SubmitChoice();
    }

    public void Close()
    {
        _open = false;
        _scrollingText = false;
        _choiceActive = false;
        ResetHeartPieceDisplay();
        Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_consumeClosingInput &&
            !Input.IsActionPressed("attack") && !Input.IsActionPressed("item"))
        {
            _consumeClosingInput = false;
        }

        if (!_open)
            return;

        if (UpdateHeartPieceDisplay(delta))
        {
            QueueRedraw();
            return;
        }

        if (_scrollingText)
        {
            UpdateTextScroll(delta);
            return;
        }

        if (IsPageComplete)
        {
            if (HasContinuation)
                _arrowFrameCounter += delta * 60.0;
        }
        else
        {
            UpdateCharacterDisplay(delta);
        }

        QueueRedraw();
        if (Engine.GetProcessFrames() == _openedFrame ||
            (!Input.IsActionJustPressed("attack") && !Input.IsActionJustPressed("item") &&
             !Input.IsActionJustPressed("move_left") && !Input.IsActionJustPressed("move_right") &&
             !Input.IsActionJustPressed("move_up") && !Input.IsActionJustPressed("move_down")))
            return;

        if (_choiceActive && IsPageComplete && !HasContinuation)
        {
            int choiceCount = CurrentLine(0).OptionColumns.Count +
                CurrentLine(1).OptionColumns.Count;
            if (choiceCount > 0 &&
                (Input.IsActionJustPressed("move_left") ||
                 Input.IsActionJustPressed("move_right") ||
                 Input.IsActionJustPressed("move_up") ||
                 Input.IsActionJustPressed("move_down")))
            {
                int choiceDelta = Input.IsActionJustPressed("move_left") ||
                    Input.IsActionJustPressed("move_up") ? -1 : 1;
                MoveChoice(choiceDelta);
                return;
            }
            if (Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("item"))
            {
                SubmitChoice();
                return;
            }
        }

        AdvanceOrClose();
    }

    internal void AdvanceOrClose()
    {
        if (!_open || _scrollingText)
            return;

        if (_heartPieceTimer > 0)
            return;
        if (_heartPieceSetComplete)
        {
            _heartPieceSetComplete = false;
            HeartPieceSetAccepted?.Invoke();
            return;
        }

        if (!IsPageComplete)
        {
            RevealCurrentLine();
            QueueRedraw();
            return;
        }

        if (HasContinuation)
            _playSound(OracleSoundEngine.SndText2);

        if (HasAnotherLine)
        {
            _scrollingText = true;
            _arrowFrameCounter = 0.0;
            _textScrollTickAccumulator = 0.0;
            _textScrollState = 0;
            // standardTextStateb runs on the button-press frame itself and
            // shifts the first of the two 8px tile rows.
            _textScrollOffset = 8.0f;
            QueueRedraw();
        }
        else if (_segmentIndex + 1 < _segments.Count)
        {
            // \stop clears the old text before the following segment starts.
            _segmentIndex++;
            _firstLineIndex = 0;
            ResetHeartPieceDisplay();
            ResetCharacterDisplay(0);
            QueueRedraw();
        }
        else
        {
            if (_choiceActive)
            {
                SubmitChoice();
                return;
            }
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

        if (_scrollingText)
        {
            DrawFontLineClipped(CurrentLine(0), new Vector2(16, -_textScrollOffset),
                CurrentLine(0).Glyphs.Count);
            DrawFontLineClipped(CurrentLine(1),
                new Vector2(16, LineSpacing - _textScrollOffset),
                CurrentLine(1).Glyphs.Count);
        }
        else
        {
            int visible = _visibleGlyphs;
            for (int lineIndex = 0; lineIndex < LinesPerPage; lineIndex++)
            {
                TextLine line = CurrentLine(lineIndex);
                int lineVisible = Math.Min(visible, line.Glyphs.Count);
                DrawFontLine(line, new Vector2(16, lineIndex * LineSpacing), lineVisible);
                visible = Math.Max(0, visible - line.Glyphs.Count);
            }
        }

        // updateTextboxArrow alternates blank tile $02 and arrow tile $03
        // every 16 updates. It runs only while state $05 is waiting for more
        // text, so the final message of a dialogue never receives an arrow.
        if (ArrowVisible)
            DrawTextureRect(_continueMarkerTexture, ContinueMarkerRect, false, RedTextColor);

        if (_choiceActive && IsPageComplete && !HasContinuation)
            DrawChoiceCursor();

        DrawHeartPieceDisplay();
    }

    internal void AdvanceArrowClockForValidation(double delta)
    {
        if (IsAwaitingNextMessage)
            _arrowFrameCounter += delta * 60.0;
    }

    internal void AdvanceCharacterClockForValidation(double delta)
    {
        UpdateCharacterDisplay(delta);
    }

    internal void RevealCurrentPageForValidation()
    {
        _visibleGlyphs = CurrentWindowGlyphCount;
        _characterFrameAccumulator = 0.0;
        _arrowFrameCounter = 0.0;
        TryStartVisibleHeartPieceDisplay();
        QueueRedraw();
    }

    internal void AdvanceHeartPieceClockForValidation(double delta) =>
        UpdateHeartPieceDisplay(delta);

    internal ulong HeartPiecePixelHashForValidation()
    {
        if (_heartPieceLine < 0)
            return 0;
        Image image = _heartPieceTextures[_heartPieceDisplayCount].GetImage();
        ulong hash = 1469598103934665603UL;
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
        {
            Color pixel = image.GetPixel(x, y);
            hash = (hash ^ (byte)Mathf.RoundToInt(pixel.R * 255.0f)) * 1099511628211UL;
            hash = (hash ^ (byte)Mathf.RoundToInt(pixel.G * 255.0f)) * 1099511628211UL;
            hash = (hash ^ (byte)Mathf.RoundToInt(pixel.B * 255.0f)) * 1099511628211UL;
        }
        return hash;
    }

    internal void AdvanceTextScrollForValidation(double delta)
    {
        UpdateTextScroll(delta);
    }

    internal void MoveChoiceForValidation(int delta) => MoveChoice(delta);

    internal int GlyphColorForValidation(int segment, int line, int column) =>
        _segments[segment].Lines[line].Glyphs[column].ColorIndex;

    internal bool GlyphUsesSymbolFontForValidation(int segment, int line, int column) =>
        _segments[segment].Lines[line].Glyphs[column].Source == FontSource.Symbol;

    internal int GlyphCodeForValidation(int segment, int line, int column) =>
        _segments[segment].Lines[line].Glyphs[column].Code;

    internal int ContinueMarkerOpaquePixelCountForValidation()
    {
        Image marker = _continueMarkerTexture.GetImage();
        int count = 0;
        for (int y = 0; y < marker.GetHeight(); y++)
        for (int x = 0; x < marker.GetWidth(); x++)
        {
            if (marker.GetPixel(x, y).A > 0.5f)
                count++;
        }
        return count;
    }

    internal static string PlainText(string message)
    {
        string text = message.Replace("\r", string.Empty);
        text = text.Replace("\\sym(0x1c)", "♪", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("\\sym(0x57)", "▲", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("\\left", "←", StringComparison.Ordinal);
        text = text.Replace("\\right", "→", StringComparison.Ordinal);
        text = text.Replace("\\up", "↑", StringComparison.Ordinal);
        text = text.Replace("\\down", "↓", StringComparison.Ordinal);
        text = Regex.Replace(text, @"\\heart(?![A-Za-z])", "♥");
        text = Regex.Replace(text, @"\\heartpiece", string.Empty,
            RegexOptions.IgnoreCase);
        return Regex.Replace(
            text,
            @"\\(?:stop(?:\(\))?|pos\([^)]*\)|col\([^)]*\)|opt\([^)]*\)|" +
            @"sfx\([^)]*\)|charsfx\([^)]*\))",
            string.Empty,
            RegexOptions.IgnoreCase);
    }

    private void SubmitChoice()
    {
        _playSound(OracleSoundEngine.SndSelectItem);
        _choiceResult = _selectedChoice;
        Close();
        _consumeClosingInput = true;
    }

    private void MoveChoice(int delta)
    {
        if (!_choiceActive || !IsPageComplete || HasContinuation)
            return;

        int choiceCount = CurrentLine(0).OptionColumns.Count +
            CurrentLine(1).OptionColumns.Count;
        if (choiceCount == 0)
            return;

        _playSound(OracleSoundEngine.SndMenuMove);
        _selectedChoice =
            (_selectedChoice + delta % choiceCount + choiceCount) % choiceCount;
        QueueRedraw();
    }

    private void DrawChoiceCursor()
    {
        int optionIndex = 0;
        for (int lineIndex = 0; lineIndex < LinesPerPage; lineIndex++)
        {
            foreach (int column in CurrentLine(lineIndex).OptionColumns)
            {
                if (optionIndex++ != _selectedChoice)
                    continue;
                var cursorLine = new TextLine(
                    new[] { new TextGlyph('>', FontSource.Main, 0, 0, 0) },
                    Array.Empty<int>());
                DrawFontLine(
                    cursorLine,
                    new Vector2(16 + Math.Max(0, column - 1) * 8,
                        lineIndex * LineSpacing),
                    1);
                return;
            }
        }
    }

    private void UpdateCharacterDisplay(double delta)
    {
        if (!_open || _scrollingText || IsPageComplete)
            return;

        _characterFrameAccumulator += delta * 60.0;
        while (_visibleGlyphs < CurrentWindowGlyphCount &&
               _characterFrameAccumulator >= 1.0)
        {
            _characterFrameAccumulator -= 1.0;
            if (_textSoundCooldownCounter > 0)
                _textSoundCooldownCounter--;
            if (--_characterDisplayTimer > 0)
                continue;

            RevealNextGlyph();
            _characterDisplayTimer = CharacterDisplayFrames[_messageSpeed];
        }
        if (IsPageComplete)
        {
            _characterFrameAccumulator = 0.0;
            _arrowFrameCounter = 0.0;
        }
        QueueRedraw();
    }

    private void RevealCurrentLine()
    {
        int topLineLength = CurrentLine(0).Glyphs.Count;
        int targetGlyphs = _visibleGlyphs < topLineLength
            ? topLineLength
            : CurrentWindowGlyphCount;
        while (_visibleGlyphs < targetGlyphs)
            RevealNextGlyph();
        _characterFrameAccumulator = 0.0;
        _characterDisplayTimer = CharacterDisplayFrames[_messageSpeed];
        if (IsPageComplete)
            _arrowFrameCounter = 0.0;
    }

    private void ResetCharacterDisplay(int alreadyVisible)
    {
        _visibleGlyphs = alreadyVisible;
        _characterFrameAccumulator = 0.0;
        _characterDisplayTimer = CharacterDisplayFrames[_messageSpeed];
        _arrowFrameCounter = 0.0;
    }

    private void RevealNextGlyph()
    {
        TextLine firstLine = CurrentLine(0);
        TextLine line;
        int column;
        int windowLine;
        if (_visibleGlyphs < firstLine.Glyphs.Count)
        {
            line = firstLine;
            column = _visibleGlyphs;
            windowLine = 0;
        }
        else
        {
            line = CurrentLine(1);
            column = _visibleGlyphs - firstLine.Glyphs.Count;
            windowLine = 1;
        }

        TextGlyph glyph = line.Glyphs[column];
        _visibleGlyphs++;
        if (line.HeartPieceColumn == column + 1)
        {
            StartHeartPieceDisplay(
                _firstLineIndex + windowLine, line.HeartPieceColumn);
        }

        // displayNextTextCharacter branches to @endLine before reading the
        // sound buffers for column $0f.
        if (column + 1 == CharactersPerLine)
            return;

        if (glyph.Code != ' ' && glyph.CharacterSound != 0 &&
            _textSoundCooldownCounter == 0)
        {
            _textSoundCooldownCounter = TextSoundCooldownFrames;
            _playSound(glyph.CharacterSound);
        }
        if (glyph.SoundEffect != 0)
            _playSound(glyph.SoundEffect);
    }

    private bool UpdateHeartPieceDisplay(double delta)
    {
        if (_heartPieceLine < 0 || _heartPieceTimer == 0)
            return false;

        _heartPieceTickAccumulator += delta * 60.0;
        while (_heartPieceTimer > 0 && _heartPieceTickAccumulator >= 1.0)
        {
            _heartPieceTickAccumulator -= 1.0;
            _heartPieceTimer--;
        }
        if (_heartPieceTimer > 0)
            return true;

        int pieces = Math.Clamp(_heartPieceCount(), 0, 4);
        _heartPieceDisplayCount = pieces;
        _playSound(OracleSoundEngine.SndText2);
        if (pieces == 4)
        {
            _heartPieceSetComplete = true;
            HeartPieceSetFilled?.Invoke();
        }
        _arrowFrameCounter = 0.0;
        return false;
    }

    private void StartHeartPieceDisplay(int line, int column)
    {
        if (_heartPieceLine >= 0)
            return;
        int pieces = Math.Clamp(_heartPieceCount(), 1, 4);
        _heartPieceLine = line;
        _heartPieceColumn = column;
        _heartPieceDisplayCount = pieces - 1;
        _heartPieceTimer = 30;
        _heartPieceTickAccumulator = 0.0;
        _heartPieceSetComplete = false;
        _arrowFrameCounter = 0.0;
    }

    private void TryStartVisibleHeartPieceDisplay()
    {
        int visible = _visibleGlyphs;
        for (int windowLine = 0; windowLine < LinesPerPage; windowLine++)
        {
            TextLine line = CurrentLine(windowLine);
            if (line.HeartPieceColumn >= 0 &&
                visible >= line.HeartPieceColumn)
            {
                StartHeartPieceDisplay(
                    _firstLineIndex + windowLine, line.HeartPieceColumn);
                return;
            }
            visible = Math.Max(0, visible - line.Glyphs.Count);
        }
    }

    private void ResetHeartPieceDisplay()
    {
        _heartPieceTickAccumulator = 0.0;
        _heartPieceTimer = 0;
        _heartPieceDisplayCount = 0;
        _heartPieceLine = -1;
        _heartPieceColumn = -1;
        _heartPieceSetComplete = false;
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
                case 0: // state c: DMA after standardTextStateb's first shift
                    break;
                case 1: // state d: shift the second 8px tile row
                    _textScrollOffset = 16.0f;
                    break;
                default: // state e: preserve the old bottom line at the top
                    _firstLineIndex++;
                    _scrollingText = false;
                    _textScrollOffset = 0.0f;
                    _textScrollState = 0;
                    ResetCharacterDisplay(CurrentLine(0).Glyphs.Count);
                    break;
            }
        }
        QueueRedraw();
    }

    private TextLine CurrentLine(int windowLine)
    {
        int index = _firstLineIndex + windowLine;
        return index < CurrentSegment.Lines.Count
            ? CurrentSegment.Lines[index]
            : TextLine.Empty;
    }

    private void DrawFontLineClipped(TextLine line, Vector2 destination, int visibleGlyphs)
    {
        int length = Math.Min(Math.Min(line.Glyphs.Count, visibleGlyphs), CharactersPerLine);
        float visibleTop = Mathf.Max(0.0f, destination.Y);
        float visibleBottom = Mathf.Min(TextAreaHeight, destination.Y + 16.0f);
        float visibleHeight = visibleBottom - visibleTop;
        if (visibleHeight <= 0.0f)
            return;

        float sourceYOffset = visibleTop - destination.Y;
        for (int column = 0; column < length; column++)
        {
            TextGlyph glyph = line.Glyphs[column];
            if (glyph.Code == 0x20)
                continue;
            Rect2 source = new(
                (glyph.Code & 0x0f) * 8,
                (glyph.Code >> 4) * 16 + sourceYOffset,
                8,
                visibleHeight);
            Rect2 target = new(
                destination.X + column * 8,
                visibleTop,
                8,
                visibleHeight);
            DrawTextureRectRegion(
                TextureFor(glyph), target, source, ColorFor(glyph.ColorIndex));
        }
    }

    private void DrawFontLine(TextLine line, Vector2 destination, int visibleGlyphs)
    {
        int length = Math.Min(Math.Min(line.Glyphs.Count, visibleGlyphs), CharactersPerLine);
        for (int column = 0; column < length; column++)
        {
            TextGlyph glyph = line.Glyphs[column];
            if (glyph.Code == 0x20)
                continue;

            Rect2 source = new(
                (glyph.Code & 0x0f) * 8,
                (glyph.Code >> 4) * 16,
                8,
                16);
            DrawTextureRectRegion(
                TextureFor(glyph),
                new Rect2(destination + new Vector2(column * 8, 0), new Vector2(8, 16)),
                source,
                ColorFor(glyph.ColorIndex));
        }
    }

    private void DrawHeartPieceDisplay()
    {
        if (_heartPieceLine < _firstLineIndex ||
            _heartPieceLine >= _firstLineIndex + LinesPerPage)
            return;

        Vector2 destination = new(
            16 + _heartPieceColumn * 8,
            (_heartPieceLine - _firstLineIndex) * LineSpacing);
        DrawTexture(_heartPieceTextures[_heartPieceDisplayCount], destination);
    }

    private Texture2D TextureFor(TextGlyph glyph) =>
        glyph.Source == FontSource.Symbol ? _symbolTexture : _fontTexture;

    private static Color ColorFor(int colorIndex) => colorIndex switch
    {
        1 => RedTextColor,
        2 => SecondaryRedTextColor,
        3 => BlueTextColor,
        4 => AlternateLightTextColor,
        _ => NormalTextColor
    };

    private static List<TextSegment> ParseMessage(string message)
    {
        var segments = new List<TextSegment>();
        var lines = new List<TextLine>();
        var glyphs = new List<TextGlyph>();
        var optionColumns = new List<int>();
        int heartPieceColumn = -1;
        int colorIndex = 0;
        int characterSound = OracleSoundEngine.SndText;
        int pendingSoundEffect = 0;
        bool skipNextNewline = false;

        void AddGlyph(int code, FontSource source = FontSource.Main)
        {
            glyphs.Add(new TextGlyph(
                code, source, colorIndex, characterSound, pendingSoundEffect));
            pendingSoundEffect = 0;
        }

        void AddCharacter(char character)
        {
            switch (character)
            {
                case '♪': AddGlyph(0x1c, FontSource.Symbol); break;
                case '▲': AddGlyph(0x57, FontSource.Symbol); break;
                case '♥': AddGlyph(0x14); break;
                case '↑': AddGlyph(0x15); break;
                case '↓': AddGlyph(0x16); break;
                case '←': AddGlyph(0x17); break;
                case '→': AddGlyph(0x18); break;
                default: AddGlyph(character <= 0xff ? character : 0x3f); break;
            }
        }

        void FinishLine()
        {
            lines.Add(new TextLine(glyphs, optionColumns, heartPieceColumn));
            glyphs = new List<TextGlyph>();
            optionColumns = new List<int>();
            heartPieceColumn = -1;
        }

        void FinishSegment()
        {
            if (lines.Count == 0)
                FinishLine();
            segments.Add(new TextSegment(lines));
            lines = new List<TextLine>();
        }

        string source = message.Replace("\r", string.Empty);
        for (int index = 0; index < source.Length;)
        {
            char character = source[index];
            if (character == '\n')
            {
                index++;
                if (skipNextNewline)
                {
                    skipNextNewline = false;
                    continue;
                }
                FinishLine();
                continue;
            }
            skipNextNewline = false;

            if (character != '\\')
            {
                AddCharacter(character);
                index++;
                continue;
            }

            int tokenStart = index++;
            int nameStart = index;
            while (index < source.Length && char.IsLetter(source[index]))
                index++;
            if (index == nameStart)
            {
                AddCharacter('\\');
                continue;
            }

            string name = source[nameStart..index].ToLowerInvariant();
            string argument = string.Empty;
            if (index < source.Length && source[index] == '(')
            {
                int argumentStart = ++index;
                while (index < source.Length && source[index] != ')')
                    index++;
                argument = source[argumentStart..index];
                if (index < source.Length)
                    index++;
            }

            switch (name)
            {
                case "col":
                    if (TryParseCommandNumber(argument, out int requestedColor) &&
                        requestedColor < 0x80)
                    {
                        colorIndex = requestedColor;
                    }
                    break;
                case "stop":
                    FinishLine();
                    FinishSegment();
                    skipNextNewline = true;
                    break;
                case "sym":
                    if (TryParseCommandNumber(argument, out int symbolCode))
                        AddGlyph(symbolCode, FontSource.Symbol);
                    break;
                case "circle": AddGlyph(0x10); break;
                case "club": AddGlyph(0x11); break;
                case "diamond": AddGlyph(0x12); break;
                case "spade": AddGlyph(0x13); break;
                case "heart": AddGlyph(0x14); break;
                // Control $0c:$05 stops the current textbox span, draws its
                // 2x2 quarter-heart map at the current cursor, then holds for
                // 30 updates before revealing the newly collected quarter.
                case "heartpiece":
                    heartPieceColumn = glyphs.Count;
                    FinishLine();
                    FinishSegment();
                    skipNextNewline = true;
                    break;
                case "up": AddGlyph(0x15); break;
                case "down": AddGlyph(0x16); break;
                case "left": AddGlyph(0x17); break;
                case "right": AddGlyph(0x18); break;
                case "times": AddGlyph(0x19); break;
                case "triangle": AddGlyph(0x7e); break;
                case "rectangle": AddGlyph(0x7f); break;
                case "abtn": AddGlyph(0xb8); AddGlyph(0xb9); break;
                case "bbtn": AddGlyph(0xba); AddGlyph(0xbb); break;
                case "n":
                    FinishLine();
                    break;
                case "opt":
                    optionColumns.Add(glyphs.Count);
                    break;
                case "sfx":
                    if (TryParseCommandNumber(argument, out int requestedSoundEffect))
                        pendingSoundEffect = requestedSoundEffect & 0xff;
                    break;
                case "charsfx":
                    if (TryParseCommandNumber(argument, out int requestedCharacterSound))
                        characterSound = requestedCharacterSound & 0xff;
                    break;
                case "pos":
                case "slow":
                case "speed":
                case "wait":
                    break;
                default:
                    // Unsupported substitutions remain readable instead of
                    // silently deleting information from partially ported text.
                    for (int tokenIndex = tokenStart; tokenIndex < index; tokenIndex++)
                        AddCharacter(source[tokenIndex]);
                    break;
            }
        }

        if (glyphs.Count > 0 || lines.Count > 0 || segments.Count == 0)
        {
            FinishLine();
            FinishSegment();
        }
        return segments;
    }

    private static bool TryParseCommandNumber(string value, out int result)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber,
                null, out result);
        return int.TryParse(value, out result);
    }

    private static Texture2D BuildFontTexture(string path)
    {
        Image source = LoadSourceImage(path);
        Image output = Image.CreateEmpty(
            source.GetWidth(), source.GetHeight(), false, Image.Format.Rgba8);

        for (int y = 0; y < source.GetHeight(); y++)
        for (int x = 0; x < source.GetWidth(); x++)
        {
            Color pixel = source.GetPixel(x, y);
            output.SetPixel(x, y, pixel.R > 0.5f ? Colors.White : Colors.Transparent);
        }

        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildContinueMarkerTexture()
    {
        // updateTextboxArrow writes HUD tile $03; tile $02 is its blank frame.
        Image source = LoadSourceImage("res://assets/oracle/gfx/gfx_hud.png");
        Image output = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            Color pixel = source.GetPixel(3 * 8 + x, y);
            // gfx_hud uses inverted 2bpp grayscale. Tile $03's arrow pixels
            // are shade 1 (PNG value 170); shade 3 is the transparent black.
            output.SetPixel(x, y, pixel.R > 0.5f ? Colors.White : Colors.Transparent);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D[] BuildHeartPieceTextures()
    {
        // func_53eb loads four tiles at $5d/$5f/$7c/$7e. Its table selects
        // outline or filled left halves and attribute bit 5 mirrors the right
        // halves. PNG shades retain the PALH_0e background-palette-0 indices.
        Image source = LoadSourceImage(
            "res://assets/oracle/gfx/gfx_font_heartpiece.png");
        if (source.GetWidth() != 64 || source.GetHeight() != 8)
        {
            throw new InvalidOperationException(
                "gfx_font_heartpiece must contain eight horizontal 8x8 tiles.");
        }

        int[,] sourceTiles =
        {
            { 0, 2, 0, 2 },
            { 1, 2, 0, 2 },
            { 1, 3, 0, 2 },
            { 1, 3, 0, 3 },
            { 1, 3, 1, 3 }
        };
        var textures = new Texture2D[5];
        for (int pieces = 0; pieces < textures.Length; pieces++)
        {
            Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                bool right = quadrant >= 2;
                bool bottom = (quadrant & 1) != 0;
                int tile = sourceTiles[pieces, quadrant];
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    int sourceX = tile * 8 + (right ? 7 - x : x);
                    Color shade = source.GetPixel(sourceX, y);
                    int paletteIndex = 3 - Math.Clamp(
                        Mathf.RoundToInt(shade.R * 3.0f), 0, 3);
                    Color color = paletteIndex switch
                    {
                        0 => GbcColor(0x0d, 0x01, 0x05),
                        1 => RedTextColor,
                        2 => NormalTextColor,
                        _ => BackgroundColor
                    };
                    output.SetPixel((right ? 8 : 0) + x,
                        (bottom ? 8 : 0) + y, color);
                }
            }
            textures[pieces] = ImageTexture.CreateFromImage(output);
        }
        return textures;
    }

    private static Image LoadSourceImage(string path)
    {
        return OracleGraphicsCache.LoadImage(path);
    }

    private static Color GbcColor(int red, int green, int blue)
    {
        return new Color(red / 31.0f, green / 31.0f, blue / 31.0f);
    }

    private enum FontSource
    {
        Main,
        Symbol
    }

    private readonly record struct TextGlyph(
        int Code,
        FontSource Source,
        int ColorIndex,
        int CharacterSound,
        int SoundEffect);

    private sealed class TextLine
    {
        public static readonly TextLine Empty = new(
            Array.Empty<TextGlyph>(), Array.Empty<int>());
        public IReadOnlyList<TextGlyph> Glyphs { get; }
        public IReadOnlyList<int> OptionColumns { get; }
        public int HeartPieceColumn { get; }

        public TextLine(
            IEnumerable<TextGlyph> glyphs,
            IEnumerable<int> optionColumns,
            int heartPieceColumn = -1)
        {
            Glyphs = glyphs is IReadOnlyList<TextGlyph> list
                ? list
                : new List<TextGlyph>(glyphs);
            OptionColumns = optionColumns is IReadOnlyList<int> optionList
                ? optionList
                : new List<int>(optionColumns);
            HeartPieceColumn = heartPieceColumn;
        }
    }

    private sealed class TextSegment
    {
        public IReadOnlyList<TextLine> Lines { get; }

        public TextSegment(IEnumerable<TextLine> lines)
        {
            Lines = lines is IReadOnlyList<TextLine> list
                ? list
                : new List<TextLine>(lines);
        }
    }
}
