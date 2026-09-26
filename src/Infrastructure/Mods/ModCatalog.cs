using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace oracleofages;

internal sealed class ModCatalog
{
    private static readonly Regex IdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal IReadOnlyList<ModManifest> Mods { get; }
    internal IReadOnlyList<ModDiagnostic> Diagnostics { get; }

    private ModCatalog(
        IReadOnlyList<ModManifest> mods,
        IReadOnlyList<ModDiagnostic> diagnostics)
    {
        Mods = mods;
        Diagnostics = diagnostics;
    }

    internal static ModCatalog Empty { get; } = new([], []);

    internal static ModCatalog Discover(string root)
    {
        string resolvedRoot = Path.GetFullPath(root);
        if (!Directory.Exists(resolvedRoot))
            return Empty;

        var mods = new List<ModManifest>();
        var diagnostics = new List<ModDiagnostic>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(resolvedRoot)
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new ModDiagnostic(
                ModDiagnosticSeverity.Warning,
                resolvedRoot,
                $"could not enumerate the mods directory: {exception.Message}"));
            return new ModCatalog([], diagnostics.AsReadOnly());
        }

        foreach (string directory in directories)
        {
            string manifestPath = Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifestPath))
                continue;
            try
            {
                ModManifest manifest = ReadManifest(directory, manifestPath);
                if (!ids.Add(manifest.Id))
                {
                    throw new InvalidDataException(
                        $"duplicate mod id '{manifest.Id}'; each manifest id must be unique");
                }
                mods.Add(manifest);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or
                JsonException or InvalidDataException)
            {
                diagnostics.Add(new ModDiagnostic(
                    ModDiagnosticSeverity.Warning,
                    manifestPath,
                    exception.Message));
            }
        }

        mods.Sort(static (left, right) =>
        {
            int priority = left.Priority.CompareTo(right.Priority);
            return priority != 0
                ? priority
                : StringComparer.Ordinal.Compare(left.Id, right.Id);
        });
        return new ModCatalog(mods.AsReadOnly(), diagnostics.AsReadOnly());
    }

    private static ModManifest ReadManifest(string directory, string manifestPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifestPath),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("manifest root must be a JSON object");

        JsonElement root = document.RootElement;
        string id = RequiredString(root, "id");
        if (!IdPattern.IsMatch(id))
        {
            throw new InvalidDataException(
                "'id' must start with an ASCII letter or digit and contain only letters, digits, '.', '_' or '-'");
        }
        string version = RequiredString(root, "version");
        string name = OptionalString(root, "name") ?? id;
        int priority = OptionalInt(root, "priority") ?? 100;
        bool enabled = OptionalBool(root, "enabled") ?? true;

        return new ModManifest(
            id,
            name,
            version,
            priority,
            enabled,
            Path.GetFullPath(directory),
            Path.GetFullPath(manifestPath));
    }

    private static string RequiredString(JsonElement root, string name)
    {
        string? value = OptionalString(root, name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"'{name}' is required and must be a non-empty string");
        return value;
    }

    private static string? OptionalString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement property))
            return null;
        if (property.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"'{name}' must be a string");
        return property.GetString();
    }

    private static int? OptionalInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement property))
            return null;
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out int value))
            throw new InvalidDataException($"'{name}' must be a 32-bit integer");
        return value;
    }

    private static bool? OptionalBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement property))
            return null;
        if (property.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            throw new InvalidDataException($"'{name}' must be a boolean");
        return property.GetBoolean();
    }
}
