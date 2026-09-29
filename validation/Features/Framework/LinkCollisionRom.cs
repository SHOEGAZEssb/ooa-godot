using System;
using System.IO;

namespace oracleofages;

// Executes the original bank-$05 wall probes and special-object movement.
// The wrapper supplies registers and captures AF; no native routine is replaced.
internal sealed class LinkCollisionRom
{
    internal const int Probe = 0x5e62;
    internal const int Move = 0x5d9f;
    internal const int Knockback = 0x5d5b;
    internal const int ActiveTile = 0x4406;
    internal const int ApplyTile = 0x42b7;
    internal const int ScreenBoundary = 0x4105; // bank $01
    private readonly byte[] _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly OracleCpu _cpu;
    private byte[] _caller = [];
    private int _bank = 5;

    internal LinkCollisionRom()
    {
        _memory[0xcc2c] = 0xd0; // wLinkObjectIndex
        _memory[0xff97] = 5;
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Link collision ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }

    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal int Word(int address) => _memory[address] | (_memory[address + 1] << 8);
    internal void Word(int address, int value)
    {
        _memory[address] = unchecked((byte)value);
        _memory[address + 1] = unchecked((byte)(value >> 8));
    }

    internal void Call(int entry, int speed = 0x28, int angle = 0, int bank = 5)
    {
        _bank = bank;
        _memory[0xff97] = (byte)bank;
        _caller = [0x16, 0xd0, 0x01, (byte)angle, (byte)speed,
            0xcd, (byte)entry, (byte)(entry >> 8),
            0xf5, 0xc1, 0x78, 0xea, 0x00, 0xc2, 0x79, 0xea, 0x01, 0xc2, 0xc9];
        _cpu.RunCall(0xc100);
        if (_bank != bank || _memory[0xff97] != bank)
            throw new InvalidDataException("Link collision call failed to restore ROM bank ownership.");
    }

    private int Read(int address)
    {
        if (address < 0x4000) return _rom[address];
        if (address < 0x8000 && _bank is 1 or 3 or 5)
            return _rom[_bank * 0x4000 + address - 0x4000];
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (Allowed(address)) return _memory[address];
        throw new InvalidDataException($"Link collision ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }

    private void Write(int address, int value)
    {
        if (address == 0x2222) { _bank = value; return; }
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"Link collision ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }

    private static bool Allowed(int address) => address is 0xc200 or 0xc201 or
        >= 0xcc00 and < 0xd000 or
        >= 0xd000 and < 0xd040 or 0xd101 or >= 0xdfc0 and <= 0xdff1 or >= 0xff80 and < 0xffc0;
}
