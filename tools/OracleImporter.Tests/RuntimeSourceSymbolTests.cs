using OracleOfAges.Importer;
using System.Globalization;
using System.Text.RegularExpressions;

// Compile-time runtime symbols are deliberately independent of generated tables.
// Check their source annotations against active vanilla Ages assembly nodes, so
// changing both a caller and its constant cannot accidentally bless a wrong ID.
internal static class RuntimeSourceSymbolTests
{
    internal static void Verify(string projectRoot, string disassemblyRoot)
    {
        var repository = new AssemblySourceRepository(disassemblyRoot,
            ["ROM_AGES", "REGION_US", "AGES_ENGINE", "BUILD_VANILLA"]);
        var sources = new Dictionary<string, IReadOnlyDictionary<string, int>>();
        var declaration = new Regex(
            @"// (?<path>(?:constants|include)/[\w/]+\.s): (?<symbol>[\w.]+)[^\r\n]*\r?\n\s*(?:public const int )?(?<member>\w+) = 0x(?<value>[0-9a-f]+)[;,]");
        int checkedCount = 0;
        foreach (string path in Directory.EnumerateFiles(
            Path.Combine(projectRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in declaration.Matches(File.ReadAllText(path)))
            {
                string sourcePath = match.Groups["path"].Value;
                string symbol = match.Groups["symbol"].Value;
                if (!sources.TryGetValue(sourcePath, out IReadOnlyDictionary<string, int>? values))
                    sources.Add(sourcePath, values = ReadValues(repository.Open(sourcePath)));
                int actual = int.Parse(match.Groups["value"].Value, NumberStyles.HexNumber);
                if (!values.TryGetValue(symbol, out int expected))
                    throw new InvalidDataException($"{path}: {sourcePath}:{symbol} is not an active, resolved Ages symbol.");
                if (actual != expected)
                    throw new InvalidDataException(
                        $"{path}: {symbol} is ${actual:x}, but {sourcePath} defines ${expected:x}.");
                checkedCount++;
            }
        }
        if (checkedCount == 0)
            throw new InvalidDataException("No annotated runtime source symbols were checked.");
        repository.AssertReadOnce();
        Console.WriteLine($"Verified {checkedCount} runtime constants against vanilla Ages source symbols.");
    }

    private static IReadOnlyDictionary<string, int> ReadValues(AssemblySourceFile file)
    {
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        int? offset = null;
        string? structure = null;
        foreach (AssemblyNode node in file.Nodes.Where(node => node.IsActive))
        {
            string code = node.Code.Trim();
            if (file.RelativePath == "include/structs.s")
            {
                if (code.StartsWith(".struct ", StringComparison.OrdinalIgnoreCase))
                    structure = code[8..].Trim();
                else if (code.Equals(".endst", StringComparison.OrdinalIgnoreCase))
                    structure = null;
                if (structure is null || node.Kind != AssemblyNodeKind.Label) continue;
                Match address = Regex.Match(node.Comment, @"^\s*\$([c-d][0-9a-f]{3})\b",
                    RegexOptions.IgnoreCase);
                if (address.Success)
                    values[$"{structure}.{node.Name}"] = int.Parse(
                        address.Groups[1].Value, NumberStyles.HexNumber);
                continue;
            }
            if (file.RelativePath == "include/wram.s")
            {
                if (code == ".union wTmpcfc0")
                    values["wTmpcfc0"] = values["wRoomLayoutEnd"];
                if (node.Kind != AssemblyNodeKind.Label || !node.Name.StartsWith('w')) continue;
                // The disassembly documents Ages first when addresses differ by game.
                Match address = Regex.Match(node.Comment, @"^\s*\$([c-d][0-9a-f]{3})(?:\b|/)",
                    RegexOptions.IgnoreCase);
                if (address.Success)
                    values[node.Name] = int.Parse(address.Groups[1].Value, NumberStyles.HexNumber);
                continue;
            }
            if (code.StartsWith(".enum ", StringComparison.OrdinalIgnoreCase))
            {
                offset = Integer(code[6..].Trim());
                continue;
            }
            if (code.Equals(".ende", StringComparison.OrdinalIgnoreCase))
            {
                offset = null;
                continue;
            }
            if (node.Kind == AssemblyNodeKind.Constant)
            {
                // Only a literal .define is needed for these domain symbols.
                // Aliases/expressions remain unresolved and fail if referenced.
                Match define = Regex.Match(code, @"^\.define\s+(\w+)\s+(\$[0-9a-f]+|[0-9]+)$",
                    RegexOptions.IgnoreCase);
                if (define.Success)
                    values[define.Groups[1].Value] = Integer(define.Groups[2].Value);
                continue;
            }
            if (offset.HasValue)
            {
                Match entry = Regex.Match(code, @"^(\w+)\s+(db|\.db|dsb)(?:\s+([0-9]+))?$",
                    RegexOptions.IgnoreCase);
                if (!entry.Success) continue;
                values[entry.Groups[1].Value] = offset.Value;
                // WLA .db is a zero-width alias inside an enum; db consumes a byte.
                offset += entry.Groups[2].Value.ToLowerInvariant() switch
                {
                    ".db" => 0,
                    "db" => 1,
                    "dsb" => Integer(entry.Groups[3].Value),
                    _ => throw new InvalidDataException($"Unsupported enum entry at {node.Span}.")
                };
            }
        }
        return values;
    }

    private static int Integer(string value) => value.StartsWith('$')
        ? int.Parse(value[1..], NumberStyles.HexNumber)
        : int.Parse(value, CultureInfo.InvariantCulture);
}
