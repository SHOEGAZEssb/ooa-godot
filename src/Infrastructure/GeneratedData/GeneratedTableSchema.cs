using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace oracleofages;

/// <summary>
/// Declares the complete top-level shape and key contract of one generated TSV.
/// Nested domain encodings remain owned by their typed database.
/// </summary>
internal sealed class GeneratedTableSchema
{
    public string Name { get; }
    public int Version { get; }
    public IReadOnlyList<string> Columns { get; }
    public GeneratedTableKeySemantics KeySemantics { get; }
    public IReadOnlyList<int> KeyColumns { get; }
    public bool HeaderRequired { get; }

    public GeneratedTableSchema(
        string name,
        GeneratedTableKeySemantics keySemantics,
        string[] columns,
        string[]? keyColumns = null,
        int version = 1,
        bool headerRequired = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Generated table schema needs a name.", nameof(name));
        if (version <= 0)
            throw new ArgumentOutOfRangeException(nameof(version));
        if (columns.Length == 0 || columns.Any(string.IsNullOrWhiteSpace) ||
            columns.Distinct(StringComparer.Ordinal).Count() != columns.Length)
        {
            throw new ArgumentException(
                $"Generated table schema '{name}' needs distinct nonempty columns.",
                nameof(columns));
        }

        Name = name;
        Version = version;
        Columns = Array.AsReadOnly((string[])columns.Clone());
        KeySemantics = keySemantics;
        HeaderRequired = headerRequired;

        string[] keys = keyColumns ?? [];
        if (keySemantics is GeneratedTableKeySemantics.Unique or
            GeneratedTableKeySemantics.Grouped && keys.Length == 0)
        {
            throw new ArgumentException(
                $"Generated table schema '{name}' must declare its key columns.",
                nameof(keyColumns));
        }
        int[] indexes = new int[keys.Length];
        for (int index = 0; index < keys.Length; index++)
        {
            int column = Array.IndexOf(columns, keys[index]);
            if (column < 0)
            {
                throw new ArgumentException(
                    $"Generated table schema '{name}' key '{keys[index]}' is not a column.",
                    nameof(keyColumns));
            }
            indexes[index] = column;
        }
        KeyColumns = Array.AsReadOnly(indexes);
    }
}
