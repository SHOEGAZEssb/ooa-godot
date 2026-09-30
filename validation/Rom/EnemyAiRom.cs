using System;
using System.IO;

namespace oracleofages;

// Executes bank0.updateEnemies, including its gates, slot walk, common
// initialization, native handlers and animation streams. Graphics are resident:
// this fixture does not emulate VRAM uploads, interrupts or hardware timing.
internal sealed class EnemyAiRom
{
    private readonly byte[] _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly OracleCpu _cpu;
    private int _bank = 0x0d;
    internal int RandomCalls { get; private set; }

    internal EnemyAiRom()
    {
        _memory[0xff97] = 0x0d;
        // bank3f.enemyData, loaded via enemyGetObjectGfxIndex $3f:$4337.
        _memory[0xcc08] = _rom[0xfdd4b + 0x08 * 4];
        _memory[0xcc0a] = _rom[0xfdd4b + 0x18 * 4];
        // Small-room loadRoomCollisions.@blankDataAroundCollisions: bottom
        // and wrapped top rows, right and wrapped left columns are $ff.
        for (int x = 0; x < 16; x++)
            _memory[0xce80 + x] = _memory[0xcef0 + x] = 0xff;
        for (int y = 0; y < 11; y++)
            _memory[0xce0a + y * 16] = _memory[0xce0f + y * 16] = 0xff;
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Enemy AI ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }

    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal int Word(int address) => _memory[address] | (_memory[address + 1] << 8);
    internal void Word(int address, int value)
    {
        _memory[address] = unchecked((byte)value);
        _memory[address + 1] = unchecked((byte)(value >> 8));
    }
    internal void Update() => _cpu.RunCall(0x2ea5);

    private int Read(int address)
    {
        if (address < 0x4000) return _rom[address];
        if (address < 0x8000 && _bank is 3 or 0x0d or 0x11 or 0x3f)
            return _rom[_bank * 0x4000 + address - 0x4000];
        if (MemoryAllowed(address)) return _memory[address];
        throw new InvalidDataException($"Enemy AI ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }

    private void Write(int address, int value)
    {
        if (address == 0x2222) { _bank = value; return; }
        if (address == 0xff94) RandomCalls++;
        if (MemoryAllowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"Enemy AI ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }

    private static bool MemoryAllowed(int address) =>
        address is 0xc4ab or 0xcba0 or >= 0xcc00 and < 0xe000 or >= 0xff80 and < 0xffc0;
}
