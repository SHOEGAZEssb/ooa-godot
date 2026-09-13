using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class LeverLavaDatabase
{
    private readonly Dictionary<int, LeverLavaScript> _scripts = new();

    internal LeverLavaDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/lever_lava_scripts.tsv",
            new GeneratedTableSchema("lever lava scripts", GeneratedTableKeySemantics.Unique,
                ["subid", "interval", "script", "source"], ["subid"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            byte[] bytes = Array.ConvertAll(row.RequiredString(2).Split(','), value => Convert.ToByte(value, 16));
            if (bytes.Length < 3 || bytes[0] == 0 || bytes[^1] != 0 || bytes[^2] != 0)
                throw row.Invalid(2, "ordered zero-terminated tile groups followed by an empty group");
            for (int i = 0; i < bytes.Length - 2; i++)
                if (bytes[i] == 0 && bytes[i + 1] == 0)
                    throw row.Invalid(2, "no script bytes after the terminating empty group");
            _scripts.Add(row.HexByte(0), new LeverLavaScript(row.HexByte(1), bytes, row.RequiredString(3)));
        }
        if (_scripts.Count != 6)
            throw new InvalidOperationException("INTERAC_LEVER_LAVA_FILLER needs all six source scripts.");
    }

    internal LeverLavaScript Script(int subid) => _scripts.TryGetValue(subid, out var script) ? script
        : throw new InvalidOperationException($"INTERAC_LEVER_LAVA_FILLER ${subid:x2} script is not imported.");
}

internal readonly record struct LeverLavaScript(int Interval, IReadOnlyList<byte> Bytes, string Source);
