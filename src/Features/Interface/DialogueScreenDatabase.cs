using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class DialogueScreenDatabase
{
    private readonly Dictionary<int, byte> _scrollY = new();

    internal DialogueScreenDatabase()
    {
        foreach (GeneratedTableRow row in GeneratedTable.Load(
            "res://assets/oracle/menu/dialogue_screen_registers.tsv",
            new GeneratedTableSchema("Native dialogue screen registers",
                GeneratedTableKeySemantics.Ordered,
                ["state", "scroll-y", "source"], headerRequired: true)).Rows)
        {
            if (!_scrollY.TryAdd(row.HexByte(0), (byte)row.HexByte(1)))
                throw new InvalidOperationException("Duplicate dialogue gfx register state.");
        }
    }

    internal DialogueScreenContext ClearedLink(int gfxState, int screenOffsetY = 0)
    {
        if (!_scrollY.TryGetValue(gfxState, out byte scrollY))
            throw new InvalidOperationException(
                $"Dialogue screen lacks gfxRegisterStates ${gfxState:x2}.");
        return new(0, 0, checked((byte)screenOffsetY), scrollY);
    }
}
