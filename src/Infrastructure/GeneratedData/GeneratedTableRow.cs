using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace oracleofages;

internal sealed class GeneratedTableRow
{
    private readonly string[] _values;
    private readonly GeneratedTableSchema _schema;

    public string Path { get; }
    public int LineNumber { get; }
    public int Count => _values.Length;

    internal GeneratedTableRow(
        string path,
        int lineNumber,
        GeneratedTableSchema schema,
        string[] values)
    {
        Path = path;
        LineNumber = lineNumber;
        _schema = schema;
        _values = values;
    }

    public string String(int column) => Field(column);

    public string RequiredString(int column)
    {
        string value = Field(column);
        if (value.Length == 0)
            throw Error(column, value, "a nonempty string");
        return value;
    }

    public int HexByte(int column) => ParseHex(column, byte.MinValue, byte.MaxValue, "a hexadecimal byte (00-ff)");
    public int HexWord(int column) => ParseHex(column, ushort.MinValue, ushort.MaxValue, "a hexadecimal word (0000-ffff)");
    public int HexInt(int column) => ParseHex(column, 0, int.MaxValue, "a nonnegative hexadecimal integer");
    public int Decimal(int column) => ParseDecimal(column, int.MinValue, int.MaxValue, "a signed decimal integer");
    public int Decimal(int column, int minimum, int maximum) =>
        ParseDecimal(column, minimum, maximum, $"a decimal integer in [{minimum}, {maximum}]");
    public int UnsignedDecimal(int column) =>
        ParseDecimal(column, 0, int.MaxValue, "a nonnegative decimal integer");
    public float FiniteFloat(int column)
    {
        string value = Field(column);
        if (!float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float parsed) || !float.IsFinite(parsed))
        {
            throw Error(column, value, "a finite invariant-culture number");
        }
        return parsed;
    }
    public bool Boolean01(int column)
    {
        int value = ParseDecimal(column, 0, 1, "boolean 0 or 1");
        return value != 0;
    }

    public int HexByteOrSentinel(int column, string sentinel, int sentinelValue)
    {
        string value = Field(column);
        return value == sentinel ? sentinelValue : HexByte(column);
    }

    public int HexWordOrSentinel(int column, string sentinel, int sentinelValue)
    {
        string value = Field(column);
        return value == sentinel ? sentinelValue : HexWord(column);
    }

    public int DecimalOrSentinel(int column, string sentinel, int sentinelValue)
    {
        string value = Field(column);
        return value == sentinel ? sentinelValue : Decimal(column);
    }

    public string Base64Utf8(int column)
    {
        string value = Field(column);
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException exception)
        {
            throw Error(column, value, "valid base64-encoded UTF-8 text", exception);
        }
    }

    public InvalidOperationException Invalid(int column, string expected) =>
        Error(column, Field(column), expected);

    private int ParseHex(int column, int minimum, int maximum, string expected)
    {
        string value = Field(column);
        if (!int.TryParse(
            value,
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture,
            out int parsed) || parsed < minimum || parsed > maximum)
        {
            throw Error(column, value, expected);
        }
        return parsed;
    }

    private int ParseDecimal(int column, int minimum, int maximum, string expected)
    {
        string value = Field(column);
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ||
            parsed < minimum || parsed > maximum)
        {
            throw Error(column, value, expected);
        }
        return parsed;
    }

    private string Field(int column)
    {
        if (column < 0 || column >= _values.Length)
            throw new ArgumentOutOfRangeException(nameof(column));
        return _values[column];
    }

    private InvalidOperationException Error(
        int column,
        string value,
        string expected,
        Exception? inner = null)
    {
        string shown = value.Length == 0 ? "<empty>" : value;
        string message =
            $"{Path}:{LineNumber}: column '{_schema.Columns[column]}' ({column + 1}) " +
            $"has value '{shown}', expected {expected}.";
        return inner is null
            ? new InvalidOperationException(message)
            : new InvalidOperationException(message, inner);
    }
}
