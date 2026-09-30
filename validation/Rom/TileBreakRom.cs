using System;
using System.IO;

namespace oracleofages;

// Original tryToBreakTile and decideItemDrop, including native allocations,
// save writes and the changed-tile queue. WRAM3 keeps its own underlying layout.
internal sealed class TileBreakRom
{
    private readonly byte[] _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly byte[][] _wram = [new byte[0x1000], new byte[0x1000], new byte[0x1000], new byte[0x1000]];
    private readonly OracleCpu _cpu;
    private byte[] _caller = [];
    private int _bank = 6;
    internal int RandomCalls { get; private set; }
    internal TileBreakRom()
    {
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Tile-break ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }
    internal byte this[int address]
    {
        get => address is >= 0xd000 and < 0xe000 ? _wram[1][address - 0xd000] : _memory[address];
        set { if (address is >= 0xd000 and < 0xe000) _wram[1][address - 0xd000] = value; else _memory[address] = value; }
    }
    internal void Reset(OracleSaveData save, int seed = 0x1234)
    {
        Array.Clear(_memory); foreach (byte[] bank in _wram) Array.Clear(bank);
        save.ReadWramBytes(0xc5b0, _memory.AsSpan(0xc5b0, OracleSaveData.FileSize));
        _bank = 6; this[0xff97] = 6; this[0xffb5] = 0xa0;
        this[0xff94] = (byte)seed; this[0xff95] = (byte)(seed >> 8);
        this[0xccaa] = 0xff; this[0xcc39] = 0xff; this[0xd008] = 2;
        RandomCalls = 0;
    }
    internal void SetTile(int collisionSet, int tile, int underlying = 0x3a, OracleRoomData? room = null)
    {
        this[0xcc33] = (byte)collisionSet; this[0xcf44] = (byte)tile;
        _wram[3][0xf44] = (byte)underlying;
        if (room is not null)
            for (int i = 0; i < 256; i++) _wram[3][0xb00 + i] = room.GetCollision((byte)i);
        this[0xce44] = _wram[3][0xb00 + tile];
    }
    internal byte Underlying => _wram[3][0xf44];
    internal bool Break(int source, bool query = false)
    {
        Call(0x2bf6, source | (query ? 0x80 : 0), 0x48, 0x48);
        return (this[0xc201] & 0x10) != 0;
    }
    internal int? DecideDrop(int index)
    {
        this[0xffae] = 0xc0; this[0xd0c2] = (byte)index;
        Call(0x16eb, index >= 0x81 ? index & 0x7f : 0, 0, 0);
        return this[0xc200] == 0xff ? null : this[0xc200];
    }
    private void Call(int entry, int a, int b, int c)
    {
        _caller = [0x31, 0xf0, 0xcf, 0x16, 0xd0, 0x3e, (byte)a, 0x06, (byte)b, 0x0e, (byte)c,
            0xcd, (byte)entry, (byte)(entry >> 8), 0xf5, 0xc1,
            0x78, 0xea, 0x00, 0xc2, 0x79, 0xea, 0x01, 0xc2,
            0x31, 0xf0, 0xdf, 0xc9];
        _cpu.RunCall(0xc100);
        if (_bank != 6 || this[0xff70] != 0) throw new InvalidDataException("Tile-break ROM lost bank restoration.");
    }
    private int Read(int address)
    {
        if (address < 0x4000) return _rom[address];
        if (address < 0x8000) return _rom[_bank * 0x4000 + address - 0x4000];
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (address is >= 0xd000 and < 0xe000) return _wram[Math.Max(1, (int)_memory[0xff70])][address - 0xd000];
        if (Allowed(address)) return _memory[address];
        throw new InvalidDataException($"Tile-break ROM ${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }
    private void Write(int address, int value)
    {
        if (address == 0x2222 && value < 0x40) { _bank = value; return; }
        if (address == 0xff94) RandomCalls++;
        if (address is >= 0xd000 and < 0xe000) { _wram[Math.Max(1, (int)_memory[0xff70])][address - 0xd000] = (byte)value; return; }
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"Tile-break ROM ${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }
    private static bool Allowed(int address) => address is >= 0xc000 and < 0xd000 or >= 0xff80 and < 0xffc0 or 0xff70;
}
