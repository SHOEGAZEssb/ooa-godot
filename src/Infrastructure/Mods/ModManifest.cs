namespace oracleofages;

internal sealed record ModManifest(
    string Id,
    string Name,
    string Version,
    int Priority,
    bool Enabled,
    string Directory,
    string ManifestPath);

internal enum ModDiagnosticSeverity
{
    Information,
    Warning
}

internal sealed record ModDiagnostic(
    ModDiagnosticSeverity Severity,
    string Path,
    string Message)
{
    public override string ToString() => $"{Path}: {Message}";
}
