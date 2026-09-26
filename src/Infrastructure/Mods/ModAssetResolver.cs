using System;
using System.Collections.Generic;
using System.IO;

namespace oracleofages;

internal sealed class ModAssetResolver
{
    private const string VanillaRoot = "res://assets/oracle/";
    private readonly IReadOnlyList<ModManifest> _mods;

    internal ModAssetResolver(IReadOnlyList<ModManifest> mods) => _mods = mods;

    internal ResolvedModAsset ResolveTable(
        string vanillaPath,
        Func<string, bool>? exists = null) =>
        Resolve(vanillaPath, ".tsv", exists ?? File.Exists);

    internal ResolvedModAsset ResolveImage(
        string vanillaPath,
        Func<string, bool>? exists = null) =>
        Resolve(vanillaPath, ".png", exists ?? File.Exists);

    private ResolvedModAsset Resolve(
        string vanillaPath,
        string extension,
        Func<string, bool> exists)
    {
        if (!vanillaPath.StartsWith(VanillaRoot, StringComparison.Ordinal) ||
            !vanillaPath.EndsWith(extension, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Mod asset '{vanillaPath}' must be a {extension} below {VanillaRoot}.",
                nameof(vanillaPath));
        }

        string relative = vanillaPath[VanillaRoot.Length..];
        if (relative.Length == 0 || relative.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            throw new ArgumentException(
                $"Mod asset '{vanillaPath}' has an invalid relative path.",
                nameof(vanillaPath));
        }

        for (int index = _mods.Count - 1; index >= 0; index--)
        {
            ModManifest mod = _mods[index];
            if (!mod.Enabled)
                continue;
            string assetRoot = Path.GetFullPath(Path.Combine(mod.Directory, "assets", "oracle"));
            string candidate = Path.GetFullPath(Path.Combine(
                assetRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = assetRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, PathComparison))
                continue;
            if (exists(candidate))
                return new ResolvedModAsset(vanillaPath, candidate, mod.Id);
        }
        return new ResolvedModAsset(vanillaPath, vanillaPath, null);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}

internal readonly record struct ResolvedModAsset(
    string VanillaPath,
    string LoadPath,
    string? ModId)
{
    internal bool IsOverride => ModId is not null;
    internal string DiagnosticPath => IsOverride
        ? $"{LoadPath} (mod '{ModId}')"
        : VanillaPath;
}
