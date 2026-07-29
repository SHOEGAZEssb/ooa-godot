namespace OracleOfAges.Importer;

public readonly record struct SourcePosition(
    string Path,
    int Offset,
    int Line,
    int Column)
{
    public override string ToString() => $"{Path}:{Line}:{Column}";
}

public readonly record struct SourceSpan(SourcePosition Start, int Length)
{
    public override string ToString() => $"{Start} (+{Length})";
}
