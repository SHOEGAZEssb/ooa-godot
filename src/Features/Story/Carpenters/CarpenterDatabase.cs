using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CarpenterDatabase
{
    private readonly Dictionary<string, int> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<int, CarpenterScript> _scripts = new();
    public IReadOnlyList<CutsceneCommand> Commands { get; }
    internal string LeaveMessage { get; }

    public CarpenterDatabase()
    {
        var constants = GeneratedTable.Load("res://assets/oracle/objects/carpenter_constants.tsv",
            new GeneratedTableSchema("carpenter.s native constants", GeneratedTableKeySemantics.Unique,
                ["key", "value"], ["key"], headerRequired: true));
        foreach (var row in constants.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1, -65536, 65535));
        if (_constants.Count != 25)
            throw new InvalidOperationException("carpenter.s: expected 25 native constants.");
        var text = GeneratedTable.Load("res://assets/oracle/objects/carpenter_leave_text.tsv",
            new GeneratedTableSchema("carpenter.s TX_2307", GeneratedTableKeySemantics.Unique,
                ["text-id", "message"], ["text-id"], headerRequired: true));
        LeaveMessage = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(text.Rows[0].RequiredString(1)));
        Commands = CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/carpenter_commands.tsv");
        var scripts = GeneratedTable.Load("res://assets/oracle/objects/carpenter_scripts.tsv",
            new GeneratedTableSchema("carpenter.s script table", GeneratedTableKeySemantics.Unique,
                ["subid", "entry", "animation"], ["subid"], headerRequired: true));
        foreach (var row in scripts.Rows)
            _scripts.Add(row.HexByte(0), new(row.Decimal(1, 0, Commands.Count - 1), row.RequiredString(2)));
        foreach (int subid in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 })
            _ = Script(subid);
        if (_scripts.Count != 9)
            throw new InvalidOperationException("carpenter.s: unexpected room $0:$25 script entry.");
    }

    public int Constant(string key) => _constants.TryGetValue(key, out int value) ? value :
        throw new InvalidOperationException($"carpenter.s: missing native constant '{key}'.");
    public CarpenterScript Script(int subid) => _scripts.TryGetValue(subid, out var script) ? script :
        throw new InvalidOperationException($"carpenter.s: unsupported script subid ${subid:x2}.");
}

internal sealed record CarpenterScript(int Entry, string Animation);
