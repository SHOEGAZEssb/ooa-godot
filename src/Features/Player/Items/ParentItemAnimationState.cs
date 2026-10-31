using System;

namespace Oracles;

// func_4553 scans animation claims independently of parent enabled/lifetime.
internal static class ParentItemAnimationState
{
    internal static void Begin(OracleRuntimeState memory, int slot, int id, int priority)
    {
        int address = Address(slot);
        memory.SetWramByte(address | 1, checked((byte)id));
        memory.SetWramByte(address | 0x3f, checked((byte)priority));
        // Sword's parentItemLoadAnimationAndIncState locks turning; state6
        // enables movement without releasing this native parent-slot bit.
        if (id == ItemId.Sword)
            memory.SetField("wLinkTurningDisabled", (byte)(memory.ReadField("wLinkTurningDisabled") | (1 << (slot - 2))));
    }

    internal static void Clear(OracleRuntimeState memory, int slot, int id)
    {
        int address = Address(slot);
        // A replaced parent's later cleanup cannot retire its successor.
        if (memory.ReadWramByte(address | 1) != id) return;
        memory.SetWramByte(address | 1, 0);
        memory.SetWramByte(address | 0x3f, 0);
        ParentItemButtons.Set(memory, slot, 0);
        // clearParentItemH releases its slot without clearing other parents
        // or the A-sensitive interaction's separate bit7 publication.
        memory.SetField("wLinkTurningDisabled", (byte)(memory.ReadField("wLinkTurningDisabled") & ~(1 << (slot - 2))));
    }

    internal static int SelectPage(OracleRuntimeState memory)
    {
        int priority = 0, page = 0xd2;
        for (int candidate = 0xd2; candidate < 0xd6; candidate++)
        {
            int value = memory.ReadWramByte((candidate << 8) | 0x3f);
            if (value < priority) continue;
            priority = value;
            page = candidate;
        }
        return memory.ReadWramByte(0xd03f) >= priority ? 0xd0 : page;
    }

    internal static void SetPriority(OracleRuntimeState memory, int slot, int id, int priority)
    {
        int address = Address(slot);
        if (memory.ReadWramByte(address | 1) == id)
            memory.SetWramByte(address | 0x3f, checked((byte)priority));
    }

    private static int Address(int slot) => slot is >= 2 and <= 5 ? (0xd0 + slot) << 8
        : throw new ArgumentOutOfRangeException(nameof(slot), "Native parent slot must be $d2-$d5.");
}
