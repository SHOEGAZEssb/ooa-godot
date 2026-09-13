using System;

namespace oracleofages;

internal sealed class BlueEnergyBeadDatabase
{
    internal static BlueEnergyBeadDatabase Shared { get; } = new();
    internal int Count { get; }
    internal int Speed { get; }
    internal int Radius { get; }
    internal int DelayMask { get; }
    internal int DeleteAddress { get; }

    private BlueEnergyBeadDatabase()
    {
        var row = GeneratedTable.Load("res://assets/oracle/objects/blue_energy_bead.tsv",
            new GeneratedTableSchema("PART_BLUE_ENERGY_BEAD inward swirl", GeneratedTableKeySemantics.Unique,
                ["id", "count", "speed", "radius", "delay-mask", "delete-address", "source"],
                ["id"], headerRequired: true)).SingleRow();
        if (row.HexByte(0) != 0x53) throw row.Invalid(0, "PART_BLUE_ENERGY_BEAD $53");
        Count = row.HexByte(1); Speed = row.HexByte(2); Radius = row.HexByte(3); DelayMask = row.HexByte(4);
        DeleteAddress = row.HexWord(5);
    }
}
