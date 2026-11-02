using System.IO;

namespace Oracles;

// Only memory mechanics are shared; each fixture declares its native access policy.
internal abstract class RomFixture
{
    protected readonly ReadOnlyMemory<byte> _rom;
    protected readonly byte[] _memory = new byte[0x10000];
    protected readonly OracleCpu _cpu;
    protected byte[] _caller = [];
    protected int _bank;
    private readonly string _source;

    protected RomFixture(string source, int bank = 0, OracleGame game = OracleGame.Ages)
    {
        _source = source;
        _bank = bank;
        _rom = ValidationRom.LoadCleanUs(game);
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"{source} ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }

    protected abstract bool Allowed(int address);
    protected virtual bool RomBankAllowed(int bank) => true;

    protected virtual int Read(int address)
    {
        if (address < 0x4000) return _rom.Span[address];
        if (address < 0x8000 && RomBankAllowed(_bank))
            return _rom.Span[_bank * 0x4000 + address - 0x4000];
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (Allowed(address)) return _memory[address];
        throw Undeclared("read", address);
    }

    protected virtual void Write(int address, int value)
    {
        if (address == 0x2222) { _bank = value; return; }
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw Undeclared("write", address);
    }

    protected InvalidDataException Undeclared(string operation, int address) =>
        new($"{_source} ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared {operation} ${address:x4}.");
}
