using Godot;
using System;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateItemSlotAllocationRom()
    {
        var rom = new ItemUseRom();
        var usage = new ParentItemUsageDatabase();
        int cases = 0;
        for (int item = 0; item < 32; item++)
        {
            var native = rom.Usage(item);
            FailIf(usage.Item(item).Usage != native.Usage || usage.Item(item).JustPressed != native.JustPressed,
                $"ROM item ${item:x2}: imported usage/input differs from the native table.");
            for (int enabled = 0; enabled < 256; enabled++)
            for (int occupied = 0; occupied < 8; occupied++)
            for (int duplicate = 0; duplicate < 3; duplicate++)
            {
                ParentItemSlotState[] slots = [new((byte)enabled, 5),
                    new((byte)(occupied & 1), (byte)(duplicate == 1 ? item : 0xff)),
                    new((byte)(occupied & 2), (byte)(duplicate == 2 ? item : 0xff)),
                    new((byte)(occupied & 4), 1)];
                rom.Reset(slots);
                int expected = rom.Choose(item, native.Usage);
                int actual = usage.ChooseSlot(item, slots);
                FailIf(actual != expected,
                    $"ROM slot ITEM${item:x2}, priority=${enabled:x2}, occupied={occupied}, duplicate={duplicate}: ROM={expected}, runtime={actual}.");
                cases++;
            }
        }
        GD.Print($"Validated {cases} executed-ROM parent slot decisions and all $20 imported usage/input rows.");
    }

    private void ValidateItemButtonDispatchRom()
    {
        var rom = new ItemUseRom();
        var usage = new ParentItemUsageDatabase();
        int cases = 0;
        // Compare sequential A/B allocation with the runtime's imported input
        // policy and slot allocator. This observes allocation, not parent updates.
        for (int a = 0; a < 32; a++)
        for (int b = 0; b < 32; b++)
        for (int held = 0; held < 4; held++)
        for (int pressed = 0; pressed < 4; pressed++)
        {
            ParentItemSlotState[] slots = [new(), new(), new(), new()];
            int[] buttons = new int[4];
            rom.Reset(slots);
            rom.Allocate(a, b, held, pressed);
            foreach (var input in new[] { (Item: a, Button: 1), (Item: b, Button: 2) })
            {
                var policy = usage.Item(input.Item);
                if (input.Item == 0 || ((policy.JustPressed ? pressed : held) & input.Button) == 0) continue;
                int slot = usage.ChooseSlot(input.Item, slots);
                if (slot < 0) continue;
                slots[slot - 2] = new((byte)policy.Enabled, (byte)input.Item);
                buttons[slot - 2] = input.Button;
            }
            for (int slot = 0; slot < 4; slot++)
            {
                int address = 0xd200 + slot * 0x100;
                FailIf(rom[address] != slots[slot].Enabled || rom[address + 1] != slots[slot].Id || rom[address + 3] != buttons[slot],
                    $"ROM A=${a:x2}, B=${b:x2}, held={held}, edge={pressed}, parent{slot + 2}: allocation mismatch.");
            }
            cases++;
        }
        // checkUseItems clears the shield signal, ticks the sword counter and
        // preserves the low use bits even when allocation is suppressed.
        foreach (var gate in new (int Address, byte Value)[]
        {
            (0xcc63, 0x80), (0xcc95, 0x80), (0xcc5c, 0x80),
            (0xccd8, 1), (0xcc5a, 1), (0xcc68, 0xff), (0xcc5d, 1)
        })
        {
            rom.Reset([]); rom[gate.Address] = gate.Value;
            rom[0xcc6f] = 3; rom[0xcc59] = 2; rom[0xcc5f] = 0xff;
            rom.Allocate(5, 6, 3, 3);
            FailIf(rom[0xd200] != 0 || rom[0xd300] != 0 || rom[0xcc6f] != 0 || rom[0xcc59] != 1 || rom[0xcc5f] != 15,
                $"ROM allocation gate ${gate.Address:x4} failed to preserve the shared prelude.");
        }
        foreach (var terrain in new (byte Flags, byte Swimming, byte Var2f, int Buttons)[]
        {
            (0, 0, 0, 3), (0, 1, 0, 0), (0x40, 1, 0, 1),
            (0x20, 0, 0, 3), (0x20, 1, 0, 2), (0x20, 1, 0x80, 3)
        })
        {
            rom.Reset([]); rom[0xcc34] = terrain.Flags; rom[0xcc5d] = terrain.Swimming; rom[0xd02f] = terrain.Var2f;
            rom.Allocate(5, 6, 3, 3);
            FailIf((rom[0xd200] != 0) != ((terrain.Buttons & 1) != 0) ||
                (rom[0xd300] != 0) != ((terrain.Buttons & 2) != 0),
                $"ROM terrain flags=${terrain.Flags:x2}, swimming={terrain.Swimming}, var2f=${terrain.Var2f:x2}: A/B gate differs.");
        }
        GD.Print($"Validated {cases} ROM A/B allocation combinations, shared gates and terrain button masks.");
    }
}
