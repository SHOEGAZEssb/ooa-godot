using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NuunBridgeDatabase
{
    internal IReadOnlyList<NuunBridgeCommand> Commands { get; }

    internal NuunBridgeDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/nuun_bridge_commands.tsv",
            new GeneratedTableSchema("Nuun bridge simple script", GeneratedTableKeySemantics.Unique,
                ["order", "opcode", "a", "b", "c", "d", "source"], ["order"], headerRequired: true));
        var commands = new List<NuunBridgeCommand>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (row.UnsignedDecimal(0) != commands.Count)
                throw row.Invalid(0, "contiguous simple-script command order");
            commands.Add(new NuunBridgeCommand(row.Decimal(1, 0, 4),
                row.Decimal(2, 0, 255), row.Decimal(3, 0, 255),
                row.Decimal(4, 0, 255), row.Decimal(5, 0, 3)));
            row.RequiredString(6);
        }
        if (commands.Count != 20 || commands[^1].Opcode != 0)
            throw new InvalidOperationException("interaction6b_bridgeToNuunSimpleScript requires 20 commands ending in ss_end.");
        Commands = commands;
    }
}

internal readonly record struct NuunBridgeCommand(int Opcode, int A, int B, int C, int D);
