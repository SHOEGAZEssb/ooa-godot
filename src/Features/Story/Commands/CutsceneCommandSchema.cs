using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
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

internal sealed class CutsceneCommandSchemaEntry
{
    private static readonly IReadOnlySet<string> ActorShapes =
        new HashSet<string>(["none", "required", "optional"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> ArgumentShapes =
        new HashSet<string>(
            ["none", "hex", "decimal", "optional-decimal", "positive-decimal"],
            StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> PayloadShapes =
        new HashSet<string>(
            [
                "none", "optional", "required", "hex", "text-variants",
                "memory-jump-table", "translation", "parallel-translation",
                "native-block"
            ],
            StringComparer.Ordinal);

    private readonly IReadOnlyList<CutsceneCommandSchema.ActorMember> _actorMembers;

    public string Opcode { get; }
    public IReadOnlyList<string> SourceAliases { get; }
    public string ByteShape { get; }
    public Type CommandType { get; }
    public string ActorShape { get; }
    public string Arg0Shape { get; }
    public string Arg1Shape { get; }
    public string PayloadShape { get; }
    public IReadOnlySet<CommandResult> Results { get; }
    public IReadOnlyList<string> Capabilities { get; }
    public string Description { get; }

    internal CutsceneCommandSchemaEntry(
        GeneratedTableRow row,
        Type commandType,
        IReadOnlySet<CommandResult> results,
        IReadOnlyList<CutsceneCommandSchema.ActorMember> actorMembers,
        IReadOnlyList<string> capabilities)
    {
        Opcode = row.RequiredString(0);
        SourceAliases = ParseAliases(row);
        ByteShape = row.RequiredString(2);
        CommandType = commandType;
        ActorShape = Shape(row, 4, ActorShapes);
        Arg0Shape = Shape(row, 5, ArgumentShapes);
        Arg1Shape = Shape(row, 6, ArgumentShapes);
        PayloadShape = Shape(row, 7, PayloadShapes);
        Results = results;
        _actorMembers = actorMembers;
        Capabilities = capabilities;
        Description = row.RequiredString(11);

        if (ActorShape == "none" && actorMembers.Count != 0 ||
            ActorShape != "none" && actorMembers.Count == 0)
        {
            throw Error(
                row,
                $"actor-shape '{ActorShape}' disagrees with actor-members " +
                $"'{row.String(9)}'");
        }
    }

    public void ValidateNormalizedFields(
        string path,
        int physicalLine,
        string actor,
        string arg0,
        string arg1,
        string payload)
    {
        ValidateField(path, physicalLine, "actor", ActorShape, actor);
        ValidateField(path, physicalLine, "arg0", Arg0Shape, arg0);
        ValidateField(path, physicalLine, "arg1", Arg1Shape, arg1);
        ValidateField(path, physicalLine, "payload", PayloadShape, payload);
    }

    public void ValidateDecoded(CutsceneCommand command)
    {
        if (command.GetType() != CommandType)
        {
            throw new InvalidOperationException(
                $"{command.Source} decoded schema opcode '{Opcode}' as " +
                $"{command.GetType().Name}; expected {CommandType.Name}.");
        }
    }

    public IEnumerable<CutsceneActorId> Actors(CutsceneCommand command)
    {
        ValidateDecoded(command);
        foreach (CutsceneCommandSchema.ActorMember member in _actorMembers)
        {
            object? value = member.Property.GetValue(command);
            if (value is CutsceneActorId actor)
            {
                yield return actor;
            }
            else if (!member.Optional)
            {
                throw new InvalidOperationException(
                    $"{command.Source} schema actor member " +
                    $"'{member.Property.Name}' is unexpectedly empty.");
            }
        }
    }

    private static IReadOnlyList<string> ParseAliases(GeneratedTableRow row)
    {
        string[] aliases = row.RequiredString(1).Split('|');
        if (aliases.Any(string.IsNullOrWhiteSpace) ||
            aliases.Distinct(StringComparer.Ordinal).Count() != aliases.Length)
        {
            throw Error(
                row, "source-aliases contains an empty or duplicate value");
        }
        return Array.AsReadOnly(aliases);
    }

    private static string Shape(
        GeneratedTableRow row,
        int column,
        IReadOnlySet<string> allowed)
    {
        string shape = row.RequiredString(column);
        if (!allowed.Contains(shape))
        {
            throw Error(
                row,
                $"column {column + 1} has unsupported shape '{shape}'");
        }
        return shape;
    }

    private static void ValidateField(
        string path,
        int physicalLine,
        string field,
        string shape,
        string value)
    {
        bool valid = shape switch
        {
            "none" => value.Length == 0,
            "optional" => true,
            "required" => !string.IsNullOrWhiteSpace(value),
            "hex" => int.TryParse(
                value,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out _),
            "decimal" => int.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out _),
            "optional-decimal" => value.Length == 0 || int.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out _),
            "positive-decimal" => int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int positive) &&
                positive > 0,
            "text-variants" => HasExactlyOneSeparator(value, '\0'),
            "memory-jump-table" => ValidMemoryJumpTable(value),
            "translation" => ValidTranslation(value),
            "parallel-translation" => ValidParallelTranslation(value),
            "native-block" =>
                !string.IsNullOrWhiteSpace(value.Split('\0', 2)[0]),
            _ => false
        };
        if (!valid)
        {
            throw new InvalidOperationException(
                $"{path}:{physicalLine}: normalized opcode field '{field}' has " +
                $"value '{Show(value)}', expected schema shape '{shape}'.");
        }
    }

    private static bool ValidMemoryJumpTable(string value)
    {
        string[] sections = value.Split('|');
        if (sections.Length != 2 || string.IsNullOrWhiteSpace(sections[0]))
            return false;
        string[] targets = sections[1].Split(',');
        return targets.Length != 0 && targets.All(target => int.TryParse(
            target,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out _));
    }

    private static bool ValidTranslation(string value)
    {
        string[] values = value.Split(',');
        return values.Length == 3 &&
            Finite(values[0]) &&
            Finite(values[1]) &&
            values[2] is "0" or "1";
    }

    private static bool ValidParallelTranslation(string value)
    {
        string[] lanes = value.Split('|');
        if (lanes.Length != 3 || string.IsNullOrWhiteSpace(lanes[1]))
            return false;
        return ValidVector(lanes[0]) && ValidVector(lanes[2]);
    }

    private static bool ValidVector(string value)
    {
        string[] components = value.Split(',');
        return components.Length == 2 &&
            Finite(components[0]) &&
            Finite(components[1]);
    }

    private static bool Finite(string value) =>
        float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float parsed) &&
        float.IsFinite(parsed);

    private static bool HasExactlyOneSeparator(string value, char separator)
    {
        int first = value.IndexOf(separator);
        return first >= 0 && value.IndexOf(separator, first + 1) < 0;
    }

    private static string Show(string value) =>
        value.Length == 0
            ? "<empty>"
            : value.Replace("\0", "\\0", StringComparison.Ordinal);

    private static InvalidOperationException Error(
        GeneratedTableRow row,
        string message) =>
        new($"{row.Path}:{row.LineNumber}: {message}.");
}
