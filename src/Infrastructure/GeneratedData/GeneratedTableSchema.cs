using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Oracles;

/// <summary>
/// Declares the complete top-level shape and key contract of one generated TSV.
/// Nested domain encodings remain owned by their typed database.
/// </summary>
internal sealed class GeneratedTableSchema : IEquatable<GeneratedTableSchema>
{
    private readonly int _hashCode;
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
        var hash = new HashCode();
        hash.Add(Name, StringComparer.Ordinal);
        hash.Add(Version);
        hash.Add(KeySemantics);
        hash.Add(HeaderRequired);
        foreach (string column in Columns) hash.Add(column, StringComparer.Ordinal);
        foreach (int column in KeyColumns) hash.Add(column);
        _hashCode = hash.ToHashCode();
    }

    public bool Equals(GeneratedTableSchema? other) => ReferenceEquals(this, other) ||
        other is not null && Name == other.Name && Version == other.Version &&
        KeySemantics == other.KeySemantics && HeaderRequired == other.HeaderRequired &&
        Columns.SequenceEqual(other.Columns, StringComparer.Ordinal) && KeyColumns.SequenceEqual(other.KeyColumns);

    public override bool Equals(object? obj) => obj is GeneratedTableSchema other && Equals(other);
    public override int GetHashCode() => _hashCode;
}
