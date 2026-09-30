using System;
using System.IO;

namespace oracleofages;

// Executes clean-US bank1 screenTransitionState3/4/5, including row queues,
// unique graphics streams and finishScrollingTransition. VBlank uploads are
// queued with LCD enabled; hardware transfer time and pixels are not compared.
internal sealed class ScrollRom
{
    private readonly ReadOnlyMemory<byte> _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly byte[][] _wram = [new byte[0x1000], new byte[0x1000], new byte[0x1000], new byte[0x1000],
        new byte[0x1000], new byte[0x1000], new byte[0x1000], new byte[0x1000]];
    private readonly OracleCpu _cpu;
    private int _bank = 1;
    // Use unbanked scratch for the native stack while row copies select WRAM3.
    private readonly byte[] _caller = [0x31, 0xf0, 0xcf, 0xcd, 0x0b, 0x40, 0x31, 0xf0, 0xdf, 0xc9];
    internal ScrollRom(bool large, int direction, int x, int y, int unique = 0, int loadedUnique = 0)
    {
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Scroll ROM ${_bank:x2}:${pc:x4}: {detail}."));
        this[0xff97] = 1; this[0xff40] = 0x80;
        this[0xcc2c] = 0xd0; this[0xcc39] = 0xff;
        this[0xcd00] = 8; this[0xcd01] = (byte)(large ? 0 : 1);
        this[0xcd02] = (byte)direction; this[0xcd04] = 3;
        this[0xcd0a] = (byte)(large ? 15 : 10); this[0xcd0b] = (byte)(large ? 11 : 8);
        // Native names are transposed: MaxCameraY limits horizontal camera.
        this[0xcd0e] = (byte)(large ? 80 : 0); this[0xcd0f] = (byte)(large ? 48 : 0);
        this[0xcd20] = (byte)unique; this[0xcd28] = (byte)loadedUnique;
        this[0xd000] = 1; this[0xd004] = 1; this[0xd008] = 2;
        Word(0xd00a, y); Word(0xd00c, x);
    }
    internal byte this[int address]
    {
        get => address is >= 0xd000 and < 0xe000 ? _wram[1][address - 0xd000] : _memory[address];
        set { if (address is >= 0xd000 and < 0xe000) _wram[1][address - 0xd000] = value; else _memory[address] = value; }
    }
    internal int Word(int address) => this[address] | this[address + 1] << 8;
    internal void Word(int address, int value) { this[address] = (byte)value; this[address + 1] = (byte)(value >> 8); }
    internal void Update()
    {
        if (this[0xcd04] == 2) throw new InvalidOperationException("Scroll ROM already finished.");
        // The preceding VBlank consumed the queue. The reference observes
        // gameplay state, not the resulting VRAM image or interrupt timing.
        this[0xffa5] = 0;
        _cpu.RunCall(0xc100);
        if (_bank != 1 || this[0xff70] != 0)
            throw new InvalidDataException("Scroll ROM failed to restore ROM/WRAM bank ownership.");
    }
    private int Read(int address)
    {
        if (address < 0x4000) return _rom.Span[address];
        if (address < 0x8000) return _rom.Span[_bank * 0x4000 + address - 0x4000];
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (address is >= 0xd000 and < 0xe000) return _wram[Math.Max(1, _memory[0xff70] & 7)][address - 0xd000];
        if (Allowed(address)) return _memory[address];
        throw new InvalidDataException($"Scroll ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }
    private void Write(int address, int value)
    {
        if (address == 0x2222 && value < 0x40) { _bank = value; return; }
        if (address is >= 0xd000 and < 0xe000) { _wram[Math.Max(1, _memory[0xff70] & 7)][address - 0xd000] = (byte)value; return; }
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"Scroll ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }
    private static bool Allowed(int address) => address is >= 0xc000 and < 0xd000 or >= 0xff80 and < 0xffc0 or 0xff40 or 0xff4f or 0xff70;
}
