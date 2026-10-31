using System;
using System.IO;
using System.Collections.Generic;

namespace Oracles;

// Executes the original bank-$05 wall probes and special-object movement.
// The wrapper supplies registers and captures AF; no native routine is replaced.
internal sealed class LinkCollisionRom
{
    internal const int Probe = 0x5e62;
    internal const int Move = 0x5d9f;
    internal const int StandardSpeed = 0x5ce6; // updateLinkSpeed_standard
    internal const int Knockback = 0x5d5b;
    internal const int ActiveTile = 0x4406;
    internal const int ApplyTile = 0x42b7;
    internal const int ScreenBoundary = 0x4105; // bank $01
    internal const int Swim = 0x5698;
    internal const int Respawn = 0x507b;
    internal const int ForceState = 0x54c0;
    internal const int DamageRings = 0x4668; // bank $06 linkUpdateDamageToApplyForRings
    internal const int ApplyDamage = 0x46bb; // bank $06 linkApplyDamage
    internal const int Invincibility = 0x4279; // bank $05 updateLinkInvincibilityCounter
    internal const int Vulnerable = 0x1d28; // bank $00 checkLinkVulnerable
    internal const int Dying = 0x5033; // bank $05 linkState03
    internal const int OverHazard = 0x2216; // bank $00 objectCheckIsOverHazard
    private readonly ReadOnlyMemory<byte> _rom;
    private readonly byte[] _memory = new byte[0x10000];
    private readonly byte[][] _wram = new byte[8][];
    private readonly byte[][] _vram = [new byte[0x2000], new byte[0x2000]];
    private readonly OracleCpu _cpu;
    private byte[] _caller = [];
    private int _bank = 5;
    private readonly bool _bankedStack;
    internal List<int> SoundRequests { get; } = new();
    internal int RandomUpdates { get; private set; }

    internal LinkCollisionRom(bool bankedStack = false, OracleGame game = OracleGame.Ages)
    {
        _rom = ValidationRom.LoadCleanUs(game);
        _bankedStack = bankedStack;
        for (int bank = 2; bank < 8; bank++) _wram[bank] = new byte[0x1000];
        _memory[game == OracleGame.Ages ? 0xcc2c : 0xcc48] = 0xd0; // wLinkObjectIndex
        _memory[0xff97] = 5;
        _memory[game == OracleGame.Ages ? 0xffb5 : 0xffb3] = 0xa0; // Empty audio request queue; no audio driver runs.
        _memory[game == OracleGame.Ages ? 0xc6cb : 0xc6c5] = 0xff; // No equipped ring.
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Link collision ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }

    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal void Bank(int bank, int address, byte value) => _wram[bank][address - 0xd000] = value;
    internal byte BankByte(int bank, int address) => _wram[bank][address - 0xd000];
    internal int Word(int address) => _memory[address] | (_memory[address + 1] << 8);
    internal void Word(int address, int value)
    {
        _memory[address] = unchecked((byte)value);
        _memory[address + 1] = unchecked((byte)(value >> 8));
    }

    internal void Call(int entry, int speed = 0x28, int angle = 0, int bank = 5, int objectPage = 0xd0, int accumulator = 0,
        bool restoresBank = true, int? objectOffset = null, bool captureB = false)
    {
        _bank = bank;
        _memory[0xff97] = (byte)bank;
        byte[] entryStack = _bankedStack ? [0x31, 0xf0, 0xc2] : [];
        byte[] exitStack = _bankedStack ? [0x31, 0xf0, 0xdf] : [];
        byte[] objectLow = objectOffset is int low ? [0x1e,checked((byte)low)] : [];
        byte[] saveB = captureB ? [0xc5] : [];
        byte[] storeB = captureB ? [0xc1, 0x78, 0xea, 0x02, 0xc2] : [];
        _caller = [.. entryStack, 0x16, (byte)objectPage, 0x62, 0x3e, (byte)accumulator, 0x01, (byte)angle, (byte)speed,
            .. objectLow,
            0xcd, (byte)entry, (byte)(entry >> 8),
            .. saveB, 0xf5, 0xc1, 0x78, 0xea, 0x00, 0xc2, 0x79, 0xea, 0x01, 0xc2, .. storeB, .. exitStack, 0xc9];
        _cpu.RunCall(0xc100);
        if (restoresBank && (_bank != bank || _memory[0xff97] != bank))
            throw new InvalidDataException("Link collision call failed to restore ROM bank ownership.");
    }

    private int Read(int address)
    {
        if (address < 0x4000) return _rom.Span[address];
        // Flute callers use $09, HUD/common graphics $19/$1b/$27/$28, interactions Seasons $29/Ages $2a.
        if (address < 0x8000 && _bank is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 0x0a or 0x0b or 0x0c or 0x0d or 0x0e or 0x0f or 0x10 or 0x11 or 0x12 or 0x14 or 0x15 or 0x16 or 0x19 or 0x1b or 0x27 or 0x28 or 0x29 or 0x2a or 0x3f)
            return _rom.Span[_bank * 0x4000 + address - 0x4000];
        if (address is >= 0x8000 and < 0xa000) return _vram[_memory[0xff4f] & 1][address - 0x8000];
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (address is >= 0xd000 and < 0xe000 && _memory[0xff70] > 1)
            return _wram[_memory[0xff70]][address - 0xd000];
        if (Allowed(address)) return _memory[address];
        throw new InvalidDataException($"Link collision ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }

    private void Write(int address, int value)
    {
        // getRandomNumber_noPreserveVars commits hRng1 once per native draw.
        if (address == 0xff94) RandomUpdates++;
        if (address is >= 0xc0a0 and <= 0xc0af) SoundRequests.Add(value);
        if (address == 0x2222) { _bank = value; return; }
        if (address is >= 0x8000 and < 0xa000)
        { _vram[_memory[0xff4f] & 1][address - 0x8000] = (byte)value; return; }
        if (address is >= 0xd000 and < 0xe000 && _memory[0xff70] > 1)
        { _wram[_memory[0xff70]][address - 0xd000] = (byte)value; return; }
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"Link collision ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }

    // Native animation, item cancellation, splash allocation, inventory and
    // respawn routines use WRAM. LCD-off graphics DMA registers are staged;
    // these gameplay fixtures do not render or consume VRAM pixels.
    private static bool Allowed(int address) => address is
        >= 0xc000 and < 0xe000 or >= 0xff80 and < 0xffc0 or 0xff70 or
        0xff40 or 0xff4f or >= 0xff51 and <= 0xff55;
}
