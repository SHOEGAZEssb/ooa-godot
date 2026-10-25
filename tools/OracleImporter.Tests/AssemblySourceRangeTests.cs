using System.Text.Json;
using Oracles.Importer;

namespace Oracles.Importer.Tests;

internal static class AssemblySourceRangeTests
{
    public static void Run(string root)
    {
        const string source = "root:\r\n  wait 1 ; WAIT 99\r\n" +
            ".ifdef REGION_JP\r\n  wait 2\r\n.else\r\n" +
            "@alias:\r\n@entry:\r\n  wait 3\r\n.endif\r\n" +
            "next:\r\n  wait 4\r\n";
        string path = Path.Combine(root, "ranges.s");
        File.WriteAllText(path, source);
        AssemblySourceFile file = new AssemblySourceRepository(root, new[] { "REGION_US" }).Open(path);
        int next = source.IndexOf("next:", StringComparison.Ordinal);
        Assert(file.FindSourceLine(0, next, "^WAIT ") == 2, "case-insensitive first source match changed");
        Assert(file.FindSourceLine(0, next, "^wait ", 1) == 8, "inactive branch or occurrence order changed");
        Assert(file.FindSourceLine(0, next, "^wait ", 2) == -1, "exclusive range end included next block");
        Assert(file.FindSourceLine(0, source.Length, "99") == -1, "comment became executable code");
        Assert(file.FindSourceLine(next, source.Length, "^wait ") == 11, "range start/end changed source lines");
        int start = source.IndexOf("@alias:", StringComparison.Ordinal);
        using JsonDocument nodes = JsonDocument.Parse(AssemblySourceQuery.SerializeRange(file, start, next));
        string[] labels = nodes.RootElement.EnumerateArray()
            .Where(node => node.GetProperty("Kind").GetString() == "Label")
            .Select(node => node.GetProperty("Name").GetString()!).ToArray();
        Assert(labels.SequenceEqual(new[] { "@alias", "@entry" }), "range lost adjacent labels or their order");
        Assert(!nodes.RootElement.EnumerateArray().Any(node => !node.GetProperty("IsActive").GetBoolean()),
            "range query emitted inactive nodes");
        Assert(!file.GetNodesInRange(start, start).Any(), "empty range returned nodes");
        bool invalid = false;
        try { _ = file.GetNodesInRange(-1, next).ToArray(); }
        catch (AssemblySourceException exception) { invalid = exception.Message.Contains("ranges.s"); }
        Assert(invalid, "invalid range lost its source-aware diagnostic");
        Console.WriteLine("Indexed source-range fixtures passed (boundaries, aliases, active branches, occurrence and source lines).");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
