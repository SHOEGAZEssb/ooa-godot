using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace oracleofages;

/// <summary>
/// Runtime view of the importer-owned cutscene command vocabulary. The schema
/// is the contract between normalized TSV rows, typed command records, runner
/// outcomes, actor preflight, and host capabilities.
/// </summary>
internal static class CutsceneCommandSchema
{
    private const string Path =
        "res://assets/oracle/cutscenes/script_command_vocabulary.tsv";

    private static readonly Registry Data = Load();

    public static IReadOnlyList<CutsceneCommandSchemaEntry> Entries =>
        Data.Entries;

    public static CutsceneCommandSchemaEntry ForOpcode(
        string opcode,
        CutsceneCommandSource source)
    {
        if (!Data.ByOpcode.TryGetValue(opcode, out CutsceneCommandSchemaEntry? entry))
        {
            throw new InvalidOperationException(
                $"Cutscene opcode '{opcode}' has no command schema entry at {source}.");
        }
        return entry;
    }

    public static CutsceneCommandSchemaEntry ForCommand(CutsceneCommand command)
    {
        if (!Data.ByType.TryGetValue(
                command.GetType(), out CutsceneCommandSchemaEntry? entry))
        {
            throw new InvalidOperationException(
                $"Cutscene command type {command.GetType().Name} has no command " +
                $"schema entry at {command.Source}.");
        }
        return entry;
    }

    public static CutsceneCommandSchemaEntry? FindOpcode(string opcode) =>
        Data.ByOpcode.TryGetValue(opcode, out CutsceneCommandSchemaEntry? entry)
            ? entry
            : null;

    public static IEnumerable<CutsceneActorId> Actors(CutsceneCommand command) =>
        ForCommand(command).Actors(command);

    public static void ValidateResult(
        CutsceneCommand command,
        CommandResult result)
    {
        CutsceneCommandSchemaEntry entry = ForCommand(command);
        if (!entry.Results.Contains(result))
        {
            throw new InvalidOperationException(
                $"{command.Source} executor returned undeclared result " +
                $"'{result.ToString().ToLowerInvariant()}' for schema opcode " +
                $"'{entry.Opcode}'; expected " +
                $"[{string.Join(", ", entry.Results.Select(value =>
                    value.ToString().ToLowerInvariant()))}].");
        }
    }

    private static Registry Load()
    {
        GeneratedTable table = GeneratedTable.Load(
            Path,
            new GeneratedTableSchema(
                "cutscene command vocabulary",
                GeneratedTableKeySemantics.Unique,
                [
                    "opcode", "source-aliases", "byte-shape", "command-type",
                    "actor-shape", "arg0-shape", "arg1-shape", "payload-shape",
                    "results", "actor-members", "capabilities", "description"
                ],
                keyColumns: ["opcode"],
                headerRequired: true));

        Type[] commandTypes = typeof(CutsceneCommand).Assembly.GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                typeof(CutsceneCommand).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, Type> commandTypesByName = commandTypes.ToDictionary(
            type => type.Name,
            StringComparer.Ordinal);
        var entries = new List<CutsceneCommandSchemaEntry>(table.Rows.Count);
        var byOpcode = new Dictionary<string, CutsceneCommandSchemaEntry>(
            StringComparer.Ordinal);
        var byType = new Dictionary<Type, CutsceneCommandSchemaEntry>();

        foreach (GeneratedTableRow row in table.Rows)
        {
            string commandTypeName = row.RequiredString(3);
            if (!commandTypesByName.TryGetValue(
                    commandTypeName, out Type? commandType))
            {
                throw SchemaError(
                    row,
                    $"command type '{commandTypeName}' is not a concrete " +
                    $"{nameof(CutsceneCommand)}");
            }

            var entry = new CutsceneCommandSchemaEntry(
                row,
                commandType,
                ParseResults(row),
                ParseActorMembers(row, commandType),
                ParseCapabilities(row));
            if (!byOpcode.TryAdd(entry.Opcode, entry))
            {
                throw SchemaError(
                    row, $"opcode '{entry.Opcode}' is declared more than once");
            }
            if (!byType.TryAdd(commandType, entry))
            {
                throw SchemaError(
                    row,
                    $"command type '{commandType.Name}' is mapped by more than one opcode");
            }
            entries.Add(entry);
        }

        string[] missingTypes = commandTypes
            .Where(type => !byType.ContainsKey(type))
            .Select(type => type.Name)
            .ToArray();
        if (missingTypes.Length != 0)
        {
            throw new InvalidOperationException(
                $"{Path}: concrete cutscene command types have no schema entry: " +
                $"{string.Join(", ", missingTypes)}.");
        }

        return new Registry(
            new ReadOnlyCollection<CutsceneCommandSchemaEntry>(entries),
            new ReadOnlyDictionary<string, CutsceneCommandSchemaEntry>(byOpcode),
            new ReadOnlyDictionary<Type, CutsceneCommandSchemaEntry>(byType));
    }

    private static IReadOnlySet<CommandResult> ParseResults(
        GeneratedTableRow row)
    {
        var results = new HashSet<CommandResult>();
        foreach (string value in SplitRequired(row, 8, "result"))
        {
            if (!Enum.TryParse(
                    value,
                    ignoreCase: true,
                    out CommandResult result) ||
                !results.Add(result))
            {
                throw SchemaError(
                    row, $"results contains invalid or duplicate value '{value}'");
            }
        }
        return results;
    }

    private static IReadOnlyList<ActorMember> ParseActorMembers(
        GeneratedTableRow row,
        Type commandType)
    {
        string encoded = row.RequiredString(9);
        if (encoded == "-")
            return Array.Empty<ActorMember>();

        var members = new List<ActorMember>();
        foreach (string token in SplitRequired(row, 9, "actor member"))
        {
            bool optional = token.EndsWith('?');
            string propertyName = optional ? token[..^1] : token;
            PropertyInfo? property = commandType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Type expectedType = optional
                ? typeof(CutsceneActorId?)
                : typeof(CutsceneActorId);
            if (property is null || property.PropertyType != expectedType)
            {
                throw SchemaError(
                    row,
                    $"actor member '{token}' is not a public {expectedType.Name} " +
                    $"property on {commandType.Name}");
            }
            members.Add(new ActorMember(property, optional));
        }
        return new ReadOnlyCollection<ActorMember>(members);
    }

    private static IReadOnlyList<string> ParseCapabilities(
        GeneratedTableRow row)
    {
        string encoded = row.RequiredString(10);
        if (encoded == "-")
            return Array.Empty<string>();

        string[] capabilities = SplitRequired(row, 10, "host capability");
        if (capabilities.Distinct(StringComparer.Ordinal).Count() != capabilities.Length)
        {
            throw SchemaError(row, "capabilities contains a duplicate value");
        }
        return Array.AsReadOnly(capabilities);
    }

    private static string[] SplitRequired(
        GeneratedTableRow row,
        int column,
        string description)
    {
        string[] values = row.RequiredString(column).Split('|');
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw SchemaError(
                row, $"{description} list contains an empty value");
        }
        return values;
    }

    private static InvalidOperationException SchemaError(
        GeneratedTableRow row,
        string message) =>
        new($"{row.Path}:{row.LineNumber}: {message}.");

    private sealed record Registry(
        IReadOnlyList<CutsceneCommandSchemaEntry> Entries,
        IReadOnlyDictionary<string, CutsceneCommandSchemaEntry> ByOpcode,
        IReadOnlyDictionary<Type, CutsceneCommandSchemaEntry> ByType);

    internal sealed record ActorMember(PropertyInfo Property, bool Optional);
}
