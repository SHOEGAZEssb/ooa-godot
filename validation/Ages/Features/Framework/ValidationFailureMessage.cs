using System;
using System.Runtime.CompilerServices;

namespace Oracles;

// The compiler skips interpolation expressions entirely for a passing check.
// No capturing message factory or formatted diagnostic is allocated per update.
[InterpolatedStringHandler]
internal ref struct ValidationFailureMessage
{
    private DefaultInterpolatedStringHandler _text;

    public ValidationFailureMessage(int literalLength, int formattedCount, bool condition, out bool append)
    {
        append = condition;
        _text = condition ? new(literalLength, formattedCount) : default;
    }

    public void AppendLiteral(string value) => _text.AppendLiteral(value);
    public void AppendFormatted<T>(T value) => _text.AppendFormatted(value);
    public void AppendFormatted<T>(T value, string? format) => _text.AppendFormatted(value, format);
    public void AppendFormatted<T>(T value, int alignment, string? format = null) => _text.AppendFormatted(value, alignment, format);
    public void AppendFormatted(ReadOnlySpan<char> value) => _text.AppendFormatted(value);
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => _text.AppendFormatted(value, alignment, format);
    internal string GetText() => _text.ToStringAndClear();
}
