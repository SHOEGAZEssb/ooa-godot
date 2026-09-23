using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SmogControllerDatabase
{
    internal SmogPhaseRecord[] Phases { get; } = new SmogPhaseRecord[4];
    internal SmogEnemySpawn Intro { get; }

    internal SmogControllerDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/smog_controller.tsv",
            new GeneratedTableSchema("INTERAC_SMOG_BOSS $33 phase tables", GeneratedTableKeySemantics.Unique,
                ["profile", "index", "value", "source"], ["profile", "index"], headerRequired: true));
        int cursor = 0;
        byte[] Read(string profile)
        {
            var bytes = new List<byte>();
            while (cursor < table.Rows.Count && table.Rows[cursor].RequiredString(0) == profile)
            {
                var row = table.Rows[cursor++];
                if (row.UnsignedDecimal(1) != bytes.Count) throw row.Invalid(1, $"ordered {profile} byte {bytes.Count}");
                bytes.Add((byte)row.HexByte(2));
            }
            if (bytes.Count == 0) throw new InvalidOperationException($"Smog $33 missing ordered profile {profile}.");
            return bytes.ToArray();
        }
        var tiles = new SmogTileWrite[4][];
        for (int phase = 0; phase < 4; phase++)
        {
            var bytes = Read($"tiles-{phase}");
            if (bytes.Length < 3 || bytes.Length % 2 != 1 || bytes[^1] != 0)
                throw new InvalidOperationException($"Smog $33 phase${phase:x2} requires tile pairs and a final zero terminator.");
            tiles[phase] = new SmogTileWrite[bytes.Length / 2];
            for (int i = 0; i < tiles[phase].Length; i++)
            {
                if (bytes[i * 2] == 0) throw new InvalidOperationException($"Smog $33 phase${phase:x2} contains an early tile terminator.");
                tiles[phase][i] = new(bytes[i * 2], bytes[i * 2 + 1]);
            }
        }
        var counts = Read("counts");
        var spawns = Read("spawns");
        var positions = Read("link-positions");
        if (counts.Length != 4 || positions.Length != 8 || spawns.Length % 4 != 0 || cursor != table.Rows.Count)
            throw new InvalidOperationException("Smog $33 phase count, Link positions or spawn stream is malformed.");
        SmogEnemySpawn Spawn(int index, int phase)
        {
            if (index * 4 + 3 >= spawns.Length) throw new InvalidOperationException("Smog $33 spawn stream ended before its phase count.");
            int subid = spawns[index * 4], direction = spawns[index * 4 + 3];
            if (subid is not (0 or 2 or 0x82) || direction > 3)
                throw new InvalidOperationException($"Smog $33 spawn${index:x2} has unsupported subid${subid:x2}/direction${direction:x2}.");
            return new(new(spawns[index * 4 + 2], spawns[index * 4 + 1]),subid,phase,direction);
        }
        Intro = Spawn(0,0);
        int next = 1;
        for (int phase = 0; phase < 4; phase++)
        {
            int first = next;
            var enemies = new SmogEnemySpawn[counts[phase]];
            for (int i = 0; i < enemies.Length; i++) enemies[i] = Spawn(next++,phase);
            Phases[phase] = new(new(positions[phase * 2 + 1],positions[phase * 2]),tiles[phase],first,enemies);
        }
        if (next * 4 != spawns.Length) throw new InvalidOperationException("Smog $33 spawn stream has unconsumed records.");
    }
}

internal readonly record struct SmogTileWrite(int Position, int Tile);
internal sealed record SmogPhaseRecord(Vector2I LinkPosition, SmogTileWrite[] Tiles, int FirstSpawnIndex, SmogEnemySpawn[] Enemies);
