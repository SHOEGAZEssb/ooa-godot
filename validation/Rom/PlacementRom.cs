using System;
using System.IO;

namespace oracleofages;

// Validation-only execution of the clean-US placement routines and room parser.
// Banks $02/$12/$15 own placement, object streams and room-pointer lookup.
internal sealed class PlacementRom
{
    private readonly ReadOnlyMemory<byte> _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly byte[] _bank4 = new byte[0x1000];
    private readonly OracleCpu _cpu;
    private byte[] _caller = [];
    private int _romBank = 0x12;
    private int _wramBank = 1;
    internal int RandomCalls { get; private set; }

    internal PlacementRom()
    {
        _memory[0xff97] = 0x12;
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Placement ROM ${_romBank:x2}:${pc:x4}: {detail}."));
    }

    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal byte[] Buffer => _bank4.AsSpan(0, 256).ToArray();
    internal byte Result => _memory[0xc200];

    internal void AssertRoom33Stream()
    {
        // objects/ages/enemyData.s:group0Map33EnemyObjectData, clean US $12:$4355.
        byte[] expected = [0xf6, 0x40, 0x08, 0x00, 0xf6, 0x20, 0x18, 0x00, 0xfe];
        if (!_rom.Span.Slice(0x48355, expected.Length).SequenceEqual(expected))
            throw new InvalidDataException("Clean-US room $0:$33 enemy stream at $12:$4355 changed.");
    }

    internal void Generate() => Call(0x3215); // bank0.generateRandomBuffer
    internal void ParseRoom() => Call(0x55b7); // objectData.parseObjectData, bank $12
    internal void NextBufferedValue() => Call(0x7959, 2);

    private void Call(int entry, int bank = 0x12)
    {
        _romBank = bank;
        _memory[0xff97] = (byte)bank;
        // No replacement of original functions: only call and capture returned A.
        _caller = [0xcd, (byte)entry, (byte)(entry >> 8), 0xea, 0x00, 0xc2, 0xc9];
        _cpu.RunCall(0xc100);
        if (_romBank != bank || _memory[0xff97] != bank || _wramBank != 1)
            throw new InvalidDataException($"Placement ROM ${bank:x2}:${entry:x4} failed to restore bank ownership.");
    }

    private int Read(int address)
    {
        if (address < 0x4000) return _rom.Span[address];
        if (address < 0x8000 && _romBank is 2 or 0x12 or 0x15)
            return _rom.Span[_romBank * 0x4000 + address - 0x4000];
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (IsBankedMemory(address))
            return _wramBank == 4 ? _bank4[address - 0xd000] : _memory[address];
        // Entry context, recent-defeat history, collisions/layout, placement
        // scratch and the far-call/RNG HRAM inputs. No hardware is emulated.
        if (address is 0xcc05 or 0xcc2d or 0xcc30 or 0xcc33 or 0xcc4a or 0xcc85 or 0xcd00 or 0xcd02 or
            >= 0xcdc0 and <= 0xcdd1 or >= 0xce00 and <= 0xcfc0 or >= 0xff8b and <= 0xff97)
            return _memory[address];
        throw new InvalidDataException($"Placement ROM ${_romBank:x2}:${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }

    private void Write(int address, int value)
    {
        if (address == 0x2222) { _romBank = value; return; }
        if (address == 0xff70)
        {
            if (value is not (1 or 4)) throw new InvalidDataException($"Unexpected placement WRAM bank ${value:x2}.");
            _wramBank = value;
            return;
        }
        if (address == 0xff94) RandomCalls++;
        if (IsBankedMemory(address))
        {
            if (_wramBank == 4) _bank4[address - 0xd000] = (byte)value;
            else _memory[address] = (byte)value;
            return;
        }
        if (address is 0xc200 or 0xcc85 or 0xcd02 or 0xcfc0 or >= 0xcdc0 and <= 0xcdd1 or
            >= 0xcec0 and < 0xcee0 or >= 0xff8b and <= 0xff95 or 0xff97)
        {
            _memory[address] = (byte)value;
            return;
        }
        throw new InvalidDataException($"Placement ROM ${_romBank:x2}:${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }

    private bool IsBankedMemory(int address)
    {
        // OracleCpu owns a bounded test stack starting at $dff0 in each bank.
        if (address is >= 0xdfc0 and <= 0xdff1) return true;
        if (_wramBank == 4) return address is >= 0xd000 and <= 0xd0ff;
        // Direct main-stream interaction opcodes use $40-$7f; enemy
        // placement uses $80-$bf. Both retain outgoing enabled=$02 slots.
        return address is >= 0xd000 and < 0xe000 && (address & 0xff) is >= 0x40 and < 0xc0;
    }
}
