using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace oracleofages;

internal sealed class GeneratedTable
{
    private static readonly ConcurrentDictionary<string, Lazy<GeneratedTable>> Cache = new(StringComparer.Ordinal);
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
        // Include the entire contract so a cache hit cannot bypass validation.
        string key = string.Join('\u001e', path, schema.Name, schema.Version,
            schema.KeySemantics, schema.HeaderRequired, string.Join('\u001f', schema.Columns),
            string.Join(',', schema.KeyColumns));
        return Cache.GetOrAdd(key, _ => new Lazy<GeneratedTable>(
            () => LoadUncached(path, schema), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static GeneratedTable LoadUncached(string path, GeneratedTableSchema schema)
    {
        if (!path.StartsWith("res://assets/oracle/", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Generated table '{path}' is outside res://assets/oracle/.", nameof(path));
        }
        ResolvedModAsset asset = ModRuntime.ResolveTable(path);
        if (!FileAccess.FileExists(asset.LoadPath))
            throw new InvalidOperationException($"Generated table '{asset.DiagnosticPath}' does not exist.");

        byte[] bytes = OracleAssetCache.ReadPhysicalBytes(asset.LoadPath);
        if (!asset.IsOverride)
            GeneratedTableManifest.ValidateAsset(path, schema.Version, bytes);
        GeneratedTable table = Parse(
            asset.DiagnosticPath,
            GeneratedTableSource.Load(asset.LoadPath),
            schema);
        if (!asset.IsOverride)
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
        GeneratedTableSchema schema) => Parse(path, new GeneratedTableSource(source), schema);

    private static GeneratedTable Parse(
        string path,
        GeneratedTableSource source,
        GeneratedTableSchema schema)
    {
        var rows = new List<GeneratedTableRow>();
        var uniqueKeys = new Dictionary<string, int>(StringComparer.Ordinal);
        bool matchingHeaderFound = false;
        foreach (GeneratedTableSource.SourceLine line in source.Lines)
        {
            int lineNumber = line.Number;
            if (line.IsHeader)
            {
                string[] header = line.Columns;
                if (header.SequenceEqual(schema.Columns, StringComparer.Ordinal))
                    matchingHeaderFound = true;
                else if (header.Length > 0 &&
                    string.Equals(header[0], schema.Columns[0], StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"{path}:{lineNumber}: schema '{schema.Name}' header is " +
                        $"[{string.Join(", ", header)}], expected " +
                        $"[{string.Join(", ", schema.Columns)}].");
                continue;
            }

            int columnCount = line.Columns.Length;
            if (columnCount != schema.Columns.Count)
            {
                throw new InvalidOperationException(
                    $"{path}:{lineNumber}: schema '{schema.Name}' expected " +
                    $"{schema.Columns.Count} columns [{string.Join(", ", schema.Columns)}], " +
                    $"got {columnCount}.");
            }

            string[] columns = line.Columns;
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
