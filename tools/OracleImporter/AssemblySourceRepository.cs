using System.Collections.ObjectModel;

namespace OracleOfAges.Importer;

public sealed class AssemblySourceRepository
{
    private readonly string _root;
    private readonly string _rootPrefix;
    private readonly HashSet<string> _symbols;
    private readonly Dictionary<string, AssemblySourceFile> _files =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _readCounts =
        new(StringComparer.OrdinalIgnoreCase);

    public AssemblySourceRepository(string root, IEnumerable<string>? configuredSymbols = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        if (!Directory.Exists(_root))
            throw new DirectoryNotFoundException($"Disassembly root was not found: {_root}");
        _rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        _symbols = new HashSet<string>(
            configuredSymbols ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public string Root => _root;

    public IReadOnlySet<string> ConfiguredSymbols => _symbols;

    public IReadOnlyCollection<AssemblySourceFile> LoadedFiles =>
        new ReadOnlyCollection<AssemblySourceFile>(
            _files.Values.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray());

    public int PhysicalReadCount => _readCounts.Values.Sum();

    public AssemblySourceFile Open(string relativeOrFullPath)
    {
        string fullPath = ResolvePath(relativeOrFullPath);
        if (_files.TryGetValue(fullPath, out AssemblySourceFile? source))
            return source;

        string text = File.ReadAllText(fullPath);
        string relative = Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
        source = AssemblySourceParser.Parse(relative, fullPath, text, _symbols);
        _files.Add(fullPath, source);
        _readCounts.Add(fullPath, 1);
        return source;
    }

    public string GetText(string relativeOrFullPath) => Open(relativeOrFullPath).Text;

    public string[] GetLines(string relativeOrFullPath) =>
        Open(relativeOrFullPath).Lines.ToArray();

    public int GetPhysicalReadCount(string relativeOrFullPath)
    {
        string fullPath = ResolvePath(relativeOrFullPath);
        return _readCounts.TryGetValue(fullPath, out int count) ? count : 0;
    }

    public void AssertReadOnce()
    {
        string[] repeated = _readCounts
            .Where(pair => pair.Value != 1)
            .Select(pair => $"{Path.GetRelativePath(_root, pair.Key)}={pair.Value}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (repeated.Length != 0)
        {
            throw new AssemblySourceException(
                $"Assembly sources were not read exactly once: {string.Join(", ", repeated)}.");
        }
    }

    private string ResolvePath(string relativeOrFullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeOrFullPath);
        string fullPath = Path.GetFullPath(
            Path.IsPathRooted(relativeOrFullPath)
                ? relativeOrFullPath
                : Path.Combine(_root, relativeOrFullPath));
        if (!fullPath.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, _root, StringComparison.OrdinalIgnoreCase))
        {
            throw new AssemblySourceException(
                $"Assembly source '{relativeOrFullPath}' resolves outside disassembly root '{_root}'.");
        }
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Assembly source was not found: {fullPath}", fullPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".s", StringComparison.OrdinalIgnoreCase))
        {
            throw new AssemblySourceException(
                $"Assembly source repository accepts only .s files: {fullPath}");
        }
        return fullPath;
    }
}
