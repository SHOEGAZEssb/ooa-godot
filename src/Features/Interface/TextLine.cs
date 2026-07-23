using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace oracleofages;
internal sealed class TextLine
{
    public static readonly TextLine Empty = new(Array.Empty<TextGlyph>(), Array.Empty<int>());
    public IReadOnlyList<TextGlyph> Glyphs { get; }
    public IReadOnlyList<int> OptionColumns { get; }
    public int HeartPieceColumn { get; }

    public TextLine(IEnumerable<TextGlyph> glyphs, IEnumerable<int> optionColumns, int heartPieceColumn = -1)
    {
        Glyphs = glyphs is IReadOnlyList<TextGlyph> list ? list : new List<TextGlyph>(glyphs);
        OptionColumns = optionColumns is IReadOnlyList<int> optionList ? optionList : new List<int>(optionColumns);
        HeartPieceColumn = heartPieceColumn;
    }
}
