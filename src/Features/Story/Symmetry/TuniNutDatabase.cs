using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TuniNutDatabase
{
    private readonly Dictionary<string, int> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TimedSparkleVisual> _visuals = new(StringComparer.Ordinal);
    public TuniNutDatabase()
    {
        var constants = GeneratedTable.Load("res://assets/oracle/objects/tuni_nut_constants.tsv",
            new GeneratedTableSchema("tuniNutMain.s constants", GeneratedTableKeySemantics.Unique,
                ["key", "value"], ["key"], headerRequired: true));
        foreach (var row in constants.Rows) _constants.Add(row.RequiredString(0), row.Decimal(1, -256, 65535));
        foreach (string key in new[] { "group", "room", "x", "y", "placed-y", "rise-wait", "rise-distance",
            "land-wait", "gravity", "darken", "speed", "stop-music", "drop-sound", "solve-sound" }) _ = Constant(key);
        if (_constants.Count != 14) throw new InvalidOperationException("tuniNutMain.s: unexpected constant count.");
        var visuals = GeneratedTable.Load("res://assets/oracle/objects/tuni_nut_visuals.tsv",
            new GeneratedTableSchema("tuniNutMain.s/$84:$07 visuals", GeneratedTableKeySemantics.Unique,
                ["name", "sprite", "source-offset", "tile-base", "palette", "animation"], ["name"], headerRequired: true));
        foreach (var row in visuals.Rows)
            _visuals.Add(row.RequiredString(0), new(row.RequiredString(1), row.Decimal(2, 0, 65535),
                row.Decimal(3, 0, 255), row.Decimal(4, 0, 7), row.RequiredString(5)));
        _ = Visual("nut"); _ = Visual("sparkle");
        if (_visuals.Count != 2) throw new InvalidOperationException("tuniNutMain.s: unexpected visual count.");
    }
    public int Constant(string key) => _constants.TryGetValue(key, out int value) ? value :
        throw new InvalidOperationException($"tuniNutMain.s: missing constant '{key}'.");
    public TimedSparkleVisual Visual(string key) => _visuals.TryGetValue(key, out var visual) ? visual :
        throw new InvalidOperationException($"tuniNutMain.s: missing visual '{key}'.");
}
