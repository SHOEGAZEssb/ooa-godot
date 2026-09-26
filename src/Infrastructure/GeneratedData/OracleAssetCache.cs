using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace oracleofages;

// Immutable packaged inputs are retained for the session. Mutable room layouts
// and palette buffers always receive their own copy.
internal static class OracleAssetCache
{
    private static readonly ConcurrentDictionary<string, Lazy<byte[]>> Bytes = new(StringComparer.Ordinal);

    internal static byte[] ReadBytes(string path)
    {
        if (!path.StartsWith("res://assets/oracle/", StringComparison.Ordinal))
            throw new ArgumentException($"Not a generated asset: {path}.", nameof(path));
        return ReadPhysicalBytes(path);
    }

    internal static byte[] ReadPhysicalBytes(string path) => (byte[])ReadShared(path).Clone();

    private static byte[] ReadShared(string path)
    {
        return Bytes.GetOrAdd(path, static key => new Lazy<byte[]>(() =>
        {
            if (!FileAccess.FileExists(key))
                throw new InvalidOperationException($"Generated asset '{key}' does not exist.");
            return FileAccess.GetFileAsBytes(key);
        }, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    // Only file I/O and managed data run here; no nodes, images or GPU resources.
    internal static string[] Preload(CancellationToken cancellation)
    {
        var images = new HashSet<string>(StringComparer.Ordinal);
        Visit("res://assets/oracle/");
        var orderedImages = new List<string>(images);
        orderedImages.Sort(StringComparer.Ordinal);
        return orderedImages.ToArray();

        void Visit(string directory)
        {
            cancellation.ThrowIfCancellationRequested();
            using DirAccess dir = DirAccess.Open(directory) ??
                throw new InvalidOperationException($"Cannot read generated directory '{directory}'.");
            foreach (string file in dir.GetFiles())
            {
                cancellation.ThrowIfCancellationRequested();
                string path = directory + file;
                if (file.EndsWith(".tsv", StringComparison.Ordinal) ||
                    file.EndsWith(".bin", StringComparison.Ordinal) ||
                    file.EndsWith(".2bpp", StringComparison.Ordinal))
                {
                    _ = ReadShared(path);
                    if (file.EndsWith(".tsv", StringComparison.Ordinal))
                        _ = GeneratedTableSource.Load(path);
                }
                else if (ImageResourcePath(path) is string image)
                    images.Add(image);
            }
            foreach (string child in dir.GetDirectories())
                Visit(directory + child + "/");
        }
    }

    internal static string? ImageResourcePath(string path) =>
        path.EndsWith(".png", StringComparison.Ordinal) ? path :
        path.EndsWith(".png.import", StringComparison.Ordinal) ? path[..^7] :
        path.EndsWith(".png.remap", StringComparison.Ordinal) ? path[..^6] : null;
}
