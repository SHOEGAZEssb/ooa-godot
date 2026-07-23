using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace oracleofages;

/// <summary>
/// Bootstraps and verifies the deterministic generated-table manifest before
/// any production table is accepted.
/// </summary>
internal static class GeneratedTableManifest
{
    private const string Root = "res://assets/oracle/";
    private const string ManifestPath = Root + "generated_tables.manifest.tsv";
    private const int ManifestVersion = 1;
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
        Entry entry = new Entry(
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
