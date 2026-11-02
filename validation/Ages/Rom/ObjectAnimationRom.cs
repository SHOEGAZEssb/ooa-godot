using System.IO;

namespace Oracles;

// Executes original object animation setters/advancers and their ROM tables.
// Only the register-loading caller is synthetic; no routine or stream is replaced.
internal sealed class ObjectAnimationRom : RomFixture
{
    private readonly int _animate, _set, _object, _streamBank, _oamBank;
    internal ObjectAnimationRom(int kind, int id) : base("Animation")
    {
        (_animate, _set, _object, _streamBank, _oamBank) = kind switch
        {
            0 => (0x261b, 0x262e, 0xd140, 0x16, 0x14), // interaction
            1 => (0x2818, 0x282b, 0xd180, 0x0d, 0x13), // enemy
            2 => (0x2978, 0x2988, 0xd1c0, 0x16, 0x14), // part
            3 => (0x49d9, 0x49e2, 0xd700, 0x07, 0x13), // item, bank $07
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        _memory[_object + 1] = (byte)id;
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
        int returnBank = _object == 0xd700 ? 7 : 0x11;
        _bank = returnBank;
        _memory[0xff97] = (byte)returnBank;
        _caller = [0x16, (byte)(_object >> 8), 0x3e, (byte)argument, 0xcd, (byte)entry, (byte)(entry >> 8), 0xc9];
        _cpu.RunCall(0xc100);
        if (_bank != returnBank || _memory[0xff97] != returnBank)
            throw new InvalidDataException("Animation helper lost its return bank.");
    }
    private int Rom(int bank, int address) => _rom.Span[bank * 0x4000 + (address & 0x3fff)];
    protected override bool RomBankAllowed(int bank) => bank is 0x07 or 0x0d or 0x16;
    protected override bool Allowed(int address) => address is >= 0xd000 and < 0xe000 or
        0xcba0 or 0xcc8a or 0xcd00 or 0xff97 or 0xffae or 0xffaf;
}
