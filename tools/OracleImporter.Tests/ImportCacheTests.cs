using OracleOfAges.Importer;

namespace OracleOfAges.Importer.Tests;

internal static class ImportCacheTests
{
    public static void Run(string root)
    {
        string project = Path.Combine(root, "cache ' [0]");
        string tools = Path.Combine(project, "tools");
        string sources = Path.Combine(project, "disassembly");
        string output = Path.Combine(project, "output");
        foreach (string directory in new[] { "import_oracles", "OracleImporter", "rom-build" })
            Directory.CreateDirectory(Path.Combine(tools, directory));
        Directory.CreateDirectory(sources);
        Directory.CreateDirectory(output);
        foreach (string file in new[] { "import_oracles.ps1", "Resolve-OracleRom.ps1", "rom-build/pins.json", "rom-build/build.sh" })
            File.WriteAllText(Path.Combine(tools, file), "fixture");
        string linked = Path.Combine(project, "src/Features/Story/Commands/CutsceneFieldShape.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(linked)!);
        File.WriteAllText(linked, "schema");
        string source = Path.Combine(sources, "fixture.s");
        File.WriteAllText(source, "root:\n.db $01\n");
        string rom = Path.Combine(project, "fixture.gbc");
        File.WriteAllBytes(rom, new byte[] { 1, 2, 3 });
        string asset = Path.Combine(output, "fixture.bin");
        File.WriteAllBytes(asset, new byte[] { 4, 5, 6 });
        ImportCache Open(string symbols = "REGION_US") => new(tools, sources, rom, output, symbols);
        void Publish() { using ImportCache cache = Open(); cache.Complete(); }
        bool Reuse() { using ImportCache cache = Open(); return cache.TryReuse(false); }

        Assert(!Reuse(), "fresh output was incorrectly reused");
        Publish();
        Assert(Reuse(), "unchanged input/output could not be reused");
        using (ImportCache cache = Open()) Assert(!cache.TryReuse(true), "forced import reused cached outputs");
        File.WriteAllText(Path.Combine(output, "fixture.png.import"), "Godot metadata");
        Assert(Reuse(), "Godot metadata invalidated generated assets");
        DateTime timestamp = File.GetLastWriteTimeUtc(source);
        File.WriteAllText(source, "root:\n.db $02\n");
        File.SetLastWriteTimeUtc(source, timestamp);
        Assert(!Reuse(), "same-size, same-timestamp source edit was missed");
        Publish();
        timestamp = File.GetLastWriteTimeUtc(asset);
        File.WriteAllBytes(asset, new byte[] { 7, 5, 6 });
        File.SetLastWriteTimeUtc(asset, timestamp);
        Assert(!Reuse(), "same-size, same-timestamp output corruption was missed");
        Publish();
        File.WriteAllText(Path.Combine(sources, "sprite.properties"), "invert: true");
        Assert(!Reuse(), "new non-assembly input was missed");
        Publish();
        File.Delete(asset);
        Assert(!Reuse(), "missing generated output was reused");
        File.WriteAllBytes(asset, new byte[] { 4, 5, 6 });
        Publish();
        File.WriteAllBytes(Path.Combine(output, "extra.bin"), new byte[] { 0 });
        Assert(!Reuse(), "unexpected output was missed");
        File.Delete(Path.Combine(output, "extra.bin"));
        using (ImportCache cache = Open("REGION_JP")) Assert(!cache.TryReuse(false), "selected symbols were omitted from inputs");
        using (var cache = new ImportCache(tools, sources, rom, output, "REGION_US", "different PowerShell version/culture"))
            Assert(!cache.TryReuse(false), "PowerShell execution environment was omitted from inputs");
        File.WriteAllText(linked, "changed schema");
        Assert(!Reuse(), "linked schema source edit was missed");
        Publish();
        using (ImportCache cache = Open())
        {
            File.WriteAllText(source, "changed during import");
            bool rejected = false;
            try { cache.Complete(); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "inputs changed during generation but cache was published");
        }
        Assert(!Reuse(), "failed import published a valid cache");
        Publish();
        using (ImportCache cache = Open())
        {
            bool locked = false;
            try { using ImportCache conflicting = Open(); }
            catch (IOException) { locked = true; }
            Assert(locked, "two importers could own the same output");
            locked = false;
            try { using var conflicting = new ImportCache(tools, sources, rom, output + Path.DirectorySeparatorChar, "REGION_US"); }
            catch (IOException) { locked = true; }
            Assert(locked, "trailing separator bypassed the output lock");
        }
        string state = Directory.GetFiles(Path.Combine(project, ".cache/oracle-import"), "*.json").Single();
        File.WriteAllText(state, "broken json");
        Assert(!Reuse(), "corrupt cache did not request regeneration");
        bool rootRejected = false;
        try { using var cache = new ImportCache(tools, sources, rom, Path.GetPathRoot(output)!, "REGION_US"); }
        catch (InvalidDataException) { rootRejected = true; }
        Assert(rootRejected, "filesystem-root output could contain the cache metadata itself");
        Console.WriteLine("Import-cache fixtures passed (content hashes, outputs, metadata, force, schema, failure and locking).");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
