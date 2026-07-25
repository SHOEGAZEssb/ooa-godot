using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OracleOfAges.Importer;

public sealed record GeneratedAssetEntry(
    string Path,
    long ByteCount,
    string Sha256,
    int? RecordCount,
    string KeySequenceSha256);

public sealed class GeneratedAssetManifest
{
    private GeneratedAssetManifest(IReadOnlyList<GeneratedAssetEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<GeneratedAssetEntry> Entries { get; }

    public static GeneratedAssetManifest Capture(string root)
    {
        string fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException($"Generated asset root was not found: {fullRoot}");

        var entries = new List<GeneratedAssetEntry>();
        foreach (string file in Directory
            .EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(fullRoot, path), StringComparer.Ordinal))
        {
            if (string.Equals(Path.GetExtension(file), ".import", StringComparison.OrdinalIgnoreCase))
                continue;

            byte[] bytes = File.ReadAllBytes(file);
            string relative = Path.GetRelativePath(fullRoot, file).Replace('\\', '/');
            int? recordCount = null;
            string keyHash = string.Empty;
            if (string.Equals(Path.GetExtension(file), ".tsv", StringComparison.OrdinalIgnoreCase))
            {
                string text = new UTF8Encoding(false, true).GetString(bytes);
                string[] dataLines = text
                    .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
                    .ToArray();
                recordCount = dataLines.Length;
                string keys = string.Join(
                    '\n',
                    dataLines.Select(line =>
                    {
                        int separator = line.IndexOf('\t');
                        return separator < 0 ? line : line[..separator];
                    }));
                keyHash = Hash(Encoding.UTF8.GetBytes(keys));
            }

            entries.Add(new GeneratedAssetEntry(
                relative,
                bytes.LongLength,
                Hash(bytes),
                recordCount,
                keyHash));
        }
        return new GeneratedAssetManifest(new ReadOnlyCollection<GeneratedAssetEntry>(entries));
    }

    public void WriteTsv(string path)
    {
        var lines = new List<string>(Entries.Count + 2)
        {
            "# generated-asset-manifest-version\t1",
            "# path\tbyte-count\tsha256\trecord-count\tkey-sequence-sha256",
        };
        lines.AddRange(Entries.Select(entry => string.Join(
            '\t',
            entry.Path,
            entry.ByteCount.ToString(CultureInfo.InvariantCulture),
            entry.Sha256,
            entry.RecordCount?.ToString(CultureInfo.InvariantCulture) ?? "-",
            string.IsNullOrEmpty(entry.KeySequenceSha256) ? "-" : entry.KeySequenceSha256)));
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    public void AssertEquivalent(GeneratedAssetManifest actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        var expectedByPath = Entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        var actualByPath = actual.Entries.ToDictionary(entry => entry.Path, StringComparer.Ordinal);
        string[] missing = expectedByPath.Keys.Except(actualByPath.Keys, StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        string[] extra = actualByPath.Keys.Except(expectedByPath.Keys, StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var changed = new List<string>();
        foreach (string path in expectedByPath.Keys.Intersect(actualByPath.Keys, StringComparer.Ordinal))
        {
            if (expectedByPath[path] != actualByPath[path])
                changed.Add(path);
        }
        changed.Sort(StringComparer.Ordinal);
        if (missing.Length != 0 || extra.Length != 0 || changed.Count != 0)
        {
            throw new InvalidDataException(
                "Generated asset manifests differ: " +
                $"missing=[{string.Join(", ", missing)}], " +
                $"extra=[{string.Join(", ", extra)}], " +
                $"changed=[{string.Join(", ", changed)}].");
        }
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
