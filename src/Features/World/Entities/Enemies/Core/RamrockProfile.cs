using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RamrockProfile
{
    private static readonly Lazy<RamrockProfile> LazyShared = new(() => new());
    internal static RamrockProfile Shared => LazyShared.Value;
    private readonly Dictionary<string, List<int>> _tables = new();
    private readonly Dictionary<int, IReadOnlyDictionary<int, Color[]>> _palettes = new();
    private RamrockProfile()
    {
        var table = GeneratedTable.Load("res://assets/oracle/enemies/ramrock.tsv",
            new GeneratedTableSchema("Ramrock native tables", GeneratedTableKeySemantics.Grouped,
                ["profile", "index", "value", "source"], ["profile"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            string key = row.RequiredString(0);
            if (!_tables.TryGetValue(key, out var values)) _tables.Add(key, values = []);
            if (row.UnsignedDecimal(1) != values.Count) throw row.Invalid(1, "contiguous native table indices");
            values.Add(row.Decimal(2)); _ = row.RequiredString(3);
        }
        foreach (string name in new[] { "ramrockArm_subid0And1XPositions", "ramrockArm_subid4And5XPositions", "ramrockArm_subid4And5Angles" })
            Require(name, 2);
        foreach (int mode in new[] { 0x0c, 0x0e, 4, 6 }) Require($"effects-{mode:x2}", 32);
        Require("active-05", 32); Require("active-07", 32);
        Require("part-front-offsets", 16);
        if (_tables.Count != 10) throw new InvalidOperationException("ramrock.s: unconsumed native tables.");
        foreach (int id in new[] { 0x83, 0x84 })
            _palettes.Add(id, new Dictionary<int, Color[]> { [6] = OracleGraphicsData.LoadPaletteColors($"res://assets/oracle/enemies/ramrock_palette_{id:x2}.bin") });
    }
    private void Require(string key, int count)
    {
        if (!_tables.TryGetValue(key, out var values) || values.Count != count)
            throw new InvalidOperationException($"ramrock.s: {key} requires {count} ordered values.");
    }
    internal int HandX(int subid) => _tables[subid < 2 ? "ramrockArm_subid0And1XPositions" : "ramrockArm_subid4And5XPositions"][subid & 1];
    internal int HandAngle(int subid) => _tables["ramrockArm_subid4And5Angles"][subid & 1];
    internal Vector2I PartFrontOffset(int angle)
    {
        int index = ((angle + 2) & 0x1c) >> 1;
        var offsets = _tables["part-front-offsets"];
        return new(unchecked((sbyte)offsets[index + 1]), unchecked((sbyte)offsets[index]));
    }
    internal int Effect(int mode, int item) => _tables[$"effects-{mode:x2}"][item];
    internal bool Active(int id, int item) => _tables[$"active-{id:x2}"][item] != 0;
    internal IReadOnlyDictionary<int, Color[]> Palette(int id) => _palettes[id];
}
