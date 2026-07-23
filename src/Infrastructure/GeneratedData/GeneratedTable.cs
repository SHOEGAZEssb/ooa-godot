using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace oracleofages;

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

internal enum GeneratedTableKeySemantics
{
    Unique,
    Grouped,
    Ordered,
    Aliased,
    Repeated
}
