using System;
using System.Collections.Generic;

namespace oracleofages;

// PART_SEA_EFFECTS $2e; ordinary TILETYPE_HOLE remains harmless underwater.
// Pollution and raw whirlpool metatiles are handled by the later PART pass.
internal sealed class SuctionPitDatabase
{
    internal static SuctionPitDatabase Shared { get; } = new();
    private readonly Dictionary<(int Mode, byte Tile), int> _effects = new();
    private readonly HashSet<(int Mode, byte Tile)> _spawnTiles = new();
    private readonly Dictionary<(int Mode, byte Tile), int> _currents = new();
    internal (byte Y, byte X)[] Probes { get; } = new (byte, byte)[10];
    internal int[] Speeds { get; } = new int[8];
    private SuctionPitDatabase()
    {
        var tiles = GeneratedTable.Load("res://assets/oracle/metadata/suction_pit_tiles.tsv",
            new GeneratedTableSchema("sea effect tiles", GeneratedTableKeySemantics.Unique,
                ["active-collisions", "order", "tile", "effect", "source"], ["active-collisions", "tile"]));
        foreach (var row in tiles.Rows)
        {
            int mode = row.Decimal(0, 0, 5);
            if (!_effects.TryAdd((mode, (byte)row.HexByte(2)), row.Decimal(3, 0, 2)))
                throw row.Invalid(0, "a unique sea-effect tile");
        }
        var spawn = GeneratedTable.Load("res://assets/oracle/metadata/sea_effect_spawn_tiles.tsv",
            new GeneratedTableSchema("sea effect spawn tiles", GeneratedTableKeySemantics.Unique,
                ["active-collisions", "order", "tile", "source"], ["active-collisions", "tile"]));
        foreach (var row in spawn.Rows)
            if (!_spawnTiles.Add((row.Decimal(0, 0, 5), (byte)row.HexByte(2))))
                throw row.Invalid(0, "a unique sea-effect spawn tile");
        var currents = GeneratedTable.Load("res://assets/oracle/metadata/sea_effect_currents.tsv",
            new GeneratedTableSchema("sea effect currents", GeneratedTableKeySemantics.Unique,
                ["active-collisions", "order", "tile", "angle", "source"], ["active-collisions", "tile"]));
        foreach (var row in currents.Rows)
            if (!_currents.TryAdd((row.Decimal(0, 0, 5), (byte)row.HexByte(2)), row.HexByte(3)))
                throw row.Invalid(0, "a unique sea-effect current");
        var probes = GeneratedTable.Load("res://assets/oracle/metadata/suction_pit_probes.tsv",
            new GeneratedTableSchema("dungeon suction probes", GeneratedTableKeySemantics.Ordered, ["order", "y", "x", "source"]));
        for (int index = 0; index < probes.Rows.Count; index++)
        {
            var row = probes.Rows[index];
            if (row.Decimal(0, 0, 9) != index) throw row.Invalid(0, "contiguous source probe order");
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
        if (tiles.Rows.Count != 12 || spawn.Rows.Count != 16 || currents.Rows.Count != 12 ||
            probes.Rows.Count != 10 || speeds.Rows.Count != 8)
            throw new InvalidOperationException("seaEffectTiles1/2.s/seaEffects.s: incomplete sea-effect data.");
    }
    internal bool Matches(int mode, byte tile) => _effects.TryGetValue((mode, tile), out int effect) && effect == 2;
    internal bool TryGetEffect(int mode, byte tile, out int effect) => _effects.TryGetValue((mode, tile), out effect);
    internal bool TryGetCurrent(int mode, byte tile, out int angle) => _currents.TryGetValue((mode, tile), out angle);
    internal bool ShouldSpawn(OracleRoomData room)
    {
        // Surface ($00, including mounted objects) and indoor ($01) dispatch
        // remain unsupported. Their source rows are retained explicitly, but
        // this owner currently admits the dungeon/underwater branches only.
        if (room.ActiveCollisions is not (2 or 4 or 5)) return false;
        foreach (byte tile in room.Layout) if (_spawnTiles.Contains((room.ActiveCollisions, tile))) return true;
        return false;
    }
}
