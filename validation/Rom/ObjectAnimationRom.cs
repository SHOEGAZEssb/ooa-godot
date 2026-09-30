using System;
using System.IO;
using System.Linq;

namespace oracleofages;

// Executes original bank0 animation setters/advancers and their ROM tables.
// Only the register-loading caller is synthetic; no routine or stream is replaced.
internal sealed class ObjectAnimationRom
{
    private readonly byte[] _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly OracleCpu _cpu;
    private readonly int _animate, _set, _object, _streamBank, _oamBank;
    private byte[] _caller = [];
    private int _bank;
    internal ObjectAnimationRom(int kind, int id)
    {
        (_animate, _set, _object, _streamBank, _oamBank) = kind switch
        {
            0 => (0x261b, 0x262e, 0xd140, 0x16, 0x14), // interaction
            1 => (0x2818, 0x282b, 0xd180, 0x0d, 0x13), // enemy
            2 => (0x2978, 0x2988, 0xd1c0, 0x16, 0x14), // part
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        _memory[_object + 1] = (byte)id;
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Animation ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }
    internal int Counter { get => _memory[_object + 0x20]; set => _memory[_object + 0x20] = (byte)value; }
    internal int Parameter { get => _memory[_object + 0x21]; set => _memory[_object + 0x21] = (byte)value; }
    internal int Pointer => _memory[_object + 0x22] | _memory[_object + 0x23] << 8;
    internal int Duration => Rom(_streamBank, Pointer - 3);
    internal string Oam
    {
        get
        {
            int address = _memory[_object + 0x1e] | _memory[_object + 0x1f] << 8;
            int count = Rom(_oamBank, address);
            if (count > 40) throw new InvalidDataException($"Invalid ROM animation OAM count {count}.");
            return string.Join(';', Enumerable.Range(0, count).Select(cell =>
                string.Join(',', Enumerable.Range(0, 4).Select(b => Rom(_oamBank, address + 1 + cell * 4 + b)))));
        }
    }
    internal void Set(int animation) => Call(_set, animation);
    internal void Advance() => Call(_animate, 0);
    internal void FrozenInteractionUpdate(bool text)
    {
        if (_object != 0xd140) throw new InvalidOperationException("Interaction gate fixture requires an interaction.");
        _memory[_object] = 1;
        _memory[_object + 4] = 1;
        _memory[0xcba0] = (byte)(text ? 1 : 0);
        _memory[0xcc8a] = (byte)(text ? 0 : 2);
        Call(0x3b36, 0); // updateInteractions executes its actual state-zero-only scan.
        _memory[0xcba0] = _memory[0xcc8a] = 0;
    }
    private void Call(int entry, int argument)
    {
        _bank = 0x11;
        _memory[0xff97] = 0x11;
        _caller = [0x16, 0xd1, 0x3e, (byte)argument, 0xcd, (byte)entry, (byte)(entry >> 8), 0xc9];
        _cpu.RunCall(0xc100);
        if (_bank != 0x11 || _memory[0xff97] != 0x11)
            throw new InvalidDataException("Animation helper lost its return bank.");
    }
    private int Rom(int bank, int address) => _rom[bank * 0x4000 + (address & 0x3fff)];
    private int Read(int address)
    {
        if (address < 0x4000) return _rom[address];
        if (address < 0x8000 && _bank is 0x0d or 0x16) return Rom(_bank, address);
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (Allowed(address)) return _memory[address];
        throw new InvalidDataException($"Animation ROM ${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }
    private void Write(int address, int value)
    {
        if (address == 0x2222) { _bank = value; return; }
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"Animation ROM ${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }
    private bool Allowed(int address) => address is >= 0xd000 and < 0xe000 or
        0xcba0 or 0xcc8a or 0xcd00 or 0xff97 or 0xffae or 0xffaf;
}
