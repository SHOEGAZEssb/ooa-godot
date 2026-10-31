using Oracles.Importer;

namespace Oracles.Importer.Tests;

internal static class ConfiguredLabelScopeTests
{
    internal static void Run(string root)
    {
        string path = Path.Combine(root, "configured-labels.s");
        File.WriteAllText(path, ".ifdef ROM_AGES\nagesOnly: .db $aa\ntable: .db $11\n" +
            ".else\ntable: .db $22\n.endif\n  .db $33\nnext: .db $44\n");
        foreach (bool ages in new[] { true, false })
        {
            var file = new AssemblySourceRepository(root, new[] { ages ? "ROM_AGES" : "ROM_SEASONS" }).Open(path);
            if (file.GetLabels("table").Count != 2 || file.RequireUniqueLabel("agesOnly").IsActive != ages)
                throw new InvalidOperationException("Metadata must retain inactive and duplicate physical label definitions.");
            var rows = AssemblySourceQuery.Select(file, "DATA_DIRECTIVES", "table", ".db");
            string[] expected = [ages ? "$11" : "$22", "$33"];
            if (!rows.SelectMany(row => row.Operands).SequenceEqual(expected) || rows.Any(row => !row.IsActive) ||
                rows[0].Line != (ages ? 3 : 5) || rows[1].Line != 7)
                throw new InvalidOperationException("Configured data queries must follow the active definition through inactive labels to the next active global label.");
            if (!ages)
            {
                bool rejected = false;
                try { _ = AssemblySourceQuery.Select(file, "DATA_DIRECTIVES", "agesOnly"); }
                catch (AssemblySourceException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("A selected-build query must reject an inactive-only definition.");
            }
        }
        string ambiguous = Path.Combine(root, "active-duplicates.s");
        File.WriteAllText(ambiguous, "table: .db $11\ntable: .db $22\n");
        var duplicate = new AssemblySourceRepository(root, []).Open(ambiguous);
        bool failed = false;
        try { _ = AssemblySourceQuery.Select(duplicate, "DATA_DIRECTIVES", "table"); }
        catch (AssemblySourceException) { failed = true; }
        if (!failed) throw new InvalidOperationException("Two active definitions must remain an explicit ambiguity.");
    }
}
