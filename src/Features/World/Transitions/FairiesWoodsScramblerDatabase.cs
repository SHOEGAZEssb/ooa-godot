using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported bank1.screenTransitionForestScrambler destinations. A zero entry
/// delegates to the standard room-neighbor calculation.
/// </summary>
internal sealed class FairiesWoodsScramblerDatabase
{
    private readonly Dictionary<int, int[]> _destinations = new();

    internal FairiesWoodsScramblerDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/fairies_woods_scrambler.tsv",
            new GeneratedTableSchema(
                "Fairies' Woods screen scrambler",
                GeneratedTableKeySemantics.Unique,
                ["room", "up", "right", "down", "left", "source"],
                ["room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _destinations.Add(
                row.HexByte(0),
                [row.HexByte(1), row.HexByte(2), row.HexByte(3), row.HexByte(4)]);
            _ = row.RequiredString(5);
        }
        Validate();
    }

    internal bool TryResolve(int room, Vector2I direction, out int destination)
    {
        destination = 0;
        if (!_destinations.TryGetValue(room, out int[]? values))
            return false;
        int index = direction == Vector2I.Up ? 0
            : direction == Vector2I.Right ? 1
            : direction == Vector2I.Down ? 2
            : direction == Vector2I.Left ? 3
            : throw new ArgumentOutOfRangeException(nameof(direction));
        destination = values[index];
        return destination != 0;
    }

    private void Validate()
    {
        if (_destinations.Count != 9 ||
            !TryResolve(0x70, Vector2I.Right, out int room70Right) ||
            room70Right != 0x71 ||
            !TryResolve(0x82, Vector2I.Up, out int room82Up) ||
            room82Up != 0x70 ||
            !TryResolve(0x91, Vector2I.Left, out int room91Left) ||
            room91Left != 0x92 ||
            TryResolve(0x92, Vector2I.Right, out _))
        {
            throw new InvalidOperationException(
                "Fairies' Woods scrambler data diverges from bank1.s.");
        }
    }
}
