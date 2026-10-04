using Godot;
using System.Collections.Generic;

namespace oracleofages;

// Bounded clean-US Link dispatch followed by the actual ordered interaction
// pass. Native terrain, ladder, swim, pit, platform and bubble code executes;
// the fixture supplies a room, save, RNG and host input, without CPU timing.
internal sealed class SideViewRom
{
    private readonly FrontendRom _rom = new();
    internal byte this[int address] { get => _rom[address]; set => _rom[address] = value; }
    internal int Word(int address) => _rom.Word(address);
    internal void Word(int address, int value)
    {
        this[address] = unchecked((byte)value);
        this[address + 1] = unchecked((byte)(value >> 8));
    }
    internal Vector2 Position => new(Word(0xd00c) / 256f, Word(0xd00a) / 256f);
    internal int SpeedZ => unchecked((short)Word(0xd014));
    internal IReadOnlyList<int> Sounds => _rom.Sounds;
    internal int RandomCalls => _rom.RandomCalls;
    internal int InitialRandomCalls { get; }

    internal void ApplyLinkDamage(byte rawDamage)
    {
        this[0xd025] = rawDamage;
        CallLink(0x46bb, 6); // linkApplyDamage, after the declared contact caller.
    }

    internal SideViewRom(OracleSaveData save, OracleRandomState random,
        OracleRoomData room, Vector2 position, int frameCounter)
    {
        InitialRandomCalls = random.Calls;
        for (int address = 0xc5b0; address < 0xcb00; address++) this[address] = save.ReadWramByte(address);
        this[0xff94] = random.Rng1; this[0xff95] = random.Rng2;
        this[0xcc00] = (byte)frameCounter;
        this[0xcc2d] = (byte)room.Group; this[0xcc30] = (byte)room.Id; this[0xcc2e] = 1;
        this[0xcc33] = (byte)room.ActiveCollisions; this[0xcc34] = room.TilesetFlags;
        this[0xcc86] = (byte)room.Height; this[0xcc87] = (byte)room.Width;
        this[0xcc39] = this[0xccaa] = 0xff;
        this[0xcc08] = 0x78; this[0xcc09] = this[0xcd00] = 1;
        this[0xd000] = this[0xd004] = this[0xd029] = 1;
        this[0xd024] = 0x80; this[0xd026] = this[0xd027] = 6;
        this[0xd01a] = 0x81;
        this[0xd009] = this[0xcc2b] = 0xff;
        Word(0xd00c, (int)(position.X * 256)); Word(0xd00a, (int)(position.Y * 256));
        this[0xcc21] = (byte)position.Y; this[0xcc22] = (byte)position.X;
        for (int tile = 0; tile < 256; tile++) _rom.SetBankByte(3, 0xdb00 + tile, room.GetCollision((byte)tile));
        for (int x = 0; x < 16; x++)
            this[0xcef0 + x] = this[0xce00 + room.HeightInTiles * 16 + x] = 0xff;
        for (int y = 0; y < 11; y++)
            this[0xce0f + y * 16] = this[0xce00 + room.WidthInTiles + y * 16] = 0xff;
        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
        {
            Vector2 point = new(x * 16 + 8, y * 16 + 8);
            int offset = y * 16 + x;
            this[0xcf00 + offset] = room.GetMetatile(point);
            this[0xce00 + offset] = (byte)room.GetTerrainInfo(point).Collision;
            _rom.SetBankByte(3, 0xdf00 + offset, room.GetUnderlyingMetatile(point));
        }
        CallLink(0x2b0a, 0, 0x10); // specialObjectSetAnimation WALK
    }

    internal void AddPlatform(int page, int subid, Vector2 position)
    {
        int slot = page << 8;
        this[slot + 0x40] = 1; this[slot + 0x41] = 0xa1; this[slot + 0x42] = (byte)subid;
        Word(slot + 0x4a, (int)(position.Y * 256)); Word(slot + 0x4c, (int)(position.X * 256));
    }

    internal void Update(int angle = 0xff, int pressed = 0, int held = 0)
    {
        this[0xcc00]++;
        this[0xcc29] = (byte)held; this[0xcc2a] = (byte)pressed; this[0xcc2b] = (byte)angle;
        // code/specialObjects.s: Link consumes the preceding interaction rider
        // before the tail clears it. Climbing is cleared before Link dispatch.
        _rom.UpdateSpecialObjectPrelude();
        this[0xcc68] = 0; // After the omitted empty companion slot.
        CallLink(0x49b6);
        CallLink(0x4279);
        this[0xcc96] = this[0xcc98];
        this[0xcc61] &= 0x0f;
        this[0xd02a] = this[0xcc67] = this[0xccd8] = 0;
        _rom.Call(0x3b36, 0); // updateInteractions: native text/freeze/state0 gates
    }

    private void CallLink(int address, int bank = 5, int accumulator = 0)
    {
        this[0xffae] = 0; this[0xffaf] = 0xd0;
        byte[] caller = [0x16, 0xd0, 0x62, 0x3e, (byte)accumulator,
            0xcd, (byte)address, (byte)(address >> 8), 0xc9];
        for (int index = 0; index < caller.Length; index++) this[0xc100 + index] = caller[index];
        _rom.Call(0xc100, bank);
    }
}
