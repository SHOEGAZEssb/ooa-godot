using System.Collections.ObjectModel;

namespace OracleOfAges.Importer;

public enum AssemblyNodeKind
{
    Blank,
    Comment,
    Label,
    Directive,
    Data,
    MacroInvocation,
    Instruction,
    Constant,
    Unrecognized,
}

public sealed record AssemblyNode(
    AssemblyNodeKind Kind,
    SourceSpan Span,
    string RawText,
    string Code,
    string Comment,
    string Name,
    IReadOnlyList<string> Operands,
    bool IsActive,
    string? EnclosingGlobalLabel);

public sealed record AssemblyLabel(
    string Name,
    SourceSpan Span,
    int NodeIndex,
    bool IsLocal,
    bool IsActive);

public sealed record AssemblyConstant(
    string Name,
    string Expression,
    SourceSpan Span,
    int NodeIndex,
    bool IsActive);

internal static class ReadOnly
{
    public static IReadOnlyList<T> List<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());

    public static IReadOnlyDictionary<TKey, IReadOnlyList<TValue>> MultiMap<TKey, TValue>(
        Dictionary<TKey, List<TValue>> source)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, IReadOnlyList<TValue>>(
            source.Count,
            source.Comparer);
        foreach ((TKey key, List<TValue> values) in source)
            result.Add(key, new ReadOnlyCollection<TValue>(values.ToArray()));
        return new ReadOnlyDictionary<TKey, IReadOnlyList<TValue>>(result);
    }
}
