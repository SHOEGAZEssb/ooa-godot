using System.Collections.ObjectModel;

namespace OracleOfAges.Importer;

public sealed class AssemblySourceFile
{
    private readonly int[] _lineStarts;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AssemblyLabel>> _labels;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AssemblyConstant>> _constants;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AssemblyNode>> _directives;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AssemblyNode>> _macros;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AssemblyNode>> _instructions;

    internal AssemblySourceFile(
        string relativePath,
        string fullPath,
        string text,
        string[] lines,
        int[] lineStarts,
        IReadOnlyList<AssemblyNode> nodes,
        Dictionary<string, List<AssemblyLabel>> labels,
        Dictionary<string, List<AssemblyConstant>> constants,
        Dictionary<string, List<AssemblyNode>> directives,
        Dictionary<string, List<AssemblyNode>> macros,
        Dictionary<string, List<AssemblyNode>> instructions)
    {
        RelativePath = relativePath;
        FullPath = fullPath;
        Text = text;
        Lines = new ReadOnlyCollection<string>(lines);
        _lineStarts = lineStarts;
        Nodes = nodes;
        _labels = ReadOnly.MultiMap(labels);
        _constants = ReadOnly.MultiMap(constants);
        _directives = ReadOnly.MultiMap(directives);
        _macros = ReadOnly.MultiMap(macros);
        _instructions = ReadOnly.MultiMap(instructions);
    }

    public string RelativePath { get; }

    public string FullPath { get; }

    public string Text { get; }

    public IReadOnlyList<string> Lines { get; }

    public IReadOnlyList<AssemblyNode> Nodes { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<AssemblyLabel>> Labels => _labels;

    public IReadOnlyDictionary<string, IReadOnlyList<AssemblyConstant>> Constants => _constants;

    public IReadOnlyDictionary<string, IReadOnlyList<AssemblyNode>> Directives => _directives;

    public IReadOnlyDictionary<string, IReadOnlyList<AssemblyNode>> MacroInvocations => _macros;

    public IReadOnlyDictionary<string, IReadOnlyList<AssemblyNode>> Instructions => _instructions;

    public SourcePosition PositionAt(int offset)
    {
        if (offset < 0 || offset > Text.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        int lineIndex = Array.BinarySearch(_lineStarts, offset);
        if (lineIndex < 0)
            lineIndex = ~lineIndex - 1;
        lineIndex = Math.Max(0, lineIndex);
        return new SourcePosition(
            RelativePath,
            offset,
            lineIndex + 1,
            offset - _lineStarts[lineIndex] + 1);
    }

    public IReadOnlyList<AssemblyLabel> GetLabels(string name) =>
        _labels.TryGetValue(name, out IReadOnlyList<AssemblyLabel>? labels)
            ? labels
            : Array.Empty<AssemblyLabel>();

    public AssemblyLabel RequireUniqueLabel(string name)
    {
        IReadOnlyList<AssemblyLabel> labels = GetLabels(name);
        if (labels.Count == 0)
            throw new AssemblySourceException(
                $"{RelativePath}: label '{name}' was not found.");
        if (labels.Count != 1)
        {
            string locations = string.Join(", ", labels.Select(label => label.Span.Start));
            throw new AssemblySourceException(
                $"{RelativePath}: label '{name}' is ambiguous at {locations}.");
        }
        return labels[0];
    }

    public string GetLabelBlockText(string name, bool includeLabel = false)
    {
        AssemblyLabel label = RequireUniqueLabel(name);
        int endNode = FindLabelBlockEnd(label);
        int startOffset = includeLabel
            ? label.Span.Start.Offset
            : LineEndOffset(label.Span.Start.Line - 1);
        int endOffset = endNode < Nodes.Count
            ? Nodes[endNode].Span.Start.Offset
            : Text.Length;
        if (endOffset < startOffset)
            endOffset = startOffset;
        return Text[startOffset..endOffset];
    }

    public IReadOnlyList<AssemblyNode> GetLabelBlockNodes(string name)
    {
        AssemblyLabel label = RequireUniqueLabel(name);
        int endNode = FindLabelBlockEnd(label);
        return Nodes
            .Skip(label.NodeIndex + 1)
            .Take(endNode - label.NodeIndex - 1)
            .ToArray();
    }

    public IReadOnlyList<AssemblyNode> GetDataDirectives(string label)
    {
        return GetLabelBlockNodes(label)
            .Where(node => node.Kind == AssemblyNodeKind.Data && node.IsActive)
            .ToArray();
    }

    public IReadOnlyList<AssemblyConstant> GetConstants(string name) =>
        _constants.TryGetValue(name, out IReadOnlyList<AssemblyConstant>? constants)
            ? constants
            : Array.Empty<AssemblyConstant>();

    private int FindLabelBlockEnd(AssemblyLabel label)
    {
        for (int index = label.NodeIndex + 1; index < Nodes.Count; index++)
        {
            AssemblyNode node = Nodes[index];
            if (node.Kind != AssemblyNodeKind.Label)
                continue;

            bool nextIsLocal = IsLocalLabel(node.Name);
            if (label.IsLocal || !nextIsLocal)
                return index;
        }
        return Nodes.Count;
    }

    private int LineEndOffset(int zeroBasedLine)
    {
        if (zeroBasedLine + 1 < _lineStarts.Length)
            return _lineStarts[zeroBasedLine + 1];
        return Text.Length;
    }

    internal static bool IsLocalLabel(string name) =>
        name.StartsWith('@') ||
        name.StartsWith('.') ||
        name.All(character => character is '+' or '-');
}

public sealed class AssemblySourceException : Exception
{
    public AssemblySourceException(string message)
        : base(message)
    {
    }
}
