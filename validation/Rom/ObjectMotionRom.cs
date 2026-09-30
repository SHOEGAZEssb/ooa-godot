using System;
using System.IO;

namespace oracleofages;

// Bounded clean-US bank0 calls. The wrapper only loads inputs and captures AF;
// all movement arithmetic and table reads execute original ROM instructions.
internal sealed class ObjectMotionRom
{
    internal const int ApplySpeed = 0x201d;
    internal const int RelativeAngle = 0x1ea4;
    internal const int UpdateSpeedZ = 0x1f45;
    internal const int Object = 0xd100;
    private const int Caller = 0xc100;
    private readonly ReadOnlyMemory<byte> _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly OracleCpu _cpu;
    private byte[] _caller = [];
    private int _bank = 1;
    private int _entry;
    private int _resultWrites;

    internal ObjectMotionRom()
    {
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Motion ROM ${pc:x4}: {detail}."));
    }

    internal byte this[int address]
    {
        get => _memory[address];
        set => _memory[address] = value;
    }

    internal int Word(int address) => _memory[address] | (_memory[address + 1] << 8);
    internal void Word(int address, int value)
    {
        _memory[address] = unchecked((byte)value);
        _memory[address + 1] = unchecked((byte)(value >> 8));
    }

    internal void Call(int entry, int argument = 0, int targetY = 0, int targetX = 0, int slot = 0)
    {
        _entry = entry;
        _bank = 1;
        _memory[0xff97] = 1;
        _memory[0xffae] = (byte)slot;
        _resultWrites = 0;
        _caller = [0x16, 0xd1, // ld d,$d1: active object page
            0x01, (byte)targetX, (byte)targetY, // ld bc,targetYX
            0x3e, (byte)argument, // ld a,argument
            0xcd, (byte)entry, (byte)(entry >> 8),
            0xf5, 0xc1, // push af; pop bc (capture flags without altering them)
            0x78, 0xea, 0x00, 0xc2, // A -> $c200
            0x79, 0xea, 0x01, 0xc2, // F -> $c201
            0xc9];
        _cpu.RunCall(Caller);
        if (_resultWrites != 3 || _bank != 1 || _memory[0xff97] != 1)
            throw new InvalidDataException($"Motion ROM ${entry:x4} lost result capture or ROM bank restoration.");
    }

    internal int Result => _memory[0xc200];
    internal bool Zero => (_memory[0xc201] & 0x80) != 0;

    private int Read(int address)
    {
        if (address >= Caller && address < Caller + _caller.Length) return _caller[address - Caller];
        // Routine bodies, their source tables and the two small called helpers.
        bool code = _entry switch
        {
            ApplySpeed => address is >= 0x201d and < 0x208c or >= 0x10 and < 0x15,
            RelativeAngle => address is >= 0x1ea4 and < 0x1f45,
            UpdateSpeedZ => address is >= 0x1f45 and < 0x1f67 or >= 0x23a7 and < 0x23b0,
            _ => false
        };
        if (code) return _rom.Span[address];
        // bank3.objectSpeedTable: 24 rows of 40 signed words.
        if (_entry == ApplySpeed && _bank == 3 && address is >= 0x409b and < 0x481b)
            return _rom.Span[0xc000 + address - 0x4000];
        if (address is >= Object and < Object + 0x100 or >= 0xdfe0 and <= 0xdff1 or 0xffae)
            return _memory[address];
        if (_entry == ApplySpeed && address is 0xff97 or >= 0xcec0 and <= 0xcec3)
            return _memory[address];
        if (_entry == RelativeAngle && address is 0xff8e or 0xff8f)
            return _memory[address];
        throw new InvalidDataException($"Motion ROM ${_entry:x4}: undeclared read ${address:x4}.");
    }

    private void Write(int address, int value)
    {
        if (_entry == ApplySpeed && address == 0x2222) { _bank = value; return; }
        if (address is 0xc200 or 0xc201) _resultWrites |= 1 << (address - 0xc200);
        else if (address is >= 0xdfe0 and <= 0xdff1) { }
        else if (_entry == ApplySpeed && (address is 0xff97 or >= 0xcec0 and <= 0xcec3 ||
                 address >= Object && address < Object + 0x100 && (address & 0x3f) is >= 0x0a and <= 0x0d)) { }
        else if (_entry == RelativeAngle && address is 0xff8e or 0xff8f) { }
        else if (_entry == UpdateSpeedZ && address >= Object && address < Object + 0x100 &&
                 (address & 0x3f) is 0x0e or 0x0f or 0x14 or 0x15) { }
        else throw new InvalidDataException($"Motion ROM ${_entry:x4}: undeclared write ${address:x4}.");
        _memory[address] = (byte)value;
    }
}
