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
    private int _bank = OracleSoundData.BaseBank;
    private readonly OracleCpu _cpu;
    private int _pendingApuClocks;

    internal OracleSoundDriver(OracleSoundData data, OracleApu apu)
    {
        _data = data;
        _apu = apu;
        _cpu = new OracleCpu(Read, Write, cycles => _pendingApuClocks += cycles / 2,
            (address, detail) => new InvalidOperationException(
                $"code/audio.s driver ${_bank:x2}:${address:x4}: {detail}."));
        Call(0x4000, OracleSoundData.BaseBank);
    }

    internal int ReadState(int address) => _ram[address];
    internal byte ReadNativeAllocationAlias(int address)
    {
        CheckNativeAllocationAlias(address);
        return _ram[address];
    }
    internal void WriteNativeAllocationAlias(int address, byte value)
    {
        CheckNativeAllocationAlias(address);
        _ram[address] = value;
    }
    private static void CheckNativeAllocationAlias(int address)
    {
        // include/wram.s: wChannelSweep, wChannelVibratoActive and
        // wChannelVibratos are three consecutive six-channel byte arrays.
        if (address is < 0xc03f or >= 0xc051)
            throw new ArgumentOutOfRangeException(nameof(address), $"Allocation failure sound alias ${address:x4} is outside the traced channel arrays ($c03f-$c050).");
    }
    internal void SetChannelPitchSlideByte(int channel, byte value)
    {
        if (channel is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(channel));
        WriteNativeAllocationAlias(0xc03f + channel, value);
    }
    internal void SetChannelVolumeByte(int channel, byte value)
    {
        if (channel is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(channel));
        _ram[0xc07d + channel] = value;
    }
    internal int ReadStateWord(int address) => _ram[address] | (_ram[address + 1] << 8);

    // Public jump entries in code/audio.s, retained in sound_data.bin.
    internal void Update() => Call(0x4003);
    internal void Play(int id) => Call(0x4006, id);
    internal void Stop() => Call(0x4009);
    internal void SetVolume(int volume) => Call(0x4010, volume);

    private void Call(int entry, int argument = 0)
    {
        _bank = OracleSoundData.BaseBank;
        try
        {
            _cpu.RunCall(entry, argument);
        }
        finally
        {
            FlushApuClocks();
        }
    }

    private void FlushApuClocks()
    {
        // code/audio.s observes the APU only through $ff10-$ff3f. Coalesce
        // instruction clocks between those accesses, preserving the exact
        // clock before every register read/write and at each public return.
        // No foreground/gameplay update or register transition moves in time.
        int clocks = _pendingApuClocks;
        _pendingApuClocks = 0;
        if (clocks != 0) _apu.AdvanceClocks(clocks);
    }

    private InvalidOperationException Failure(string detail) => new(
        $"code/audio.s driver ${_bank:x2}:${_cpu.InstructionAddress:x4}: {detail}.");

    private int Read(int address)
    {
        address &= 0xffff;
        if (address is >= 0x4000 and < 0x8000)
            return _data.ReadByte(_data.PointerOffset(_bank, address));
        if (address is >= 0xff10 and <= 0xff3f)
        {
            FlushApuClocks();
            return _apu.Read(address);
        }
        if (address is >= 0xc000 and < 0xe000 or >= 0xff80)
            return _ram[address];
        throw Failure($"read outside sound memory at ${address:x4}");
    }

    private void Write(int address, int value)
    {
        address &= 0xffff;
        value &= 255;
        if (address is >= 0x2000 and < 0x4000)
        {
            if (value is < OracleSoundData.BaseBank or >= OracleSoundData.BaseBank + OracleSoundData.BankCount)
                throw Failure($"selected unsupported sound bank ${value:x2}");
            _bank = value;
        }
        else if (address is >= 0xff10 and <= 0xff3f)
        {
            FlushApuClocks();
            _apu.Write(address, value);
        }
        else if (address is >= 0xc000 and < 0xe000 or >= 0xff80)
            _ram[address] = (byte)value;
        else
            throw Failure($"write outside sound memory at ${address:x4}");
    }

}
