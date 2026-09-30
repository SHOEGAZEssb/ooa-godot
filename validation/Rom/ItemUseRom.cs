using System;
using System.IO;

namespace oracleofages;

// Clean-US bank $06 checkUseItems/checkItemUsed/chooseParentItemSlot.
// Allocation stops at the original parent-update boundary: no ROM instruction
// is replaced. UpdateParents separately executes the original parent pass.
internal sealed class ItemUseRom
{
    private readonly ReadOnlyMemory<byte> _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly OracleCpu _cpu;
    private byte[] _caller = [];
    private bool _observeAllocation;
    private int _bank = 6;
    private sealed class AllocationComplete : Exception { }

    internal ItemUseRom()
    {
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Item-use ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }
    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal (int Usage, bool JustPressed) Usage(int item) =>
        (_rom.Span[0x195be + item * 2], _rom.Span[0x195bf + item * 2] == 0x2a);
    internal void Reset(ReadOnlySpan<ParentItemSlotState> slots)
    {
        Array.Clear(_memory);
        _bank = 6;
        _memory[0xff97] = 6;
        for (int i = 0; i < slots.Length; i++)
        {
            _memory[0xd200 + i * 0x100] = slots[i].Enabled;
            _memory[0xd201 + i * 0x100] = slots[i].Id;
        }
        _memory[0xc6cb] = 0xff; // No punching ring.
        _memory[0xffb5] = 0xa0; // Sound request queue.
    }
    internal int Choose(int item, int usage)
    {
        _caller = [0x0e, (byte)usage, 0x1e, (byte)item, 0xcd, 0x8c, 0x49,
            0xf5, 0xc1, 0x79, 0xea, 0x00, 0xc0, // Preserve returned flags.
            0x7c, 0xea, 0x01, 0xc0, 0xc9];
        _cpu.RunCall(0xc100);
        return (_memory[0xc000] & 0x80) != 0 ? _memory[0xc001] - 0xd0 : -1;
    }
    internal void Allocate(int a, int b, int held, int pressed)
    {
        this[0xc689] = (byte)a; this[0xc688] = (byte)b;
        this[0xcc29] = (byte)held; this[0xcc2a] = (byte)pressed;
        _observeAllocation = true;
        try
        {
            _cpu.RunCall(0x48b3);
            throw new InvalidDataException("Item-use ROM returned before its allocation boundary.");
        }
        catch (AllocationComplete) { }
        finally { _observeAllocation = false; }
    }
    internal void UpdateParents() => _cpu.RunCall(0x4922);
    private int Read(int address)
    {
        if (_observeAllocation && _bank == 6 && address == 0x4922 && _cpu.InstructionAddress == address)
            throw new AllocationComplete();
        if (address < 0x4000) return _rom.Span[address];
        if (address < 0x8000) return _rom.Span[_bank * 0x4000 + address - 0x4000];
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (Allowed(address)) return _memory[address];
        throw new InvalidDataException($"Item-use ROM ${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }
    private void Write(int address, int value)
    {
        if (address == 0x2222 && value < 0x40) { _bank = value; return; }
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"Item-use ROM ${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }
    private static bool Allowed(int address) => address is >= 0xc000 and < 0xe000 or >= 0xff80 and < 0xffc0;
}
