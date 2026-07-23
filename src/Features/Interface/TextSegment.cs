using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace oracleofages;
internal sealed class TextSegment
{
    public IReadOnlyList<TextLine> Lines { get; }

    public TextSegment(IEnumerable<TextLine> lines)
    {
        Lines = lines is IReadOnlyList<TextLine> list ? list : new List<TextLine>(lines);
    }
}
