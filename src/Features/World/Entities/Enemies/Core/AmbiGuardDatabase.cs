using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class AmbiGuardDatabase
{
    private readonly Dictionary<int, AmbiGuardPatrol> _patrols = new();
    private readonly Dictionary<(int Mode, int Collision), (bool Enabled, int Effect)> _collisions = new();
    internal (bool Enabled, int Effect) Collision(int mode, int collision) => _collisions[(mode, collision)];
    internal AmbiGuardDatabase()
    {
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/ambi_guard_collisions.tsv",
            new GeneratedTableSchema("Ambi guard collisions", GeneratedTableKeySemantics.Unique,
                ["mode", "collision", "enabled", "effect", "source"], ["mode", "collision"], headerRequired: true)).Rows)
            _collisions.Add((row.HexByte(0), row.HexByte(1)), (row.Decimal(2, 0, 1) != 0, row.HexByte(3)));
        if (_collisions.Count != 64) throw new InvalidOperationException("ENEMY_AMBI_GUARD $54 requires both native collision modes.");
        var commands = new Dictionary<int, List<AmbiGuardMovementCommand>>();
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/ambi_guard_movement_commands.tsv",
            new GeneratedTableSchema("Ambi guard movement scripts", GeneratedTableKeySemantics.Unique,
                ["subid", "index", "opcode", "arg0", "arg1", "source"], ["subid", "index"], headerRequired: true)).Rows)
        {
            int subid = row.HexByte(0);
            if (!commands.TryGetValue(subid, out var list)) commands.Add(subid, list = new());
            if (row.Decimal(1) != list.Count) throw row.Invalid(1, "source-ordered contiguous script indices");
            string opcode = row.RequiredString(2);
            if (opcode is not ("up" or "right" or "down" or "left" or "wait" or "state" or "loop"))
                throw row.Invalid(2, "a native movement-script opcode");
            int arg1 = opcode == "state" ? row.HexByte(4) : 0;
            if (opcode == "state" && arg1 != 0x0d)
                throw row.Invalid(4, "ambiGuard.s turn state $0d");
            list.Add(new(opcode, row.HexByte(3), arg1, row.RequiredString(5)));
        }
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/ambi_guard_patrols.tsv",
            new GeneratedTableSchema("Ambi guard patrols", GeneratedTableKeySemantics.Unique,
                ["subid", "speed", "direction", "source"], ["subid"], headerRequired: true)).Rows)
        {
            int subid = row.HexByte(0);
            if (!commands.TryGetValue(subid, out var list) || list.Count == 0 || list[^1].Opcode != "loop")
                throw row.Invalid(0, "a terminated native patrol stream");
            _patrols.Add(subid, new(row.HexByte(1), row.Decimal(2, 0, 3), list.ToArray(), row.RequiredString(3)));
        }
        if (_patrols.Count != 26 || commands.Count != 26)
            throw new InvalidOperationException("ambiGuard.s: all $00-$0c/$80-$8c patrols must be imported.");
    }
    internal AmbiGuardPatrol Require(int subid) => _patrols.TryGetValue(subid, out var patrol) ? patrol
        : throw new InvalidOperationException($"ambiGuard.s:$54:${subid:x2}: no native movement script.");
}

internal sealed record AmbiGuardPatrol(int Speed, int InitialDirection, AmbiGuardMovementCommand[] Commands, string Source);
internal sealed record AmbiGuardMovementCommand(string Opcode, int Arg0, int Arg1, string Source);
