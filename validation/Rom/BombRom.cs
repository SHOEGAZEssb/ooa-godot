using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace oracleofages;

// Clean-US Link item dispatch, grabbed-position handoff, child update and
// post pass. Graphics are resident; every called instruction is original.
internal sealed class BombRom
{
    private readonly ReadOnlyMemory<byte> _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly byte[] _wram3 = new byte[0x1000];
    private byte[] _caller = [];
    private readonly OracleCpu _cpu;
    private int _bank = 6;
    internal List<int> Sounds { get; } = new();
    internal int RandomCalls { get; private set; }
    internal int MovementKeys { get; set; }
    internal int Angle { get; set; } = 0xff;
    internal int[] BombSlots => Enumerable.Range(0xd7, 5)
        .Select(page => page * 256)
        .Where(slot => this[slot] != 0 && this[slot + 1] == 3).ToArray();

    internal BombRom(int direction = 0, int ring = 0xff, int bombs = 0x10, int seed = 0x1234)
    {
        this[0xcc2c] = 0xd0;
        this[0xcc2b] = 0xff;
        this[0xc6cb] = (byte)ring;
        this[0xc6b0] = (byte)bombs;
        this[0xc6b1] = 0x10;
        this[0xc6aa] = this[0xc6ab] = 12;
        this[0xcc08] = 0x78; // itemData: resident common item graphics.
        this[0xcc09] = 1;
        this[0xffb5] = 0xa0;
        this[0xff94] = (byte)seed;
        this[0xff95] = (byte)(seed >> 8);
        this[0xd024] = 0x80;
        this[0xd026] = this[0xd027] = 6;
        this[0xd008] = (byte)direction;
        this[0xd009] = (byte)(direction * 8);
        this[0xd00b] = 64;
        this[0xd00d] = 80;
        this[0xcc86] = 128; // wRoomEdgeY/X, small room.
        this[0xcc87] = 160;
        Array.Fill(_memory, (byte)0xa0, 0xcf00, 0x100);
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Bomb ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }

    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal int Word(int address) => this[address] | this[address + 1] << 8;
    internal byte Underlying(int offset) => _wram3[0xf00 + offset];
    internal void SetUnderlying(int offset, byte tile) => _wram3[0xf00 + offset] = tile;
    internal void SetTileCollision(int tile, byte collision) => _wram3[0xb00 + tile] = collision;
    internal void Update(bool held = false, bool pressed = false, bool primary = true, bool moveLink = false)
    {
        this[primary ? 0xc689 : 0xc688] = 3;
        this[primary ? 0xc688 : 0xc689] = 0;
        this[0xcc29] = (byte)(MovementKeys | (held ? primary ? 1 : 2 : 0));
        this[0xcc2a] = pressed ? (byte)(primary ? 1 : 2) : (byte)0;
        this[0xcc2b] = (byte)Angle;
        if (this[0xcba0] == 0)
        {
            Call(6, 0x48b3); // checkUseItems
            if (moveLink && this[0xcc61] == 0 && Angle < 0x20)
            {
                // The caller supplies ordinary floor speed. Parent movement
                // ownership is read from ROM, collision/movement executed.
                CallLinkMovement(0x5e62); // Link wall probes
                CallLinkMovement(0x5d9f); // native collision-aware movement
            }
        }
        Call(7, 0x4872); // updateItems
        // bank0.updateAllObjects: attachment follows all object movement,
        // even when text froze parents and initialized children.
        if ((this[0xcc5a] & 0x80) != 0) Call(6, 0x54df);
        Call(7, 0x491a); // updateItemsPost
    }

    internal void Call(int bank, int address)
    {
        _bank = bank;
        this[0xff97] = (byte)bank;
        // Native tile breaking selects WRAM3, so keep the execution stack
        // in unbanked WRAM. The wrapper supplies a call, not replacement code.
        _caller = [0x31, 0xf0, 0xc2, 0xcd, (byte)address, (byte)(address >> 8), 0x31, 0xf0, 0xdf, 0xc9];
        _cpu.RunCall(0xc100);
    }

    private void CallLinkMovement(int address)
    {
        _bank = 5;
        this[0xff97] = 5;
        this[0xffae] = 0;
        this[0xffaf] = 0xd0;
        _caller = [0x31, 0xf0, 0xc2, 0x16, 0xd0, 0x62, 0x01, (byte)Angle, 0x28,
            0xcd, (byte)address, (byte)(address >> 8), 0x31, 0xf0, 0xdf, 0xc9];
        _cpu.RunCall(0xc100);
    }

    private int Read(int address)
    {
        if (address < 0x4000) return _rom.Span[address];
        if (address < 0x8000) return _rom.Span[_bank * 0x4000 + address - 0x4000];
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (address is >= 0xd000 and < 0xe000 && _memory[0xff70] == 3) return _wram3[address - 0xd000];
        if (Allowed(address)) return _memory[address];
        throw new InvalidDataException($"Bomb ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }

    private void Write(int address, int value)
    {
        if (address == 0x2222) { _bank = value; return; }
        if (address == 0xff94) RandomCalls++;
        if (address is >= 0xc0a0 and <= 0xc0af) Sounds.Add(value);
        if (address is >= 0xd000 and < 0xe000 && _memory[0xff70] == 3) { _wram3[address - 0xd000] = (byte)value; return; }
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"Bomb ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }

    private static bool Allowed(int address) => address is
        >= 0xc000 and < 0xe000 or >= 0xff80 and < 0xffc0 or 0xff70;
}
