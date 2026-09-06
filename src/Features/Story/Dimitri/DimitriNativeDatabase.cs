using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class DimitriNativeDatabase
{
    private readonly Dictionary<string, List<Vector2>> _probes = new();
    internal int[] CollisionMasks { get; } = new int[16];
    internal IReadOnlyList<Vector2> Probes(string kind) => _probes[kind];

    internal DimitriNativeDatabase()
    {
        foreach (var row in GeneratedTable.Load("res://assets/oracle/cutscenes/dimitri_native_probes.tsv",
            new GeneratedTableSchema("Dimitri native probes", GeneratedTableKeySemantics.Ordered,
                ["kind", "index", "x", "y", "source"], headerRequired: true)).Rows)
        {
            string kind = row.RequiredString(0);
            if (!_probes.TryGetValue(kind, out var probes)) _probes.Add(kind, probes = new());
            if (row.UnsignedDecimal(1) != probes.Count) throw row.Invalid(1, "source probe order");
            probes.Add(new(row.Decimal(2), row.Decimal(3)));
        }
        if (_probes.Count != 3 || Probes("carry").Count != 4 || Probes("cliff").Count != 4 || Probes("collision").Count != 8)
            throw new InvalidOperationException("Dimitri/commonCode.s native probe shape changed.");
        var masks = GeneratedTable.Load("res://assets/oracle/cutscenes/companion_collision_masks.tsv",
            new GeneratedTableSchema("Companion collision masks", GeneratedTableKeySemantics.Ordered,
                ["index", "mask", "source"], headerRequired: true));
        if (masks.Rows.Count != 16) throw new InvalidOperationException("bank0.s companion collision masks require 16 rows.");
        for (int index = 0; index < 16; index++)
        {
            if (masks.Rows[index].UnsignedDecimal(0) != index) throw masks.Rows[index].Invalid(0, "source mask order");
            CollisionMasks[index] = masks.Rows[index].HexByte(1);
        }
    }
}
