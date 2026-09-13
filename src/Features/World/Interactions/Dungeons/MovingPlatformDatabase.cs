using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MovingPlatformDatabase
{
    private readonly Dictionary<(int Dungeon, int Script), MovingPlatformScript> _scripts = new();

    internal MovingPlatformDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/moving_platform_scripts.tsv",
            new GeneratedTableSchema("moving platform mini-scripts", GeneratedTableKeySemantics.Unique,
                ["dungeon", "script", "commands", "source"], ["dungeon", "script"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            var commands = Array.ConvertAll(row.RequiredString(2).Split(','), encoded =>
            {
                string[] fields = encoded.Split(':');
                if (fields.Length != 2) throw row.Invalid(2, "opcode:operand commands");
                return new MovingPlatformCommand(Convert.ToByte(fields[0], 16), Convert.ToByte(fields[1], 16));
            });
            foreach (var command in commands)
                if (command.Opcode is not (0x00 or 0x04 or 0x08 or 0x09 or 0x0a or 0x0b) ||
                    command.Opcode == 4 && command.Operand >= commands.Length)
                    throw row.Invalid(2, "supported source commands and valid jump destinations");
            _scripts.Add((row.HexByte(0), row.HexByte(1)), new MovingPlatformScript(commands, row.RequiredString(3)));
        }
        if (_scripts.Count != 22) throw new InvalidOperationException("Incomplete moving-platform dungeon aliases/scripts.");
    }

    internal MovingPlatformScript Script(int dungeon, int index) => _scripts.TryGetValue((dungeon, index), out var script) ? script
        : throw new InvalidOperationException($"No imported moving-platform script ${index:x2} for dungeon ${dungeon:x2}.");
}

internal readonly record struct MovingPlatformCommand(int Opcode, int Operand);
internal readonly record struct MovingPlatformScript(IReadOnlyList<MovingPlatformCommand> Commands, string Source);
