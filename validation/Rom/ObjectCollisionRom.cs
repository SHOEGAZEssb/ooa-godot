using System;
using System.Collections.Generic;
using System.IO;

namespace oracleofages;

// Original geometry and bank-$07 collision scan, including native effect handlers.
// The wrapper sets registers/captures flags; the read observer only records dispatch.
internal sealed class ObjectCollisionRom
{
    internal const int Overlap = 0x1d5a;
    internal const int LinkOverlap = 0x1c41;
    internal const int Scan = 0x41d1;
    private readonly byte[] _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly OracleCpu _cpu;
    private byte[] _caller = [];
    private int _bank = 7;
    internal List<(int Target, int Type, int Effect)> Dispatches { get; } = [];
    internal ObjectCollisionRom()
    {
        _memory[0xcc2c] = 0xd0;
        _memory[0xc6cb] = 0xff;
        _memory[0xffb5] = 0xa0;
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Collision ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }
    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal byte Table(int address) => _rom[7 * 0x4000 + address - 0x4000];
    internal void ClearObjects() => Array.Clear(_memory, 0xd000, 0x1000);
    internal bool Call(int entry, int obj = 0xd080, int other = 0xd600)
    {
        Dispatches.Clear();
        _bank = 7;
        _memory[0xff97] = 7;
        _memory[0xffae] = (byte)obj;
        _memory[0xffaf] = (byte)(obj >> 8);
        _caller = [0x16, (byte)(obj >> 8), 0x21, (byte)other, (byte)(other >> 8),
            0xcd, (byte)entry, (byte)(entry >> 8), 0xf5, 0xc1,
            0x79, 0xea, 0x00, 0xc2, 0xc9];
        _cpu.RunCall(0xc100);
        if (_bank != 7 || _memory[0xff97] != 7)
            throw new InvalidDataException("Object collision call lost ROM bank ownership.");
        return (_memory[0xc200] & 0x10) != 0;
    }
    private int Read(int address)
    {
        if (address < 0x4000) return _rom[address];
        if (address < 0x8000 && _bank is 3 or 5 or 6 or 7 or 0x3f)
        {
            int value = _rom[_bank * 0x4000 + address - 0x4000];
            if (_bank == 7 && _cpu.InstructionAddress == 0x4371 && address is >= 0x6d0a and < 0x7caa)
                Dispatches.Add((_memory[0xffaf] * 256 + _memory[0xffae], _memory[0xff90], value));
            return value;
        }
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (Allowed(address)) return _memory[address];
        throw new InvalidDataException($"Collision ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }
    private void Write(int address, int value)
    {
        if (address == 0x2222) { _bank = value; return; }
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"Collision ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }
    private static bool Allowed(int address) => address is >= 0xc000 and < 0xe000 or >= 0xff80 and < 0xffc0;
}
