using Godot;
using System;

namespace oracleofages;

internal sealed class SmogWallDatabase
{
    internal Vector2I[] FrontOffsets { get; } = new Vector2I[4];
    internal Vector2I[] ProbeOffsets { get; } = new Vector2I[8];
    internal int[] Speeds { get; } = new int[5];

    internal SmogWallDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/enemies/smog_wall.tsv",
            new GeneratedTableSchema("ENEMY_SMOG $7c wall movement", GeneratedTableKeySemantics.Unique,
                ["profile", "index", "value", "source"], ["profile", "index"], headerRequired: true));
        if (table.Rows.Count != 29) throw new InvalidOperationException("Smog wall tables require 29 bytes.");
        int cursor = 0;
        byte[] Read(string profile, int count)
        {
            var values = new byte[count];
            for (int i = 0; i < count; i++)
            {
                var row = table.Rows[cursor++];
                if (row.RequiredString(0) != profile || row.UnsignedDecimal(1) != i)
                    throw row.Invalid(0, $"ordered {profile} byte {i}");
                values[i] = (byte)row.HexByte(2);
            }
            return values;
        }
        void Vectors(string profile, Vector2I[] target)
        {
            var bytes = Read(profile, target.Length * 2);
            for (int i = 0; i < target.Length; i++)
                target[i] = new(unchecked((sbyte)bytes[i * 2 + 1]), unchecked((sbyte)bytes[i * 2]));
        }
        Vectors("front-offsets", FrontOffsets);
        Vectors("wall-probes", ProbeOffsets);
        var speeds = Read("speeds", Speeds.Length);
        for (int i = 0; i < speeds.Length; i++) Speeds[i] = speeds[i];
    }
}
