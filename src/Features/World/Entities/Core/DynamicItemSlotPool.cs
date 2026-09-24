using System;
using System.Collections.Generic;

namespace oracleofages;

// getFreeItemSlot scans $d7-$db. Reserved $d6/$dc-$df never participate.
// A liveness predicate represents itemDelete without waiting for scene cleanup.
internal sealed class DynamicItemSlotPool
{
    private readonly Entry?[] _slots = new Entry[5];
    private Entry? Live(int index)
    {
        Entry? entry=_slots[index];
        if(entry is not null && !entry.IsLive()) _slots[index]=entry=null;
        return entry;
    }
    internal int FindFree()
    {
        for(int i=0;i<_slots.Length;i++) if(Live(i) is null) return i+0xd7;
        return -1;
    }
    internal int TryAllocate(object owner,int itemId,Func<bool> isLive)
    {
        if(itemId is <0 or >255) throw new ArgumentOutOfRangeException(nameof(itemId));
        for(int i=0;i<_slots.Length;i++)
            if(ReferenceEquals(Live(i)?.Owner,owner))
                throw new InvalidOperationException($"ITEM${itemId:x2} already owns slot${i+0xd7:x2}.");
        int slot=FindFree();
        if(slot>=0) _slots[slot-0xd7]=new(owner,itemId,isLive);
        return slot;
    }
    internal object? FindItem(int itemId,int afterSlot=0xd6)
    {
        for(int i=0;i<_slots.Length;i++)
            if(i+0xd7>afterSlot && Live(i) is Entry entry && entry.ItemId==itemId) return entry.Owner;
        return null;
    }
    internal int SlotOf(object owner)
    {
        for(int i=0;i<_slots.Length;i++) if(ReferenceEquals(Live(i)?.Owner,owner)) return i+0xd7;
        return -1;
    }
    internal void Clear() => Array.Clear(_slots);
    internal object? OwnerAt(int slot) => slot is >= 0xd7 and <= 0xdb ? Live(slot - 0xd7)?.Owner : null;
    // Read each slot when reached, not a snapshot: a later allocation runs
    // now; replacement in an already visited slot waits for the next pass.
    internal IEnumerable<object> LiveOwners()
    {
        for(int i=0;i<_slots.Length;i++)
            if(Live(i) is Entry entry) yield return entry.Owner;
    }
    private sealed record Entry(object Owner,int ItemId,Func<bool> IsLive);
}
