using System;
using System.IO;

namespace oracleofages;

// Original clean-US giveTreasure/removeRupeeValue, including bank-$3f
// collection modes, extra grants, inventory placement and sound requests.
internal sealed class TreasureRom
{
    private readonly byte[] _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly OracleCpu _cpu;
    private byte[] _caller = [];
    private int _bank;
    internal TreasureRom()
    {
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Treasure ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }
    internal byte this[int address] => _memory[address];
    internal void Seed(OracleSaveData save, int dungeon = 0, int upgrades = 0)
    {
        Array.Clear(_memory);
        save.ReadWramBytes(0xc5b0, _memory.AsSpan(0xc5b0, OracleSaveData.FileSize));
        _memory[0xcc39] = (byte)dungeon; // wDungeonIndex
        _memory[0xcca8] = (byte)upgrades;
        _memory[0xffb5] = 0xa0; // sound request queue write pointer
    }
    internal void Give(int treasure, int parameter) => Call(0x171c, treasure, parameter);
    internal void RemoveRupees(int value) => Call(0x1778, value, 0);
    internal void RoomLoadMaturity() => Call(0x1821, 5, 0);
    private void Call(int entry, int argument, int parameter)
    {
        _bank = 1;
        _memory[0xff97] = 1;
        _caller = [0x3e, (byte)argument, 0x0e, (byte)parameter,
            0xcd, (byte)entry, (byte)(entry >> 8), 0xc9];
        _cpu.RunCall(0xc100);
        if (_bank != 1 || _memory[0xff97] != 1)
            throw new InvalidDataException("Treasure ROM lost bank restoration.");
    }
    private int Read(int address)
    {
        if (address < 0x4000) return _rom[address];
        if (address < 0x8000 && _bank == 0x3f) return _rom[0xfc000 + address - 0x4000];
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (Allowed(address)) return _memory[address];
        throw new InvalidDataException($"Treasure ROM ${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }
    private void Write(int address, int value)
    {
        if (address == 0x2222) { _bank = value; return; }
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"Treasure ROM ${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }
    private static bool Allowed(int address) => address is >= 0xc000 and < 0xe000 or >= 0xff80 and < 0xffc0;
}
