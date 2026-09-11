using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

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
        bool valid = CutsceneFieldShape.IsValid(shape, value);
        if (!valid)
        {
            throw new InvalidOperationException(
                $"{path}:{physicalLine}: normalized opcode field '{field}' has " +
                $"value '{Show(value)}', expected schema shape '{shape}'.");
        }
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
