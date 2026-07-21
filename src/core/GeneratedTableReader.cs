using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace oracleofages;

internal enum GeneratedTableKeySemantics
{
    Unique,
    Grouped,
    Ordered,
    Aliased,
    Repeated
}

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

internal sealed class GeneratedTable
{
    public string Path { get; }
    public GeneratedTableSchema Schema { get; }
    public IReadOnlyList<GeneratedTableRow> Rows { get; }

    private GeneratedTable(
        string path,
        GeneratedTableSchema schema,
        IReadOnlyList<GeneratedTableRow> rows)
    {
        Path = path;
        Schema = schema;
        Rows = rows;
    }

    public static GeneratedTable Load(string path, GeneratedTableSchema schema)
    {
        if (!path.StartsWith("res://assets/oracle/", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Generated table '{path}' is outside res://assets/oracle/.", nameof(path));
        }
        if (!FileAccess.FileExists(path))
            throw new InvalidOperationException($"Generated table '{path}' does not exist.");

        byte[] bytes = FileAccess.GetFileAsBytes(path);
        GeneratedTableManifest.ValidateAsset(path, schema.Version, bytes);
        string source;
        try
        {
            source = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException(
                $"Generated table '{path}' is not valid UTF-8.", exception);
        }
        GeneratedTable table = Parse(path, source, schema);
        GeneratedTableManifest.ValidateRecordCount(path, table.Rows.Count);
        return table;
    }

    public GeneratedTableRow SingleRow()
    {
        if (Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"{Path}: schema '{Schema.Name}' expected exactly one data row, got {Rows.Count}.");
        }
        return Rows[0];
    }

    internal static GeneratedTable ParseForValidation(
        string path,
        string source,
        GeneratedTableSchema schema) => Parse(path, source, schema);

    private static GeneratedTable Parse(
        string path,
        string source,
        GeneratedTableSchema schema)
    {
        var rows = new List<GeneratedTableRow>();
        var uniqueKeys = new Dictionary<string, int>(StringComparer.Ordinal);
        bool matchingHeaderFound = false;
        string[] lines = source.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            int lineNumber = index + 1;
            string line = lines[index].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line.StartsWith('#'))
            {
                string candidate = line[1..].TrimStart().Replace("`t", "\t");
                if (candidate.Contains('\t'))
                {
                    string[] header = candidate.Split('\t');
                    if (header.SequenceEqual(schema.Columns, StringComparer.Ordinal))
                    {
                        matchingHeaderFound = true;
                    }
                    else if (header.Length > 0 &&
                        string.Equals(header[0], schema.Columns[0], StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"{path}:{lineNumber}: schema '{schema.Name}' header is " +
                            $"[{string.Join(", ", header)}], expected " +
                            $"[{string.Join(", ", schema.Columns)}].");
                    }
                }
                continue;
            }

            string[] columns = line.Split('\t');
            if (columns.Length != schema.Columns.Count)
            {
                throw new InvalidOperationException(
                    $"{path}:{lineNumber}: schema '{schema.Name}' expected " +
                    $"{schema.Columns.Count} columns [{string.Join(", ", schema.Columns)}], " +
                    $"got {columns.Length}.");
            }

            var row = new GeneratedTableRow(path, lineNumber, schema, columns);
            if (schema.KeySemantics == GeneratedTableKeySemantics.Unique)
            {
                string key = BuildKey(columns, schema.KeyColumns);
                if (uniqueKeys.TryGetValue(key, out int previousLine))
                {
                    throw new InvalidOperationException(
                        $"{path}:{lineNumber}: duplicate unique key " +
                        $"[{DescribeKey(schema, columns)}]; first declared at line {previousLine}.");
                }
                uniqueKeys.Add(key, lineNumber);
            }
            rows.Add(row);
        }

        if (schema.HeaderRequired && !matchingHeaderFound)
        {
            throw new InvalidOperationException(
                $"{path}: schema '{schema.Name}' requires header " +
                $"# {string.Join('\t', schema.Columns)}.");
        }
        return new GeneratedTable(path, schema, rows.AsReadOnly());
    }

    private static string BuildKey(string[] columns, IReadOnlyList<int> indexes) =>
        string.Join('\u001f', indexes.Select(index => columns[index]));

    private static string DescribeKey(
        GeneratedTableSchema schema,
        string[] columns) => string.Join(", ", schema.KeyColumns.Select(index =>
            $"{schema.Columns[index]}='{columns[index]}'"));
}

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

/// <summary>
/// Bootstraps and verifies the deterministic generated-table manifest before
/// any production table is accepted.
/// </summary>
internal static class GeneratedTableManifest
{
    private const string Root = "res://assets/oracle/";
    private const string ManifestPath = Root + "generated_tables.manifest.tsv";
    private const int ManifestVersion = 1;
    private sealed record Entry(int SchemaVersion, int RecordCount, string Sha256);
    private static readonly object Sync = new();
    private static Dictionary<string, Entry>? _entries;

    internal static int EntryCount
    {
        get
        {
            EnsureValidated();
            return _entries!.Count;
        }
    }

    public static void ValidateAsset(string path, int schemaVersion, byte[] bytes)
    {
        EnsureValidated();
        string relative = Relative(path);
        if (!_entries!.TryGetValue(relative, out Entry? entry))
            throw new InvalidOperationException($"{path}: missing generated-table manifest entry.");
        ValidateVersionAndHash(path, schemaVersion, bytes, entry);
    }

    public static void ValidateRecordCount(string path, int recordCount)
    {
        EnsureValidated();
        ValidateCount(path, recordCount, _entries![Relative(path)]);
    }

    internal static void ValidateMetadataForValidation(
        string path,
        int runtimeSchemaVersion,
        byte[] bytes,
        int parsedRecordCount,
        int manifestSchemaVersion,
        int manifestRecordCount,
        string manifestSha256)
    {
        var entry = new Entry(
            manifestSchemaVersion, manifestRecordCount, manifestSha256);
        ValidateVersionAndHash(path, runtimeSchemaVersion, bytes, entry);
        ValidateCount(path, parsedRecordCount, entry);
    }

    private static void ValidateVersionAndHash(
        string path,
        int schemaVersion,
        byte[] bytes,
        Entry entry)
    {
        if (entry.SchemaVersion != schemaVersion)
        {
            throw new InvalidOperationException(
                $"{path}: manifest schema version {entry.SchemaVersion}, " +
                $"runtime expects {schemaVersion}.");
        }
        string actual = Hash(bytes);
        if (!string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{path}: SHA-256 {actual} does not match generated manifest {entry.Sha256}; " +
                "rerun tools/import_oracles.ps1.");
        }
    }

    private static void ValidateCount(string path, int recordCount, Entry entry)
    {
        if (entry.RecordCount != recordCount)
        {
            throw new InvalidOperationException(
                $"{path}: parsed {recordCount} records, generated manifest expects " +
                $"{entry.RecordCount}.");
        }
    }

    private static void EnsureValidated()
    {
        if (_entries is not null)
            return;
        lock (Sync)
        {
            if (_entries is not null)
                return;
            _entries = LoadAndValidateAll();
        }
    }

    private static Dictionary<string, Entry> LoadAndValidateAll()
    {
        if (!FileAccess.FileExists(ManifestPath))
        {
            throw new InvalidOperationException(
                $"Generated table manifest '{ManifestPath}' is missing; " +
                "rerun tools/import_oracles.ps1.");
        }
        byte[] manifestBytes = FileAccess.GetFileAsBytes(ManifestPath);
        string source;
        try
        {
            source = new UTF8Encoding(false, true).GetString(manifestBytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException(
                $"Generated table manifest '{ManifestPath}' is not valid UTF-8.", exception);
        }

        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        bool versionSeen = false;
        string[] lines = source.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            int lineNumber = index + 1;
            string line = lines[index].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line.StartsWith("# manifest-version\t", StringComparison.Ordinal))
            {
                string value = line[("# manifest-version\t").Length..];
                if (versionSeen || value != ManifestVersion.ToString(CultureInfo.InvariantCulture))
                {
                    throw new InvalidOperationException(
                        $"{ManifestPath}:{lineNumber}: expected one manifest version " +
                        $"{ManifestVersion}, got '{value}'.");
                }
                versionSeen = true;
                continue;
            }
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 4)
            {
                throw new InvalidOperationException(
                    $"{ManifestPath}:{lineNumber}: expected path, schema-version, " +
                    "record-count, and sha256.");
            }
            string relative = columns[0].Replace('\\', '/');
            if (relative.StartsWith('/') || relative.Contains("..", StringComparison.Ordinal) ||
                !relative.EndsWith(".tsv", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{ManifestPath}:{lineNumber}: invalid relative TSV path '{columns[0]}'.");
            }
            if (!int.TryParse(columns[1], NumberStyles.None, CultureInfo.InvariantCulture,
                    out int schemaVersion) || schemaVersion <= 0 ||
                !int.TryParse(columns[2], NumberStyles.None, CultureInfo.InvariantCulture,
                    out int recordCount) || recordCount < 0 ||
                columns[3].Length != 64 || !columns[3].All(IsHexDigit))
            {
                throw new InvalidOperationException(
                    $"{ManifestPath}:{lineNumber}: invalid version/count/SHA-256 for '{relative}'.");
            }
            if (!entries.TryAdd(relative, new Entry(schemaVersion, recordCount, columns[3])))
            {
                throw new InvalidOperationException(
                    $"{ManifestPath}:{lineNumber}: duplicate path '{relative}'.");
            }
        }
        if (!versionSeen)
            throw new InvalidOperationException($"{ManifestPath}: missing manifest version.");

        var diskTables = new HashSet<string>(StringComparer.Ordinal);
        CollectTables(Root, string.Empty, diskTables);
        if (!diskTables.SetEquals(entries.Keys))
        {
            string missing = string.Join(", ", diskTables.Except(entries.Keys).Order());
            string stale = string.Join(", ", entries.Keys.Except(diskTables).Order());
            throw new InvalidOperationException(
                $"{ManifestPath}: table set mismatch; unmanifested=[{missing}], missing=[{stale}].");
        }

        foreach ((string relative, Entry entry) in entries)
        {
            string path = Root + relative;
            byte[] bytes = FileAccess.GetFileAsBytes(path);
            string actualHash = Hash(bytes);
            if (!string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{path}: SHA-256 {actualHash} does not match manifest {entry.Sha256}.");
            }
            int actualRows = CountDataRows(bytes, path);
            if (actualRows != entry.RecordCount)
            {
                throw new InvalidOperationException(
                    $"{path}: contains {actualRows} records, manifest expects {entry.RecordCount}.");
            }
        }
        return entries;
    }

    private static int CountDataRows(byte[] bytes, string path)
    {
        string source;
        try
        {
            source = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException($"{path}: not valid UTF-8.", exception);
        }
        int count = 0;
        foreach (string raw in source.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
                count++;
        }
        return count;
    }

    private static void CollectTables(
        string directory,
        string relativeDirectory,
        HashSet<string> paths)
    {
        using DirAccess? access = DirAccess.Open(directory);
        if (access is null)
            throw new InvalidOperationException($"Could not enumerate generated assets at '{directory}'.");
        foreach (string file in access.GetFiles())
        {
            if (!file.EndsWith(".tsv", StringComparison.Ordinal) ||
                file == "generated_tables.manifest.tsv")
            {
                continue;
            }
            paths.Add(relativeDirectory + file);
        }
        foreach (string child in access.GetDirectories())
        {
            CollectTables(
                directory + child + "/",
                relativeDirectory + child + "/",
                paths);
        }
    }

    private static string Relative(string path)
    {
        if (!path.StartsWith(Root, StringComparison.Ordinal))
            throw new InvalidOperationException($"Generated table path '{path}' is outside '{Root}'.");
        return path[Root.Length..].Replace('\\', '/');
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool IsHexDigit(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}
