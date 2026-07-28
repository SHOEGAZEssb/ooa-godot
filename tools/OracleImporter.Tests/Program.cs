using OracleOfAges.Importer;
using System.Text;

string temporaryRoot = Path.Combine(
    Path.GetTempPath(),
    $"ooa-importer-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
try
{
    RunSourceModelTests(temporaryRoot);
    RunManifestTests(temporaryRoot);
    RunImporterBoundaryTests();
    Console.WriteLine("OracleImporter tests passed.");
}
finally
{
    string resolved = Path.GetFullPath(temporaryRoot);
    string temp = Path.GetFullPath(Path.GetTempPath());
    if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Refusing to remove non-temporary test path: {resolved}");
    Directory.Delete(resolved, recursive: true);
}

static void RunSourceModelTests(string root)
{
    string sourcePath = Path.Combine(root, "fixture.s");
    string source =
        ".define ROM_AGES 1\r\n" +
        "rootLabel: ; root\r\n" +
        "  .db $01, \"semi;colon\" ; values\r\n" +
        "  /* $02 */ objectData $03 $04 ($05 + 1)\r\n" +
        "@local:\r\n" +
        "  ld a,$02\r\n" +
        "  objectData $03, ($04 + 1)\r\n" +
        "++\r\n" +
        ".ifdef REGION_JP\r\n" +
        "jpOnly: .db $ff\r\n" +
        ".else\r\n" +
        "usOnly: .dw $1234\r\n" +
        ".endif\r\n" +
        "@duplicate:\r\n" +
        "@duplicate:\r\n" +
        "ALIAS equ rootLabel\r\n";
    File.WriteAllText(sourcePath, source, new UTF8Encoding(false));

    var repository = new AssemblySourceRepository(
        root,
        new[] { "ROM_AGES", "REGION_US", "AGES_ENGINE", "BUILD_VANILLA" });
    AssemblySourceFile file = repository.Open("fixture.s");
    Assert(ReferenceEquals(file, repository.Open(sourcePath)), "repository did not cache source");
    Assert(repository.PhysicalReadCount == 1, "source was physically read more than once");
    Assert(file.Lines.Count == 17, "CRLF line split changed");
    Assert(file.RequireUniqueLabel("rootLabel").Span.Start.Line == 2, "label span changed");
    Assert(file.GetLabels("@duplicate").Count == 2, "duplicate labels were discarded");
    Assert(file.GetDataDirectives("rootLabel").Count == 1, "global label block lost data");
    Assert(file.GetLabelBlockNodes("rootLabel").Any(
        node => node.Kind == AssemblyNodeKind.Instruction && node.Name == "ld"),
        "instruction was not indexed");
    Assert(file.GetLabelBlockNodes("rootLabel").Any(
        node => node.Kind == AssemblyNodeKind.MacroInvocation && node.Name == "objectData"),
        "macro invocation was not indexed");
    AssemblyNode db = file.GetDataDirectives("rootLabel")[0];
    Assert(db.Operands.Count == 2 && db.Operands[1] == "\"semi;colon\"",
        "quoted semicolon or operands were parsed incorrectly");
    AssemblyNode prefixedMacro = file.Nodes.Single(node =>
        node.Kind == AssemblyNodeKind.MacroInvocation &&
        node.Name == "objectData" &&
        node.Comment == "$02");
    Assert(
        prefixedMacro.Operands.SequenceEqual(
            new[] { "$03", "$04", "($05 + 1)" }),
        "comment-prefixed whitespace macro operands were not parsed");
    IReadOnlyList<AssemblyNodeQueryResult> macroQuery =
        AssemblySourceQuery.Select(
            file,
            "MACRO_INVOCATIONS",
            "rootLabel",
            "objectData");
    Assert(
        macroQuery.Count == 2 &&
        macroQuery[0].Path == "fixture.s" &&
        macroQuery[0].Line == 4 &&
        macroQuery[1].Operands.Count == 2,
        "structured macro query lost order, operands, or source spans");
    Assert(
        AssemblySourceQuery.Select(file, "LABELS")
            .Any(node => node.Name == "usOnly" && node.IsActive),
        "structured label query lost active-branch state");
    Assert(
        AssemblySourceQuery.Select(file, "LABELS")
            .Any(node => node.Name == "++"),
        "anonymous forward label was not represented as a label node");
    Assert(file.RequireUniqueLabel("jpOnly").IsActive == false,
        "configured JP branch should be inactive");
    Assert(file.RequireUniqueLabel("usOnly").IsActive,
        "configured US branch should be active");
    Assert(file.GetConstants("ALIAS").Single().Expression == "rootLabel",
        "constant alias was not retained");
    Assert(file.PositionAt(source.IndexOf("ld a", StringComparison.Ordinal)).Line == 6,
        "line-start offsets changed");
    repository.AssertReadOnce();

    bool escaped = false;
    try
    {
        repository.Open(Path.Combine(root, "..", "outside.s"));
    }
    catch (AssemblySourceException exception)
    {
        escaped = exception.Message.Contains("outside", StringComparison.OrdinalIgnoreCase);
    }
    Assert(escaped, "repository accepted a path outside its root");

    string unknownPath = Path.Combine(root, "unknown.s");
    File.WriteAllText(unknownPath, "#odd syntax\n", new UTF8Encoding(false));
    AssemblySourceFile unknown = repository.Open(unknownPath);
    Assert(unknown.Nodes[0].Kind == AssemblyNodeKind.Unrecognized,
        "unrecognized syntax was discarded");

    string malformedPath = Path.Combine(root, "malformed.s");
    File.WriteAllText(malformedPath, ".else\n", new UTF8Encoding(false));
    bool sourceAware = false;
    try
    {
        repository.Open(malformedPath);
    }
    catch (AssemblySourceException exception)
    {
        sourceAware = exception.Message.Contains(
            "malformed.s:1:1",
            StringComparison.Ordinal);
    }
    Assert(sourceAware, "malformed conditional did not report path, line, and column");
}

static void RunManifestTests(string root)
{
    string assets = Path.Combine(root, "assets");
    Directory.CreateDirectory(assets);
    File.WriteAllBytes(Path.Combine(assets, "one.bin"), new byte[] { 1, 2, 3 });
    File.WriteAllText(
        Path.Combine(assets, "two.tsv"),
        "# header\nalpha\t1\nalpha\t2\nbeta\t3\n",
        new UTF8Encoding(false));
    File.WriteAllText(Path.Combine(assets, "ignored.import"), "volatile");

    GeneratedAssetManifest expected = GeneratedAssetManifest.Capture(assets);
    Assert(expected.Entries.Count == 2, "manifest included Godot .import metadata");
    GeneratedAssetEntry table = expected.Entries.Single(entry => entry.Path == "two.tsv");
    Assert(table.RecordCount == 3, "manifest record count changed");
    expected.AssertEquivalent(GeneratedAssetManifest.Capture(assets));

    File.AppendAllText(Path.Combine(assets, "two.tsv"), "gamma\t4\n");
    bool rejected = false;
    try
    {
        expected.AssertEquivalent(GeneratedAssetManifest.Capture(assets));
    }
    catch (InvalidDataException exception)
    {
        rejected = exception.Message.Contains("two.tsv", StringComparison.Ordinal);
    }
    Assert(rejected, "manifest comparison accepted modified output");
}

static void RunImporterBoundaryTests()
{
    string repositoryRoot = Environment.CurrentDirectory;
    string importerRoot = Path.Combine(repositoryRoot, "tools", "import_oracles");
    if (!Directory.Exists(importerRoot))
        return;

    string[] stagePaths = Directory.GetFiles(
        importerRoot,
        "*.ps1",
        SearchOption.TopDirectoryOnly);
    foreach (string path in stagePaths)
    {
        string source = File.ReadAllText(path);
        Assert(!source.Contains("Get-Content", StringComparison.OrdinalIgnoreCase),
            $"{Path.GetFileName(path)} bypasses the import source repository with Get-Content");
        string fileName = Path.GetFileName(path);
        if (fileName.Equals(
                "Initialize-Import.ps1",
                StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(
                "Write-GeneratedTableManifest.ps1",
                StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }
        Assert(!source.Contains("ReadAllText", StringComparison.OrdinalIgnoreCase) &&
            !source.Contains("ReadAllLines", StringComparison.OrdinalIgnoreCase),
            $"{Path.GetFileName(path)} performs direct file reads");
    }

    string hostSource = File.ReadAllText(Path.Combine(
        repositoryRoot,
        "tools",
        "OracleImporter",
        "AssemblySourceRepository.cs"));
    Assert(hostSource.Contains("File.ReadAllText(fullPath)", StringComparison.Ordinal),
        "assembly source repository no longer owns its single physical read");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
