using oracleofages;
using System.Text;

string temporaryRoot = Path.Combine(
    Path.GetTempPath(),
    $"ooa-mod-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
try
{
    CreateMod("01-low", "low", 10, true, "low-table", "low-image");
    CreateMod("02-high", "high", 20, true, "high-table", "high-image");
    CreateMod("03-zeta", "zeta", 20, true, "zeta-table", null);
    CreateMod("04-disabled", "disabled", 100, false, "disabled-table", "disabled-image");
    CreateManifest("05-invalid", "{\"id\":\"bad id\",\"version\":\"1.0.0\"}");
    CreateManifest("06-duplicate", "{\"id\":\"low\",\"version\":\"2.0.0\"}");
    CreateManifest("07-malformed", "{not json}");

    ModCatalog catalog = ModCatalog.Discover(temporaryRoot);
    Assert(catalog.Mods.Select(mod => mod.Id).SequenceEqual(["low", "high", "zeta", "disabled"]),
        "catalog ordering was not priority then ordinal id");
    Assert(catalog.Diagnostics.Count == 3,
        "invalid, duplicate, and malformed manifests should each produce a diagnostic");
    Assert(catalog.Diagnostics.All(diagnostic =>
        diagnostic.Path.EndsWith("manifest.json", StringComparison.Ordinal)),
        "manifest diagnostics did not name the failing file");

    var resolver = new ModAssetResolver(catalog.Mods);
    ResolvedModAsset table = resolver.ResolveTable(
        "res://assets/oracle/metadata/example.tsv");
    Assert(table.IsOverride && table.ModId == "zeta" &&
        File.ReadAllText(table.LoadPath, Encoding.UTF8) == "zeta-table",
        "equal-priority table overrides did not resolve by ordinal id");

    ResolvedModAsset image = resolver.ResolveImage(
        "res://assets/oracle/gfx/example.png");
    Assert(image.IsOverride && image.ModId == "high" &&
        File.ReadAllText(image.LoadPath, Encoding.UTF8) == "high-image",
        "highest enabled image override did not win");

    ResolvedModAsset fallback = resolver.ResolveTable(
        "res://assets/oracle/metadata/missing.tsv");
    Assert(!fallback.IsOverride && fallback.LoadPath == fallback.VanillaPath,
        "missing overrides did not fall back to the vanilla resource");

    ExpectFailure(
        () => resolver.ResolveTable("res://assets/oracle/../outside.tsv"),
        "invalid relative path");
    ExpectFailure(
        () => resolver.ResolveTable("res://assets/oracle/gfx/example.png"),
        "must be a .tsv");

    Assert(ModCatalog.Discover(Path.Combine(temporaryRoot, "missing")).Mods.Count == 0,
        "a missing mods directory should produce an empty catalog");
    Console.WriteLine("Mod support tests passed.");
}
finally
{
    string resolved = Path.GetFullPath(temporaryRoot);
    string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!resolved.StartsWith(
            temp,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Refusing to remove non-temporary test path: {resolved}");
    }
    Directory.Delete(resolved, recursive: true);
}

void CreateMod(
    string folder,
    string id,
    int priority,
    bool enabled,
    string? table,
    string? image)
{
    CreateManifest(
        folder,
        $$"""
        {"id":"{{id}}","name":"{{id}}","version":"1.0.0","priority":{{priority}},"enabled":{{enabled.ToString().ToLowerInvariant()}}}
        """);
    string directory = Path.Combine(temporaryRoot, folder, "assets", "oracle");
    if (table is not null)
    {
        string path = Path.Combine(directory, "metadata", "example.tsv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, table, new UTF8Encoding(false));
    }
    if (image is not null)
    {
        string path = Path.Combine(directory, "gfx", "example.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, image, new UTF8Encoding(false));
    }
}

void CreateManifest(string folder, string json)
{
    string directory = Path.Combine(temporaryRoot, folder);
    Directory.CreateDirectory(directory);
    File.WriteAllText(
        Path.Combine(directory, "manifest.json"),
        json,
        new UTF8Encoding(false));
}

static void ExpectFailure(Action action, string fragment)
{
    try
    {
        action();
    }
    catch (ArgumentException exception) when (
        exception.Message.Contains(fragment, StringComparison.Ordinal))
    {
        return;
    }
    throw new InvalidOperationException($"Expected failure containing '{fragment}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
