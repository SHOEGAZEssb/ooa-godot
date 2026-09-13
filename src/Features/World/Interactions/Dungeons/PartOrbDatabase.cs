using System;

namespace oracleofages;

internal sealed class PartOrbDatabase
{
    internal static PartOrbDatabase Shared { get; } = new();
    private readonly int[] _lockouts = new int[32];
    internal int Speed { get; }
    internal int Right { get; }
    internal int Left { get; }
    internal byte BackgroundTile { get; }
    internal byte TileCollision { get; }
    internal int SubidMask { get; }
    internal int RadiusY { get; }
    internal int RadiusX { get; }
    private PartOrbDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/part_orb_collisions.tsv",
            new GeneratedTableSchema("PART_ORB collision responses", GeneratedTableKeySemantics.Unique,
                ["item", "enabled", "effect", "lockout", "source"], ["item"], headerRequired: true));
        if (table.Rows.Count != 32) throw new InvalidOperationException("Incomplete PART_ORB collision row.");
        for (int i = 0; i < 32; i++)
        {
            var row = table.Rows[i];
            int effect = row.HexByte(2), lockout = row.HexByte(3);
            if (row.HexByte(0) != i || effect is not (0 or 0x20 or 0x26) || lockout != (effect == 0x26 ? 28 : 0))
                throw row.Invalid(0, "ordered source orb collision responses");
            _lockouts[i] = row.Decimal(1, 0, 1) == 1 ? lockout : -1;
        }
        var script = GeneratedTable.Load("res://assets/oracle/objects/moving_orb_script.tsv",
            new GeneratedTableSchema("moving orb script", GeneratedTableKeySemantics.Unique,
                ["subid", "speed", "initial-direction", "right", "left", "source"], ["subid"], headerRequired: true)).SingleRow();
        if (script.HexByte(0) != 0 || script.HexByte(2) != 0) throw script.Invalid(0, "orb script00/DIR_UP");
        Speed = script.HexByte(1); Right = script.HexByte(3); Left = script.HexByte(4);
        var init = GeneratedTable.Load("res://assets/oracle/objects/part_orb_initialization.tsv",
            new GeneratedTableSchema("PART_ORB initialization", GeneratedTableKeySemantics.Unique,
                ["id", "background-tile", "tile-collision", "subid-mask", "radius-y", "radius-x", "source"], ["id"], headerRequired: true)).SingleRow();
        if (init.HexByte(0) != 3) throw init.Invalid(0, "stationary PART_ORB $03");
        BackgroundTile = (byte)init.HexByte(1); TileCollision = (byte)init.HexByte(2);
        SubidMask = init.HexByte(3); RadiusY = init.HexByte(4); RadiusX = init.HexByte(5);
    }
    internal int HitLockout(int collision) => collision is >= 0 and < 32 ? _lockouts[collision] : -1;
}
