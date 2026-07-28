using System.Text.Json;
using System.Text.Json.Serialization;

namespace OracleOfAges.Importer;

public static class AssemblySourceQuery
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<AssemblyNodeQueryResult> Select(
        AssemblySourceFile file,
        string command,
        string? label = null,
        string? name = null)
    {
        IEnumerable<AssemblyNode> nodes = command switch
        {
            "NODES" => file.Nodes,
            "LABEL_NODES" => file.GetLabelBlockNodes(
                Required(label, command, "label")),
            "LABELS" => file.Nodes.Where(node =>
                node.Kind == AssemblyNodeKind.Label),
            "DATA_DIRECTIVES" => Scope(file, label).Where(node =>
                node.Kind == AssemblyNodeKind.Data),
            "MACRO_INVOCATIONS" => Scope(file, label).Where(node =>
                node.Kind == AssemblyNodeKind.MacroInvocation),
            "INSTRUCTIONS" => Scope(file, label).Where(node =>
                node.Kind == AssemblyNodeKind.Instruction),
            "CONSTANTS" => Scope(file, label).Where(node =>
                node.Kind == AssemblyNodeKind.Constant),
            _ => throw new InvalidDataException(
                $"Unknown assembly node query '{command}'."),
        };
        if (!string.IsNullOrEmpty(name))
        {
            nodes = nodes.Where(node =>
                string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        return nodes.Select(ToResult).ToArray();
    }

    public static string Serialize(
        AssemblySourceFile file,
        string command,
        string? label = null,
        string? name = null) =>
        JsonSerializer.Serialize(Select(file, command, label, name), JsonOptions);

    private static IEnumerable<AssemblyNode> Scope(
        AssemblySourceFile file,
        string? label) =>
        string.IsNullOrEmpty(label)
            ? file.Nodes
            : file.GetLabelBlockNodes(label);

    private static string Required(
        string? value,
        string command,
        string field) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException(
                $"{command} requires a {field}.");

    private static AssemblyNodeQueryResult ToResult(AssemblyNode node)
    {
        string code = node.Code.Trim();
        string operandText = code.StartsWith(node.Name, StringComparison.OrdinalIgnoreCase)
            ? code[node.Name.Length..].Trim()
            : string.Join(", ", node.Operands);
        return new AssemblyNodeQueryResult(
            node.Kind,
            node.Name,
            node.Operands,
            operandText,
            node.RawText,
            node.Code,
            node.Comment,
            node.IsActive,
            node.EnclosingGlobalLabel,
            node.Span.Start.Path,
            node.Span.Start.Line,
            node.Span.Start.Column,
            node.Span.Start.Offset,
            node.Span.Length);
    }
}

public sealed record AssemblyNodeQueryResult(
    AssemblyNodeKind Kind,
    string Name,
    IReadOnlyList<string> Operands,
    string OperandText,
    string RawText,
    string Code,
    string Comment,
    bool IsActive,
    string? EnclosingGlobalLabel,
    string Path,
    int Line,
    int Column,
    int Offset,
    int Length);
