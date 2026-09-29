using System;
using Godot;

namespace oracleofages;

/// <summary>Source work of Link's eight collision probes and wall-bit normalization.</summary>
internal sealed class OracleLinkWallWork
{
    internal static OracleLinkWallWork Shared { get; } = new();
    private readonly ProbeResult[,] _probes = new ProbeResult[32, 128];
    private readonly int[] _loops = new int[4];
    private readonly int[] _wrappers = new int[256];

    private OracleLinkWallWork()
    {
        var table = GeneratedTable.Load("res://assets/oracle/timing/link_wall_cpu.tsv",
            new GeneratedTableSchema("Link wall-probe work", GeneratedTableKeySemantics.Unique,
                ["operation", "index", "variant", "cpu-cycles", "blocked", "source"],
                ["operation", "index", "variant"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int index = row.HexByte(1), clocks = row.Decimal(3, 1, 100_000);
            switch (row.RequiredString(0))
            {
                case "probe" when index < 32:
                    _probes[index, row.Decimal(2, 0, 127)] = new(clocks, row.Decimal(4, 0, 1) != 0);
                    break;
                case "loop" when index == 0:
                    _loops[row.Decimal(2, 0, 3)] = clocks;
                    break;
                case "wrapper" when row.Decimal(2, 0, 0) == 0:
                    _wrappers[index] = clocks;
                    break;
                default: throw row.Invalid(0, "source wall probe, eight-probe loop, or normalization wrapper");
            }
            _ = row.RequiredString(5);
        }
        if (table.Rows.Count != 4356)
            throw new InvalidOperationException("Link wall work requires 4096 probes, four loops and 256 normalization paths.");
        foreach (ProbeResult probe in _probes)
            if (probe.Clocks == 0) throw new InvalidOperationException("Missing source Link collision-probe timing.");
        if (Array.Exists(_loops, clocks => clocks == 0) || Array.Exists(_wrappers, clocks => clocks == 0))
            throw new InvalidOperationException("Missing source Link collision loop or normalization timing.");
    }

    internal ProbeResult Probe(int collision, Vector2 point, bool raised)
    {
        // Values >=$10 all select a special mask by their low nibble;
        // $ff is therefore special$1f, not a solid simple collision.
        int normalized = collision < 0x10 ? collision : 0x10 | (collision & 15);
        int x = (Mathf.FloorToInt(point.X) & 15) >> 1;
        int y = (Mathf.FloorToInt(point.Y) & 15) >> 1;
        return _probes[normalized, (raised ? 64 : 0) + y * 8 + x];
    }

    internal int Calculate(OracleRoomData room, Vector2 position, bool raised)
    {
        bool sidescroll = (room.TilesetFlags & (int)TilesetFlags.Sidescroll) != 0;
        int clocks = _loops[(sidescroll ? 2 : 0) + (raised ? 1 : 0)];
        int rawWalls = LinkWallProbe.Shared.RawWalls(position, sidescroll, point => {
            ProbeResult probe = Probe(room.GetNativeCollisionAt(point), point, raised);
            clocks += probe.Clocks;
            return probe.Blocked;
        });
        return clocks + _wrappers[rawWalls];
    }

    internal readonly record struct ProbeResult(int Clocks, bool Blocked);
}
