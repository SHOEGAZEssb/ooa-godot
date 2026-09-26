using System;
using System.Collections.Concurrent;

namespace oracleofages;

internal static class ModRuntime
{
    private static readonly object Sync = new();
    private static ModAssetResolver _resolver = new(ModCatalog.Empty.Mods);
    private static readonly ConcurrentDictionary<string, byte> ReportedOverrides =
        new(StringComparer.Ordinal);
    private static bool _initialized;

    internal static void Initialize(LaunchOptions options)
    {
        lock (Sync)
        {
            if (_initialized)
                return;

            ModCatalog catalog = options.ModsEnabled
                ? ModCatalog.Discover(options.ModsDirectory)
                : ModCatalog.Empty;
            _resolver = new ModAssetResolver(catalog.Mods);
            _initialized = true;

            if (!options.ModsEnabled)
            {
                Console.Error.WriteLine("[mods] disabled for this run");
                return;
            }
            Console.Error.WriteLine($"[mods] directory: {options.ModsDirectory}");
            foreach (ModDiagnostic diagnostic in catalog.Diagnostics)
                Console.Error.WriteLine($"[mods] warning: {diagnostic}");
            foreach (ModManifest mod in catalog.Mods)
            {
                Console.Error.WriteLine(
                    $"[mods] {(mod.Enabled ? "enabled" : "disabled")}: " +
                    $"{mod.Id} {mod.Version} (priority {mod.Priority})");
            }
        }
    }

    internal static ResolvedModAsset ResolveTable(string path) => Report(
        _resolver.ResolveTable(path));

    internal static ResolvedModAsset ResolveImage(string path) => Report(
        _resolver.ResolveImage(path));

    private static ResolvedModAsset Report(ResolvedModAsset asset)
    {
        if (asset.IsOverride && ReportedOverrides.TryAdd(asset.VanillaPath, 0))
        {
            Console.Error.WriteLine(
                $"[mods] override: {asset.VanillaPath} <- {asset.DiagnosticPath}");
        }
        return asset;
    }
}
