using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace oracleofages;

// Executes the complete native ENEMY, PART and INTERACTION walks over a
// declared room/state. Graphics are resident; hardware access remains an error.
internal sealed class EnemyStatusRom
{
    private readonly ReadOnlyMemory<byte> _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly byte[][] _wram = new byte[8][];
    private readonly OracleCpu _cpu;
    private int _bank = 0x0d;
    internal int RandomCalls { get; private set; }
    internal int InitialRuntimeRandomCalls { get; }
    internal List<int> Sounds { get; } = [];
    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal int Word(int address) => this[address] | this[address + 1] << 8;
    internal void Word(int address, int value)
    {
        this[address] = unchecked((byte)value);
        this[address + 1] = unchecked((byte)(value >> 8));
    }

    internal EnemyStatusRom(OracleRoomData room, OracleSaveData save, int randomCalls)
    {
        InitialRuntimeRandomCalls = randomCalls;
        for (int bank = 2; bank < 8; bank++) _wram[bank] = new byte[0x1000];
        for (int address = 0xc600; address < 0xc800; address++) this[address] = save.ReadWramByte(address);
        this[0xff70] = 1;
        this[0xffb5] = 0xa0;
        this[0xcc08] = _rom.Span[0xfdd4b + 0x0c * 4]; // resident Moblin graphics
        this[0xcc0a] = 0x8e; // partData[$1a]: enemy arrows
        this[0xcc0c] = 0x78; // partData[$01]: item drops
        this[0xcc2c] = 0xd0;
        this[0xcc2d] = (byte)room.Group;
        this[0xcc2e] = 1;
        this[0xcc30] = (byte)room.Id;
        this[0xcc33] = (byte)room.ActiveCollisions;
        this[0xcc34] = (byte)room.TilesetFlags;
        this[0xcc86] = (byte)room.Height;
        this[0xcc87] = (byte)room.Width;
        this[0xcdc0] = (byte)room.Id;
        this[0xcdd0] = 2;
        this[0xcdd1] = 1;
        for (int x = 0; x < 16; x++)
            this[0xcef0 + x] = this[0xce00 + room.HeightInTiles * 16 + x] = 0xff;
        for (int y = 0; y < 11; y++)
            this[0xce0f + y * 16] = this[0xce00 + room.WidthInTiles + y * 16] = 0xff;
        CopyRoom(room);
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Enemy status ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }

    internal void CopyRoom(OracleRoomData room)
    {
        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
        {
            Vector2 point = new(x * 16 + 8, y * 16 + 8);
            this[0xcf00 + y * 16 + x] = room.GetMetatile(point);
            this[0xce00 + y * 16 + x] = (byte)room.GetTerrainInfo(point).Collision;
        }
    }

    internal void Update(int frame, Vector2 link)
    {
        this[0xcc00] = (byte)frame;
        this[0xffb2] = this[0xd00b] = (byte)link.Y;
        this[0xffb3] = this[0xd00d] = (byte)link.X;
        Call(0x2ea5, 0x0d); // bank0.updateEnemies
        Call(0x5e58, 0x11); // partCode.updateParts
        Call(0x3b36, 0x08); // bank0.updateInteractions
    }

    internal void HitWithSword(int collisionType, int damage)
    {
        // Declared overlapping ITEM, then the unmodified bank-$07 scan and
        // collision effect. The wrapper only supplies the caller registers.
        this[0xd724] = (byte)(0x80 | collisionType);
        this[0xd728] = unchecked((byte)-damage);
        this[0xd726] = this[0xd727] = 6;
        this[0xd70b] = this[0xd08b];
        this[0xd70d] = (byte)(this[0xd08d] - 1);
        this[0xffae] = 0x80;
        this[0xffaf] = 0xd0;
        byte[] caller = [0x16, 0xd0, 0x21, 0x00, 0xd6, 0xcd, 0xd1, 0x41, 0xc9];
        caller.CopyTo(_memory, 0xc100);
        Call(0xc100, 7);
        this[0xd724] = 0;
    }

    private void Call(int entry, int bank)
    {
        _bank = bank;
        this[0xff97] = (byte)bank;
        _cpu.RunCall(entry);
    }
    private int Read(int address)
    {
        if (address < 0x4000) return _rom.Span[address];
        if (address < 0x8000 && _bank is >= 0 and < 0x40)
            return _rom.Span[_bank * 0x4000 + address - 0x4000];
        if (address is >= 0xd000 and < 0xe000 && this[0xff70] > 1)
            return _wram[this[0xff70]][address - 0xd000];
        if (Allowed(address)) return this[address];
        throw new InvalidDataException($"Enemy status ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }
    private void Write(int address, int value)
    {
        if (address == 0x2222) { _bank = value; return; }
        if (address == 0xff94) RandomCalls++;
        if (address is >= 0xc0a0 and <= 0xc0af) Sounds.Add(value);
        if (address is >= 0xd000 and < 0xe000 && this[0xff70] > 1)
        { _wram[this[0xff70]][address - 0xd000] = (byte)value; return; }
        if (Allowed(address)) { this[address] = (byte)value; return; }
        throw new InvalidDataException($"Enemy status ROM ${_bank:x2}:${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }
    private static bool Allowed(int address) => address is >= 0xc000 and < 0xe000 or >= 0xff80 and < 0xffc0 or 0xff70;
}
