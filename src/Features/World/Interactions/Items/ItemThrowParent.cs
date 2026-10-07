using System;
using System.Collections.Generic;

namespace oracleofages;

// parentItemGenericState1 owns only the throw pose and movement/turning lock.
// Physical children retain their independent lifetime after the parent clears.
internal sealed class ItemThrowParent
{
    private readonly Dictionary<int, List<Frame>> _frames = new();
    private int _index;
    private int _counter;
    internal bool Active { get; private set; }
    internal bool Pending { get; private set; }
    internal int Slot { get; private set; }
    internal int Mode { get; private set; }
    internal int Graphic => Active && !Pending ? _frames[Mode][_index].Graphic : 0;
    internal int Parameter => Active && !Pending ? _frames[Mode][_index].Parameter : 0;
    internal int Counter => _counter;

    internal ItemThrowParent()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/item_throw_parent_animations.tsv",
            new GeneratedTableSchema("parentItemGenericState1 animations", GeneratedTableKeySemantics.Unique,
                ["mode", "frame", "duration", "graphic", "parameter", "source"],
                ["mode", "frame"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int mode = row.HexByte(0);
            if (mode is not (0x21 or 0x25)) throw row.Invalid(0, "throw modes$21/$25");
            if (!_frames.TryGetValue(mode, out var frames)) _frames.Add(mode, frames = []);
            if (row.Decimal(1) != frames.Count) throw row.Invalid(1, "ordered frames");
            frames.Add(new(row.Decimal(2, 1, 255), row.HexByte(3), row.HexByte(4)));
            _ = row.RequiredString(5);
        }
        foreach (int mode in new[] { 0x21, 0x25 })
            if (!_frames.TryGetValue(mode, out var frames) || frames.Count != 2 ||
                (frames[0].Parameter & 0x80) != 0 || (frames[1].Parameter & 0x80) == 0)
                throw new InvalidOperationException($"Throw mode${mode:x2} has incomplete parent animation.");
    }

    internal void Begin(int slot, bool mounted, bool raft)
    {
        if (slot is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(slot));
        Slot = slot;
        Mode = mounted && !raft ? 0x25 : 0x21;
        _index = 0;
        _counter = _frames[Mode][0].Duration;
        Active = true;
        Pending = false;
    }

    internal void Reserve(int slot)
    {
        if (slot is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(slot));
        Slot = slot; Active = Pending = true;
    }

    internal void Update()
    {
        if (!Active || Pending) return;
        // state1 tests the parameter before specialObjectAnimate_optimized.
        // The terminal pose therefore retains the lock for one update.
        if ((Parameter & 0x80) != 0) { Clear(); return; }
        if (--_counter == 0) _counter = _frames[Mode][++_index].Duration;
    }

    internal void Clear()
    {
        Active = false;
        Pending = false;
        Slot = 0;
        _counter = 0;
    }

    private readonly record struct Frame(int Duration, int Graphic, int Parameter);
}
