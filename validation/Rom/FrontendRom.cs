using System;
using System.Collections.Generic;
using System.IO;

namespace oracleofages;

// Original frontend dispatch and graphics/data routines with LCD held off.
// No interrupts, loading latency or serial partner; private VRAM/WRAM/SRAM.
internal sealed class FrontendRom
{
    private readonly ReadOnlyMemory<byte> _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly byte[][] _wram = new byte[8][];
    private readonly byte[][] _vram = [new byte[0x2000], new byte[0x2000]];
    private readonly byte[] _sram = new byte[0x2000];
    private readonly OracleCpu _cpu;
    private int _bank;
    internal bool FileSelectHandoff { get; private set; }
    internal bool GameplayHandoff { get; private set; }
    internal int RandomCalls { get; private set; }
    internal List<int> Sounds { get; } = [];
    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal int Word(int address) => this[address] | this[address + 1] << 8;
    internal byte BankByte(int bank, int address) => _wram[bank][address - 0xd000];
    internal void SetBankByte(int bank, int address, byte value) => _wram[bank][address - 0xd000] = value;
    internal byte NameByte(int offset) => _wram[4][0x7a0 + offset];
    internal byte DisplayHearts(int slot) => _wram[4][0x780 + slot * 8 + 2];
    internal byte SavedByte(int slot, int address, bool backup = false) =>
        _sram[(backup ? 0x1000 : 0x10) + slot * 0x550 + address - 0xc5b0];

    internal void InitializeMenu()
    {
        // fileSelectThreadStart clears the intro/menu union before mode 0.
        Array.Clear(_memory, 0xcbb3, 0x10);
        UpdateMenu(0, 0);
    }

    internal FrontendRom()
    {
        for (int bank = 2; bank < 8; bank++) _wram[bank] = new byte[0x1000];
        this[0xff70] = 0;
        this[0xffb4] = this[0xffb5] = 0xa0;
        this[0xff96] = 1; // CGB, no serial partner
        this[0xff4d] = 0x80; // initialized double-speed CGB
        this[0xcc2c] = 0xd0;
        this[0xcd25] = 0xff;
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Frontend ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }

    internal void UpdateIntro(int pressed)
    {
        // The intro thread resumes with SVBK $00 (the fixed object bank).
        // $00/$01 address the same RAM, but returning A from wave helpers
        // preserves this register value and feeds the native flash counter.
        this[0xff70] = 0;
        this[0xc482] = (byte)pressed;
        this[0xcbb7]++;
        Call(0x4cc9, 3); // bank3Cutscenes.runIntro
        AdvancePalette();
    }

    internal void UpdateMenu(int pressed, int held)
    {
        this[0xc482] = (byte)pressed;
        this[0xc481] = (byte)held;
        this[0xff70] = 0;
        Call(0x412e, 2); // bank2.b2_fileSelectScreen
        AdvancePalette();
    }

    internal void AdvancePalette()
    {
        if (this[0xc4ad] > 1)
            throw new InvalidDataException($"Frontend palette thread has unsupported update rate ${this[0xc4ad]:x2} at $c4ad.");
        this[0xff70] = 2;
        Call(0x56e3, 1); // bank1.paletteFadeHandler
        this[0xff70] = 0;
    }

    internal void Call(int entry, int bank)
    {
        _bank = bank;
        this[0xff97] = (byte)bank;
        // Full-screen native decompression exceeds the ordinary small-call
        // budget. It remains bounded, without converting CPU cost to updates.
        try { _cpu.RunCall(entry, instructionLimit: 1000000, stackAddress: 0xc2dc); }
        catch (OperationCanceledException) when (FileSelectHandoff || GameplayHandoff) { }
    }

    private int Read(int address)
    {
        if (address == _cpu.InstructionAddress && address == 0x08e2)
        {
            // intro_titlescreen_state3 has already restarted THREAD_1 before
            // entering its stub. Observe the thread boundary; do not run the
            // whole cooperative main-loop scheduler inside this bounded call.
            FileSelectHandoff = true;
            throw new OperationCanceledException();
        }
        if (address == _cpu.InstructionAddress && address == 0x08c3)
        {
            // restartThisThread: fileSelectMode1 has completed its fade and
            // selected mainThreadStart. Stop before entering the scheduler.
            GameplayHandoff = true;
            throw new OperationCanceledException();
        }
        if (address < 0x4000) return _rom.Span[address];
        if (address < 0x8000) return _rom.Span[_bank * 0x4000 + address - 0x4000];
        if (address < 0xa000) return _vram[this[0xff4f] & 1][address - 0x8000];
        if (address < 0xc000) return _sram[address - 0xa000];
        if (address is >= 0xd000 and < 0xe000 && (this[0xff70] & 7) > 1)
            return _wram[this[0xff70] & 7][address - 0xd000];
        if (address == 0xff40) return 0;
        if (address == 0xff44) return 0x91;
        if (Allowed(address)) return this[address];
        throw new InvalidDataException($"Frontend ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }

    private void Write(int address, int value)
    {
        if (address == 0x2222) { _bank = value & 0x3f; return; }
        if (address < 0x8000) return; // MBC SRAM enable/bank writes; one RAM bank
        if (address < 0xa000) { _vram[this[0xff4f] & 1][address - 0x8000] = (byte)value; return; }
        if (address < 0xc000) { _sram[address - 0xa000] = (byte)value; return; }
        if (address == 0xff94) RandomCalls++;
        if (address is >= 0xc0a0 and <= 0xc0af && value != 0) Sounds.Add(value);
        if (address is >= 0xd000 and < 0xe000 && (this[0xff70] & 7) > 1)
        { _wram[this[0xff70] & 7][address - 0xd000] = (byte)value; return; }
        if (Allowed(address)) { this[address] = (byte)value; return; }
        throw new InvalidDataException($"Frontend ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }

    private static bool Allowed(int address) => address is >= 0xc000 and < 0xe000 or >= 0xff80 or
        >= 0xff01 and <= 0xff07 or >= 0xff10 and <= 0xff3f or 0xff0f or 0xff40 or 0xff4d or 0xff4f or 0xff51 or 0xff52 or 0xff53 or 0xff54 or 0xff55 or 0xff70;
}
