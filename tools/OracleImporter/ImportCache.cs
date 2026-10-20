using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OracleOfAges.Importer;

public sealed class ImportCache : IDisposable
{
    private const int Version = 1;
    private readonly string _tools;
    private readonly string _disassembly;
    private readonly string _rom;
    private readonly string _destination;
    private readonly string _symbols;
    private readonly string _executionEnvironment;
    private readonly string _statePath;
    private readonly FileStream _outputLock;
    private readonly string _inputs;

    public ImportCache(string tools, string disassembly, string rom, string destination, string symbols,
        string executionEnvironment = "")
    {
        _tools = Path.GetFullPath(tools);
        _disassembly = Path.GetFullPath(disassembly);
        _rom = Path.GetFullPath(rom);
        _destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        _symbols = symbols;
        _executionEnvironment = executionEnvironment;
        string cacheRoot = Path.GetFullPath(Path.Combine(_tools, "../.cache/oracle-import"));
        string outputPrefix = Path.EndsInDirectorySeparator(_destination)
            ? _destination : _destination + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (cacheRoot.Equals(_destination, comparison) || cacheRoot.StartsWith(outputPrefix, comparison))
            throw new InvalidDataException("Import output must not contain its .cache/oracle-import metadata.");
        Directory.CreateDirectory(cacheRoot);
        string identity = OperatingSystem.IsWindows() ? _destination.ToUpperInvariant() : _destination;
        string key = Hash(Encoding.UTF8.GetBytes(identity));
        _statePath = Path.Combine(cacheRoot, key + ".json");
        try
        {
            _outputLock = new FileStream(Path.Combine(cacheRoot, key + ".lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new IOException($"Another import owns output '{_destination}'.", exception);
        }
        try { _inputs = FingerprintInputs(); }
        catch { _outputLock.Dispose(); throw; }
    }

    public string Reason { get; private set; } = "no previous complete import";
    public int AssetCount { get; private set; }

    public bool TryReuse(bool force)
    {
        if (force) { Reason = "forced complete import"; return false; }
        if (!File.Exists(_statePath) || !Directory.Exists(_destination))
            return false;
        try
        {
            ImportCacheState? state = JsonSerializer.Deserialize<ImportCacheState>(File.ReadAllBytes(_statePath));
            if (state is null || state.Version != Version || state.Inputs != _inputs)
            {
                Reason = "inputs changed";
                return false;
            }
            GeneratedAssetManifest actual = GeneratedAssetManifest.Capture(_destination);
            if (actual.Entries.Count != state.AssetCount || FingerprintOutputs(actual) != state.Outputs)
            {
                Reason = "outputs changed";
                return false;
            }
            AssetCount = state.AssetCount;
            Reason = "all input and output hashes match";
            return true;
        }
        catch (Exception exception) when (exception is IOException or JsonException or DecoderFallbackException)
        {
            Reason = "cache or outputs need regeneration";
            return false;
        }
    }

    public void Complete()
    {
        if (FingerprintInputs() != _inputs)
            throw new InvalidDataException("Import inputs changed during generation; rerun the import.");
        GeneratedAssetManifest assets = GeneratedAssetManifest.Capture(_destination);
        if (assets.Entries.Count == 0)
            throw new InvalidDataException("Cannot cache an import with no generated assets.");
        var state = new ImportCacheState(Version, _inputs, FingerprintOutputs(assets), assets.Entries.Count);
        string temporary = _statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(state));
            File.Move(temporary, _statePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        AssetCount = assets.Entries.Count;
    }

    public void Dispose() => _outputLock.Dispose();

    private string FingerprintInputs()
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(digest, "oracle-import-cache-" + Version);
        Append(digest, _disassembly);
        Append(digest, _symbols);
        Append(digest, _executionEnvironment);
        Append(digest, Environment.Version.ToString());
        Append(digest, CultureInfo.CurrentCulture.Name);
        foreach (string path in EnumerateInputFiles().OrderBy(value => value, StringComparer.Ordinal))
        {
            Append(digest, path);
            if (Path.GetExtension(path).Equals(".s", StringComparison.OrdinalIgnoreCase))
                Append(digest, AssemblySourceRepository.HashSourceFile(path));
            else
            {
                using FileStream input = File.OpenRead(path);
                Append(digest, Convert.ToHexString(SHA256.HashData(input)));
            }
        }
        return Convert.ToHexString(digest.GetHashAndReset());
    }

    private IEnumerable<string> EnumerateInputFiles()
    {
        foreach (string path in EnumerateTree(_disassembly, codeOnly: false)) yield return path;
        foreach (string path in EnumerateTree(Path.Combine(_tools, "import_oracles"), codeOnly: false)) yield return path;
        foreach (string path in EnumerateTree(Path.Combine(_tools, "OracleImporter"), codeOnly: true)) yield return path;
        foreach (string file in new[] { "import_oracles.ps1", "Resolve-OracleRom.ps1", "rom-build/pins.json", "rom-build/build.sh" })
            yield return Path.Combine(_tools, file);
        string project = Path.GetFullPath(Path.Combine(_tools, ".."));
        foreach (string file in new[] { "Directory.Build.props", "Directory.Build.targets", "global.json", "NuGet.Config" })
        {
            string path = Path.Combine(project, file);
            if (File.Exists(path)) yield return path;
        }
        yield return Path.Combine(project, "src/Features/Story/Commands/CutsceneFieldShape.cs");
        string assembly = typeof(ImportCache).Assembly.Location;
        yield return assembly;
        foreach (string extension in new[] { ".deps.json", ".runtimeconfig.json" })
            yield return Path.ChangeExtension(assembly, extension);
        yield return _rom;
    }

    private static IEnumerable<string> EnumerateTree(string root, bool codeOnly)
    {
        foreach (string file in Directory.EnumerateFiles(root))
        {
            if (Path.GetFileName(file) == ".git") continue;
            if (!codeOnly || Path.GetExtension(file) is ".cs" or ".csproj") yield return file;
        }
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            string name = Path.GetFileName(directory);
            if (name == ".git" || (codeOnly && name is "bin" or "obj")) continue;
            foreach (string file in EnumerateTree(directory, codeOnly)) yield return file;
        }
    }

    private static void Append(IncrementalHash digest, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        digest.AppendData(length);
        digest.AppendData(bytes);
    }

    private static string FingerprintOutputs(GeneratedAssetManifest manifest) =>
        Hash(JsonSerializer.SerializeToUtf8Bytes(manifest.Entries));

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private sealed record ImportCacheState(int Version, string Inputs, string Outputs, int AssetCount);
}
