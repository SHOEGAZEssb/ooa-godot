using System;
using System.Collections.Generic;

namespace oracleofages;

// Dungeon branch of PART_SEA_EFFECTS $2e; ordinary TILETYPE_HOLE remains
// harmless underwater. These raw metatiles are handled by the later PART pass.
internal sealed class SuctionPitDatabase
{
    internal static SuctionPitDatabase Shared { get; } = new();
    private readonly HashSet<(int Mode, byte Tile)> _tiles = new();
    internal (byte Y, byte X)[] Probes { get; } = new (byte, byte)[5];
    internal int[] Speeds { get; } = new int[8];
    private SuctionPitDatabase()
    {
        var tiles = GeneratedTable.Load("res://assets/oracle/metadata/suction_pit_tiles.tsv",
            new GeneratedTableSchema("dungeon suction tiles", GeneratedTableKeySemantics.Unique,
                ["active-collisions", "order", "tile", "effect", "source"], ["active-collisions", "tile"]));
        foreach (var row in tiles.Rows)
        {
            int mode = row.Decimal(0);
            if (mode is not (2 or 5) || row.Decimal(3) != 2 ||
                row.Decimal(1) != _tiles.Count % 4 ||
                !_tiles.Add((mode, (byte)row.HexByte(2))))
                throw row.Invalid(0, "a unique dungeon suction tile in collision mode 2 or 5");
        }
        var probes = GeneratedTable.Load("res://assets/oracle/metadata/suction_pit_probes.tsv",
            new GeneratedTableSchema("dungeon suction probes", GeneratedTableKeySemantics.Ordered, ["order", "y", "x", "source"]));
        for (int index = 0; index < probes.Rows.Count; index++)
        {
            var row = probes.Rows[index];
            if (row.Decimal(0, 0, 4) != index) throw row.Invalid(0, "contiguous source probe order");
            Probes[index] = ((byte)row.Decimal(1, 0, 255), (byte)row.Decimal(2, 0, 255));
        }
        var speeds = GeneratedTable.Load("res://assets/oracle/metadata/suction_pit_speeds.tsv",
            new GeneratedTableSchema("dungeon suction speeds", GeneratedTableKeySemantics.Ordered, ["order", "speed", "source"]));
        for (int index = 0; index < speeds.Rows.Count; index++)
        {
            var row = speeds.Rows[index];
            if (row.Decimal(0, 0, 7) != index) throw row.Invalid(0, "contiguous source speed order");
            Speeds[index] = row.HexByte(1);
        }
        if (tiles.Rows.Count != 8 || probes.Rows.Count != 5 || speeds.Rows.Count != 8)
            throw new InvalidOperationException("seaEffectTiles1/2.s/seaEffects.s: incomplete dungeon suction data.");
    }
    internal bool Matches(int mode, byte tile) => _tiles.Contains((mode, tile));
    internal bool ShouldSpawn(OracleRoomData room)
    {
        foreach (byte tile in room.Layout) if (Matches(room.ActiveCollisions, tile)) return true;
        return false;
    }
}
