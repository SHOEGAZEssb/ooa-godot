using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oracles;

internal sealed class PlacedPartDatabase
{
    private readonly Dictionary<int, PlacedPartDefinition> _definitions = new();
    private readonly Dictionary<string, byte[]> _tables = new();

    internal PlacedPartDatabase()
    {
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/placed_parts.tsv",
            new("placed PART definitions", GeneratedTableKeySemantics.Unique,
                ["id", "sprite", "tile-base", "palette", "source-grayscale-inverted", "radius-y", "radius-x",
                    "damage-quarters", "health", "mode", "initial-collision", "animations-base64", "source"],
                ["id"], headerRequired: true)).Rows)
        {
            int id = row.HexByte(0);
            if (id is not (PartId.FallingFire or PartId.SmallFairy)) throw row.Invalid(0, "PART $23/$28");
            string[] animations = Encoding.UTF8.GetString(Convert.FromBase64String(row.RequiredString(11))).Split('\n');
            if (animations.Length != 2) throw row.Invalid(11, "two PART animation entries");
            _definitions.Add(id, new(new($"part-{id:x2}", [row.RequiredString(1)], row.Decimal(2), row.Decimal(3),
                row.Boolean01(4), animations), row.Decimal(5), row.Decimal(6), row.Decimal(7), row.Decimal(8), row.HexByte(9),
                row.Boolean01(10)));
        }
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/placed_part_tables.tsv",
            new("placed PART tables", GeneratedTableKeySemantics.Unique,
                ["profile", "values", "source"], ["profile"], headerRequired: true)).Rows)
            _tables.Add(row.RequiredString(0), row.SplitRequired(1, ',').Select(value => Convert.ToByte(value, 16)).ToArray());
        if (_definitions.Count != 2 || _tables.Count != 7)
            throw new InvalidOperationException("PART$23/$28: incomplete imported definitions or tables.");
        foreach (string key in new[] { "active-23", "active-28", "collision-01", "collision-72" }) Require(key, 32);
        Require("fairy-speeds", 4); Require("fairy-offsets", 16); Require("fire-intervals", 4);
        foreach (int id in new[] { PartId.FallingFire, PartId.SmallFairy })
        for (int item = 0; item < 32; item++)
        {
            if (!Active(id, item)) continue;
            int expected = id == PartId.FallingFire
                ? item switch { 0 => 3, 3 => 6, _ => -1 }
                : item switch { 0 or >= 4 and <= 11 => 0x23, 0x0d or 0x17 => 0x24, _ => -1 };
            if (Effect(id, item) != expected || expected < 0)
                throw new NotSupportedException($"partActiveCollisions/objectCollisionTable: PART${id:x2} item${item:x2} has unsupported effect${Effect(id, item):x2}.");
        }
    }

    private void Require(string key, int length)
    {
        if (!_tables.TryGetValue(key, out byte[]? values) || values.Length != length)
            throw new InvalidOperationException($"PART$23/$28: missing or malformed source table '{key}'.");
    }
    internal PlacedPartDefinition Definition(int id) => _definitions[id];
    internal byte[] Table(string key) => _tables[key];
    internal bool Active(int id, int collision) => collision is >= 0 and < 32 && _tables[$"active-{id:x2}"][collision] != 0;
    internal int Effect(int id, int collision) => _tables[$"collision-{_definitions[id].Mode:x2}"][collision];
}

internal readonly record struct PlacedPartDefinition(DungeonInteractionVisual Visual, int RadiusY, int RadiusX,
    int Damage, int Health, int Mode, bool InitiallyCollidable);
