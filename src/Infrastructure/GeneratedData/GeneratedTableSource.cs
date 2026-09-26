using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace oracleofages;

// Schema-neutral tokenization can run during startup I/O. Typed table loads
// still validate the caller's header, keys, version, hash and record count.
internal sealed class GeneratedTableSource
{
    private static readonly ConcurrentDictionary<string, Lazy<GeneratedTableSource>> Cache = new(StringComparer.Ordinal);
    internal IReadOnlyList<SourceLine> Lines { get; }

    internal static GeneratedTableSource Load(string path) => Cache.GetOrAdd(path,
        static key => new Lazy<GeneratedTableSource>(() =>
        {
            try
            {
                return new GeneratedTableSource(new UTF8Encoding(false, true).GetString(
                    OracleAssetCache.ReadPhysicalBytes(key)));
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidOperationException($"Generated table '{key}' is not valid UTF-8.", exception);
            }
        }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    internal GeneratedTableSource(string source)
    {
        var lines = new List<SourceLine>();
        ReadOnlySpan<char> remaining = source.AsSpan();
        int number = 0;
        while (!remaining.IsEmpty)
        {
            number++;
            int newline = remaining.IndexOf('\n');
            ReadOnlySpan<char> line = (newline < 0 ? remaining : remaining[..newline]).TrimEnd('\r');
            remaining = newline < 0 ? default : remaining[(newline + 1)..];
            if (line.IsWhiteSpace()) continue;
            bool header = line[0] == '#';
            string content = header ? line[1..].TrimStart().ToString().Replace("`t", "\t") : line.ToString();
            if (header && !content.Contains('\t')) continue;
            lines.Add(new SourceLine(number, content.Split('\t'), header));
        }
        Lines = lines.AsReadOnly();
    }

    internal readonly record struct SourceLine(int Number, string[] Columns, bool IsHeader);
}
