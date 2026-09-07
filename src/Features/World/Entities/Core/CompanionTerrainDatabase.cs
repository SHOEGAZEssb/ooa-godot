using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CompanionTerrainDatabase
{
    private readonly Dictionary<string, List<Vector2>> _probes = new();
    internal int[] CollisionMasks { get; } = new int[16];
    internal IReadOnlyList<Vector2> Probes(string kind) => _probes[kind];

    internal bool IsSolid(OracleRoomData room, Vector2 sample, bool swimming = false)
    {
        if (sample.X < 0 || sample.Y < 0 || sample.X >= room.Width || sample.Y >= room.Height) return false;
        int tile = room.GetMetatile(sample);
        if (swimming && tile >= 0xfe) return false;
        if (tile is 0xd5 or 0xd6) return true;
        int collision = tile == 0xd4 ? 3 : room.GetTerrainInfo(sample).Collision;
        int x = Mathf.FloorToInt(sample.X) & 15, y = Mathf.FloorToInt(sample.Y) & 15;
        if (collision < 0x10) return (collision & (1 << ((y < 8 ? 2 : 0) + (x < 8 ? 1 : 0)))) != 0;
        int kind = collision & 15;
        return (CollisionMasks[kind] & (1 << ((kind < 8 ? x : y) >> 1))) != 0;
    }

    internal CompanionTerrainDatabase()
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
