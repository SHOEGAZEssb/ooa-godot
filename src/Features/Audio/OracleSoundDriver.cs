using System;

namespace oracleofages;

/// <summary>
/// Executes only the imported US bank $39 sound driver and its banked read
/// trampoline. WRAM/HRAM belong to this driver; Game Boy peripherals other than
/// the four sound voices are deliberately unavailable. Unsupported execution
/// fails with its original bank, address and opcode.
/// </summary>
internal sealed class OracleSoundDriver
{
    private readonly OracleSoundData _data;
    private readonly OracleApu _apu;
    private readonly byte[] _ram = new byte[0x10000];
    private int _a, _f, _b, _c, _d, _e, _h, _l, _sp, _pc;
    private int _bank = OracleSoundData.BaseBank;
    private int _instructionAddress;
    private long _cycles;
    private const int Z = 0x80, N = 0x40, H = 0x20, C = 0x10;

    internal OracleSoundDriver(OracleSoundData data, OracleApu apu)
    {
        _data = data;
        _apu = apu;
        Call(0x4000, OracleSoundData.BaseBank);
    }

    internal int ReadState(int address) => _ram[address];
    internal int ReadStateWord(int address) => _ram[address] | (_ram[address + 1] << 8);

    // Public jump entries in code/audio.s, retained in sound_data.bin.
    internal void Update() => Call(0x4003);
    internal void Play(int id) => Call(0x4006, id);
    internal void Stop() => Call(0x4009);
    internal void SetVolume(int volume) => Call(0x4010, volume);

    private int BC { get => (_b << 8) | _c; set { _b = value >> 8; _c = value & 255; } }
    private int DE { get => (_d << 8) | _e; set { _d = value >> 8; _e = value & 255; } }
    private int HL { get => (_h << 8) | _l; set { _h = value >> 8; _l = value & 255; } }

    private void Call(int entry, int argument = 0)
    {
        _bank = OracleSoundData.BaseBank;
        _pc = entry;
        _a = argument;
        _sp = 0xdff0;
        _ram[_sp] = _ram[_sp + 1] = 0;
        for (int count = 0; count < 100000; count++)
        {
            if (_pc == 0)
                return;
            Step();
        }
        throw Failure("exceeded 100000 instructions");
    }

    private InvalidOperationException Failure(string detail) => new(
        $"code/audio.s driver ${_bank:x2}:${_instructionAddress:x4}: {detail}.");

    private void Clock(int cycles)
    {
        _cycles += cycles;
        // Ages runs the CGB CPU at double speed; the APU does not double.
        _apu.AdvanceClocks(cycles / 2);
    }

    private int Read(int address)
    {
        address &= 0xffff;
        Clock(4);
        if (address is >= 0x4000 and < 0x8000)
            return _data.ReadByte(_data.PointerOffset(_bank, address));
        if (address is >= 0xff10 and <= 0xff3f)
            return _apu.Read(address);
        if (address is >= 0xc000 and < 0xe000 or >= 0xff80)
            return _ram[address];
        throw Failure($"read outside sound memory at ${address:x4}");
    }

    private void Write(int address, int value)
    {
        address &= 0xffff;
        value &= 255;
        Clock(4);
        if (address is >= 0x2000 and < 0x4000)
        {
            if (value is < OracleSoundData.BaseBank or >= OracleSoundData.BaseBank + OracleSoundData.BankCount)
                throw Failure($"selected unsupported sound bank ${value:x2}");
            _bank = value;
        }
        else if (address is >= 0xff10 and <= 0xff3f)
            _apu.Write(address, value);
        else if (address is >= 0xc000 and < 0xe000 or >= 0xff80)
            _ram[address] = (byte)value;
        else
            throw Failure($"write outside sound memory at ${address:x4}");
    }

    private int Byte() { int value = Read(_pc); _pc = (_pc + 1) & 0xffff; return value; }
    private int Word() { int low = Byte(); return low | (Byte() << 8); }
    private int Pop() { int low = Read(_sp++); return low | (Read(_sp++) << 8); }
    private void Push(int value) { Write(--_sp, value >> 8); Write(--_sp, value); }
    private int Register(int index) => index switch
    {
        0 => _b, 1 => _c, 2 => _d, 3 => _e, 4 => _h, 5 => _l, 6 => Read(HL), _ => _a
    };
    private void Register(int index, int value)
    {
        value &= 255;
        switch (index)
        {
            case 0: _b = value; break; case 1: _c = value; break;
            case 2: _d = value; break; case 3: _e = value; break;
            case 4: _h = value; break; case 5: _l = value; break;
            case 6: Write(HL, value); break; default: _a = value; break;
        }
    }
    private int Pair(int index) => index switch { 0 => BC, 1 => DE, 2 => HL, _ => _sp };
    private void Pair(int index, int value)
    {
        value &= 0xffff;
        switch (index) { case 0: BC = value; break; case 1: DE = value; break; case 2: HL = value; break; default: _sp = value; break; }
    }
    private bool Condition(int index) => index switch
    {
        0 => (_f & Z) == 0, 1 => (_f & Z) != 0, 2 => (_f & C) == 0, _ => (_f & C) != 0
    };

    private void Alu(int operation, int value)
    {
        int carry = (_f & C) != 0 ? 1 : 0;
        int result;
        switch (operation)
        {
            case 0: case 1:
                if (operation == 0) carry = 0;
                result = _a + value + carry;
                _f = (((_a & 15) + (value & 15) + carry) > 15 ? H : 0) | (result > 255 ? C : 0);
                break;
            case 2: case 3: case 7:
                if (operation != 3) carry = 0;
                result = _a - value - carry;
                _f = N | ((_a & 15) < (value & 15) + carry ? H : 0) | (result < 0 ? C : 0);
                break;
            case 4: result = _a & value; _f = H; break;
            case 5: result = _a ^ value; _f = 0; break;
            default: result = _a | value; _f = 0; break;
        }
        if ((result & 255) == 0) _f |= Z;
        if (operation != 7) _a = result & 255;
    }

    private void Step()
    {
        long before = _cycles;
        _instructionAddress = _pc;
        int op = Byte(), cycles;
        if (op is >= 0x40 and <= 0x7f && op != 0x76)
        {
            Register((op >> 3) & 7, Register(op & 7));
            cycles = (op & 7) == 6 || ((op >> 3) & 7) == 6 ? 8 : 4;
        }
        else if (op is >= 0x80 and <= 0xbf)
        {
            Alu((op >> 3) & 7, Register(op & 7));
            cycles = (op & 7) == 6 ? 8 : 4;
        }
        else if ((op & 0xc7) == 0x04 || (op & 0xc7) == 0x05)
        {
            int index = (op >> 3) & 7, old = Register(index);
            bool decrement = (op & 1) != 0;
            int value = (old + (decrement ? -1 : 1)) & 255;
            _f = (_f & C) | (value == 0 ? Z : 0) | (decrement ? N : 0) |
                ((decrement ? (old & 15) == 0 : (old & 15) == 15) ? H : 0);
            Register(index, value);
            cycles = index == 6 ? 12 : 4;
        }
        else if ((op & 0xc7) == 0x06)
        {
            int index = (op >> 3) & 7;
            Register(index, Byte());
            cycles = index == 6 ? 12 : 8;
        }
        else if ((op & 0xcf) == 0x01) { Pair(op >> 4, Word()); cycles = 12; }
        else if ((op & 0xcf) == 0x03) { Pair(op >> 4, Pair(op >> 4) + 1); cycles = 8; }
        else if ((op & 0xcf) == 0x0b) { Pair(op >> 4, Pair(op >> 4) - 1); cycles = 8; }
        else if ((op & 0xcf) == 0x09)
        {
            int value = Pair(op >> 4), sum = HL + value;
            _f = (_f & Z) | (((HL & 0xfff) + (value & 0xfff)) > 0xfff ? H : 0) | (sum > 0xffff ? C : 0);
            HL = sum & 0xffff;
            cycles = 8;
        }
        else if ((op & 0xe7) == 0x20)
        {
            int offset = (sbyte)Byte();
            bool taken = Condition((op >> 3) & 3);
            if (taken) _pc = (_pc + offset) & 0xffff;
            cycles = taken ? 12 : 8;
        }
        else if ((op & 0xe7) == 0xc0)
        {
            bool taken = Condition((op >> 3) & 3);
            Clock(4);
            if (taken) _pc = Pop();
            cycles = taken ? 20 : 8;
        }
        else if ((op & 0xe7) == 0xc2 || (op & 0xe7) == 0xc4)
        {
            int target = Word();
            bool taken = Condition((op >> 3) & 3), call = (op & 7) == 4;
            if (taken) { if (call) { Clock(4); Push(_pc); } _pc = target; }
            cycles = taken ? (call ? 24 : 16) : 12;
        }
        else if ((op & 0xcf) == 0xc1)
        {
            int value = Pop(), index = (op >> 4) & 3;
            if (index == 3) { _a = value >> 8; _f = value & 0xf0; } else Pair(index, value);
            cycles = 12;
        }
        else if ((op & 0xcf) == 0xc5)
        {
            int index = (op >> 4) & 3;
            Clock(4); Push(index == 3 ? (_a << 8) | _f : Pair(index));
            cycles = 16;
        }
        else if ((op & 0xc7) == 0xc6) { Alu((op >> 3) & 7, Byte()); cycles = 8; }
        else switch (op)
        {
            case 0x00: cycles = 4; break;
            case 0x02: Write(BC, _a); cycles = 8; break;
            case 0x12: Write(DE, _a); cycles = 8; break;
            case 0x0a: _a = Read(BC); cycles = 8; break;
            case 0x1a: _a = Read(DE); cycles = 8; break;
            case 0x22: Write(HL, _a); HL = (HL + 1) & 0xffff; cycles = 8; break;
            case 0x32: Write(HL, _a); HL = (HL - 1) & 0xffff; cycles = 8; break;
            case 0x2a: _a = Read(HL); HL = (HL + 1) & 0xffff; cycles = 8; break;
            case 0x3a: _a = Read(HL); HL = (HL - 1) & 0xffff; cycles = 8; break;
            case 0x18: int offset = (sbyte)Byte(); _pc = (_pc + offset) & 0xffff; cycles = 12; break;
            case 0x07: _f = (_a & 0x80) != 0 ? C : 0; _a = ((_a << 1) | (_a >> 7)) & 255; cycles = 4; break;
            case 0x0f: _f = (_a & 1) != 0 ? C : 0; _a = (_a >> 1) | ((_a & 1) << 7); cycles = 4; break;
            case 0x17: int carry = (_f & C) != 0 ? 1 : 0; _f = (_a & 0x80) != 0 ? C : 0; _a = ((_a << 1) | carry) & 255; cycles = 4; break;
            case 0x1f: carry = (_f & C) != 0 ? 0x80 : 0; _f = (_a & 1) != 0 ? C : 0; _a = (_a >> 1) | carry; cycles = 4; break;
            case 0x2f: _a ^= 255; _f |= N | H; cycles = 4; break;
            case 0x37: _f = (_f & Z) | C; cycles = 4; break;
            case 0x3f: _f = (_f & Z) | ((_f & C) ^ C); cycles = 4; break;
            case 0xc3: _pc = Word(); cycles = 16; break;
            case 0xc9: _pc = Pop(); cycles = 16; break;
            case 0xcd: int target = Word(); Clock(4); Push(_pc); _pc = target; cycles = 24; break;
            case 0xe0: Write(0xff00 | Byte(), _a); cycles = 12; break;
            case 0xf0: _a = Read(0xff00 | Byte()); cycles = 12; break;
            case 0xe2: Write(0xff00 | _c, _a); cycles = 8; break;
            case 0xf2: _a = Read(0xff00 | _c); cycles = 8; break;
            case 0xe9: _pc = HL; cycles = 4; break;
            case 0xea: Write(Word(), _a); cycles = 16; break;
            case 0xfa: _a = Read(Word()); cycles = 16; break;
            case 0xcb: cycles = Extended(); break;
            default: throw Failure($"unsupported instruction ${op:x2}");
        }
        int remaining = cycles - (int)(_cycles - before);
        if (remaining < 0) throw Failure($"invalid cycle accounting for ${op:x2}");
        Clock(remaining);
    }

    private int Extended()
    {
        int op = Byte(), index = op & 7, value = Register(index), group = op >> 6, bit = (op >> 3) & 7;
        if (group == 1) _f = (_f & C) | H | ((value & (1 << bit)) == 0 ? Z : 0);
        else if (group == 2) Register(index, value & ~(1 << bit));
        else if (group == 3) Register(index, value | (1 << bit));
        else
        {
            int carry = 0, oldCarry = (_f & C) != 0 ? 1 : 0;
            switch (bit)
            {
                case 0: carry = value >> 7; value = (value << 1) | carry; break;
                case 1: carry = value & 1; value = (value >> 1) | (carry << 7); break;
                case 2: carry = value >> 7; value = (value << 1) | oldCarry; break;
                case 3: carry = value & 1; value = (value >> 1) | (oldCarry << 7); break;
                case 4: carry = value >> 7; value <<= 1; break;
                case 5: carry = value & 1; value = (value >> 1) | (value & 0x80); break;
                case 6: value = (value >> 4) | (value << 4); break;
                case 7: carry = value & 1; value >>= 1; break;
            }
            value &= 255;
            _f = (value == 0 ? Z : 0) | (carry != 0 ? C : 0);
            Register(index, value);
        }
        return index == 6 ? (group == 1 ? 12 : 16) : 8;
    }
}
