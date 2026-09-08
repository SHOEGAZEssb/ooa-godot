using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SymmetryDatabase
{
    private readonly Dictionary<int, int> _scripts = new();
    private readonly Dictionary<string, int> _constants = new(StringComparer.Ordinal);
    public IReadOnlyList<CutsceneCommand> Commands { get; } =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/symmetry_commands.tsv");

    public SymmetryDatabase()
    {
        var scripts = GeneratedTable.Load("res://assets/oracle/objects/symmetry_scripts.tsv",
            new GeneratedTableSchema("symmetryNpc.s script table", GeneratedTableKeySemantics.Unique,
                ["subid", "entry"], ["subid"], headerRequired: true));
        foreach (var row in scripts.Rows)
            _scripts.Add(row.HexByte(0), row.Decimal(1, 0, Commands.Count - 1));
        for (int i = 0; i <= 0x0d; i++) _ = Entry(i);
        if (_scripts.Count != 14) throw new InvalidOperationException("symmetryNpc.s: unexpected script table size.");
        var constants = GeneratedTable.Load("res://assets/oracle/objects/symmetry_constants.tsv",
            new GeneratedTableSchema("symmetryNpc.s constants", GeneratedTableKeySemantics.Unique,
                ["key", "value"], ["key"], headerRequired: true));
        foreach (var row in constants.Rows) _constants.Add(row.RequiredString(0), row.Decimal(1, 0, 255));
        foreach (string key in new[] { "placed-flag", "sister-flag", "brother-flag", "finished-flag" }) _ = Constant(key);
        if (_constants.Count != 4) throw new InvalidOperationException("symmetryNpc.s: unexpected constants.");
    }

    public int Entry(int subid) => _scripts.TryGetValue(subid, out int entry) ? entry :
        throw new InvalidOperationException($"symmetryNpc.s: unsupported subid ${subid:x2}.");
    public int Constant(string key) => _constants.TryGetValue(key, out int value) ? value :
        throw new InvalidOperationException($"symmetryNpc.s: missing constant '{key}'.");
}
