using System;
using System.IO;
using System.Reflection;
using Godot;

namespace oracleofages;

// Executes setTile, setTileInAllBuffers and tilesets.updateChangedTileQueue.
// Observe the prepared WRAM tilemaps and VBlank commands, not LCD bus timing.
internal sealed class TileQueueRom
{
    private readonly byte[] _rom = ValidationRom.LoadCleanUs();
    private readonly byte[] _memory = new byte[0x10000];
    private readonly byte[][] _wram = [new byte[0x1000], new byte[0x1000], new byte[0x1000], new byte[0x1000]];
    private readonly OracleCpu _cpu;
    private byte[] _caller = [];
    private int _bank = 4;
    internal TileQueueRom()
    {
        _cpu = new OracleCpu(Read, Write, static _ => { },
            (pc, detail) => new InvalidDataException($"Tile queue ROM ${_bank:x2}:${pc:x4}: {detail}."));
    }
    internal byte this[int address] { get => _memory[address]; set => _memory[address] = value; }
    internal byte Bank(int bank, int address) => _wram[bank][address - 0xd000];
    internal void Bank(int bank, int address, byte value) => _wram[bank][address - 0xd000] = value;
    internal int Count => (this[0xcce0] - this[0xccdf]) & 31;
    internal void SeedRoom(OracleRoomData room)
    {
        var mappings = (byte[])typeof(OracleRoomData).GetField("_mappings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(room)!;
        Array.Copy(mappings, 0, _wram[3], 0, 0x800);
        for (int tile = 0; tile < 256; tile++) Bank(3, 0xdb00 + tile, room.GetCollision((byte)tile));
        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
        {
            int packed = y * 16 + x;
            Vector2 point = new(x * 16 + 8, y * 16 + 8);
            this[0xcf00 + packed] = room.GetMetatile(point);
            this[0xce00 + packed] = room.GetTerrainInfo(point).Collision;
            Bank(3, 0xdf00 + packed, room.GetUnderlyingMetatile(point));
            for (int q = 0; q < 4; q++)
            {
                int sx = x * 2 + q % 2, sy = y * 2 + q / 2;
                Bank(3, 0xd800 + sy * 32 + sx, room.GetBackgroundSubtileForValidation(sx, sy));
                Bank(3, 0xdc00 + sy * 32 + sx, room.GetBackgroundAttributeForValidation(sx, sy));
            }
        }
    }
    internal void Reset(int cursor = 0)
    {
        Array.Clear(_memory); foreach (var bank in _wram) Array.Clear(bank);
        this[0xccdf] = this[0xcce0] = (byte)cursor;
        for (int tile = 0; tile < 256; tile++)
        {
            Bank(3, 0xdb00 + tile, (byte)(tile ^ 0x5a));
            for (int i = 0; i < 8; i++) Bank(3, 0xd000 + tile * 8 + i, (byte)(tile + i * 17));
        }
    }
    internal bool SetTile(byte position, byte tile, bool allBuffers = false)
    {
        Call(0, allBuffers ? 0x3ac6 : 0x3a9c, tile, position);
        return (this[0xc201] & 0x80) == 0;
    }
    internal void Drain(byte scrollMode)
    {
        this[0xcd00] = scrollMode;
        // The previous VBlank consumed its command queue. No native tile
        // queue cursor or graphics buffer is cleared at this boundary.
        this[0xffa5] = 0;
        Call(4, 0x6c32);
    }
    internal void Clear(bool scrolling) => Call(1, scrolling ? 0x49c9 : 0x49af);
    private void Call(int bank, int entry, byte a = 0, byte c = 0)
    {
        _bank = bank; this[0xff97] = (byte)bank;
        _caller = [0x31, 0xf0, 0xcf, 0x3e, a, 0x0e, c,
            0xcd, (byte)entry, (byte)(entry >> 8), 0xf5, 0xc1,
            0x79, 0xea, 0x01, 0xc2, 0x31, 0xf0, 0xdf, 0xc9];
        _cpu.RunCall(0xc100);
        if (this[0xff70] != 0) throw new InvalidDataException("Tile queue ROM did not restore WRAM bank zero.");
    }
    private int Read(int address)
    {
        if (address < 0x4000) return _rom[address];
        if (address < 0x8000) return _rom[_bank * 0x4000 + address - 0x4000];
        if (address >= 0xc100 && address < 0xc100 + _caller.Length) return _caller[address - 0xc100];
        if (address is >= 0xd000 and < 0xe000) return _wram[Math.Max(1, (int)this[0xff70])][address - 0xd000];
        if (Allowed(address)) return this[address];
        throw new InvalidDataException($"Tile queue ROM ${_cpu.InstructionAddress:x4}: undeclared read ${address:x4}.");
    }
    private void Write(int address, int value)
    {
        if (address == 0x2222 && value < 0x40) { _bank = value; return; }
        if (address is >= 0xd000 and < 0xe000) { Bank(Math.Max(1, (int)this[0xff70]), address, (byte)value); return; }
        if (Allowed(address)) { this[address] = (byte)value; return; }
        throw new InvalidDataException($"Tile queue ROM ${_cpu.InstructionAddress:x4}: undeclared write ${address:x4}.");
    }
    private static bool Allowed(int address) => address is >= 0xc000 and < 0xd000 or >= 0xff80 and < 0xffc0 or 0xff70;
}
