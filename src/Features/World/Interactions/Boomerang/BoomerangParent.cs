using System;
using System.Collections.Generic;

namespace oracleofages;

// ITEM$06's parent owns only the throw pose and movement/turning lock. The
// dynamic child keeps its own lifetime, including the hidden catch delay.
internal sealed class BoomerangParent
{
    private readonly Dictionary<int, List<Frame>> _frames = new();
    private int _index;
    private int _counter;
    internal bool Active { get; private set; }
    internal int Slot { get; private set; }
    internal int Mode { get; private set; }
    internal int Graphic => Active ? _frames[Mode][_index].Graphic : 0;
    internal int Parameter => Active ? _frames[Mode][_index].Parameter : 0;
    internal int Counter => _counter;

    internal BoomerangParent()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/boomerang_parent_animations.tsv",
            new GeneratedTableSchema("boomerangParent.s animations", GeneratedTableKeySemantics.Unique,
                ["mode", "frame", "duration", "graphic", "parameter", "source"],
                ["mode", "frame"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int mode = row.HexByte(0);
            if (mode is not (0x21 or 0x25)) throw row.Invalid(0, "Boomerang modes$21/$25");
            if (!_frames.TryGetValue(mode, out var frames)) _frames.Add(mode, frames = []);
            if (row.Decimal(1) != frames.Count) throw row.Invalid(1, "ordered frames");
            frames.Add(new(row.Decimal(2, 1, 255), row.HexByte(3), row.HexByte(4)));
            _ = row.RequiredString(5);
        }
        foreach (int mode in new[] { 0x21, 0x25 })
            if (!_frames.TryGetValue(mode, out var frames) || frames.Count != 2 ||
                (frames[0].Parameter & 0x80) != 0 || (frames[1].Parameter & 0x80) == 0)
                throw new InvalidOperationException($"Boomerang mode${mode:x2} has incomplete parent animation.");
    }

    internal void Begin(int slot, bool mounted, bool raft)
    {
        if (slot is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(slot));
        Slot = slot;
        Mode = mounted && !raft ? 0x25 : 0x21;
        _index = 0;
        _counter = _frames[Mode][0].Duration;
        Active = true;
    }

    internal void Update()
    {
        if (!Active) return;
        // state1 tests the parameter before specialObjectAnimate_optimized.
        // The terminal pose therefore retains the lock for one update.
        if ((Parameter & 0x80) != 0) { Clear(); return; }
        if (--_counter == 0) _counter = _frames[Mode][++_index].Duration;
    }

    internal void Clear()
    {
        Active = false;
        Slot = 0;
        _counter = 0;
    }

    private readonly record struct Frame(int Duration, int Graphic, int Parameter);
}
