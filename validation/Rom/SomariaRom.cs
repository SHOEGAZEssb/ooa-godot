using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// Clean-US parent dispatch, item update and unconditional post pass in original
// object order. Room bytes and Link's ground state are explicit inputs; graphics,
// block allocation, tile writes/restoration and puff creation execute native code.
internal sealed class SomariaRom
{
    private readonly FrontendRom _rom = new();
    internal byte this[int address] { get => _rom[address]; set => _rom[address] = value; }
    internal int RandomCalls => _rom.RandomCalls;
    internal IReadOnlyList<int> Sounds => _rom.Sounds;
    internal int[] Blocks => Enumerable.Range(0xd7, 5).Select(page => page << 8)
        .Where(slot => this[slot] != 0 && this[slot + 1] == 0x18).ToArray();
    internal int Word(int address) => _rom.Word(address);
    internal byte Underlying(int packed) => _rom.BankByte(3, 0xdf00 + packed);

    internal SomariaRom(OracleSaveData save, OracleRandomState random, OracleRoomData room,
        int direction, int x, int y)
    {
        for (int address = 0xc5b0; address < 0xcb00; address++) this[address] = save.ReadWramByte(address);
        this[0xff94] = random.Rng1; this[0xff95] = random.Rng2;
        this[0xcc2b] = 0xff;
        this[0xcc2d] = (byte)room.Group; this[0xcc30] = (byte)room.Id; this[0xcc2e] = 1;
        this[0xcc33] = (byte)room.ActiveCollisions; this[0xcc34] = room.TilesetFlags;
        this[0xcc86] = (byte)room.Height; this[0xcc87] = (byte)room.Width;
        this[0xcd00] = 1;
        this[0xd000] = 1; this[0xd004] = 1; this[0xd024] = 0x80;
        this[0xd029] = 1;
        this[0xd026] = this[0xd027] = 6;
        this[0xd008] = (byte)direction; this[0xd009] = (byte)(direction * 8);
        this[0xd00b] = (byte)y; this[0xd00d] = (byte)x;
        for (int tile = 0; tile < 256; tile++)
            _rom.SetBankByte(3, 0xdb00 + tile, room.GetCollision((byte)tile));
        CopyRoom(room);
    }

    internal void Update(int pressed, int held, int frameCounter)
    {
        this[0xcc00] = (byte)frameCounter;
        this[0xcc29] = (byte)held; this[0xcc2a] = (byte)pressed;
        this[0xcc99] = (byte)((this[0xd00b] & 0xf0) | (this[0xd00d] >> 4));
        if (this[0xcba0] == 0 && (this[0xcc8a] & 0x81) == 0)
            _rom.Call(0x48b3, 6); // checkUseItems, including parent pass.
        _rom.Call(0x4872, 7); // updateItems: initialized children obey native freeze gates.
        _rom.Call(0x3b36, 0); // updateInteractions, including block-deletion puffs.
        _rom.Call(0x491a, 7); // updateItemsPost: runs even while parents/items are frozen.
    }

    internal void ClearPhysicalItems() => _rom.Call(0x19ad, 0);

    internal void CopyRoom(OracleRoomData room)
    {
        for (int row = 0; row < room.HeightInTiles; row++)
        for (int column = 0; column < room.WidthInTiles; column++)
        {
            int packed = row * 16 + column;
            Godot.Vector2 point = new(column * 16 + 8, row * 16 + 8);
            this[0xcf00 + packed] = room.GetMetatile(point);
            this[0xce00 + packed] = (byte)room.GetTerrainInfo(point).Collision;
            _rom.SetBankByte(3, 0xdf00 + packed, room.GetUnderlyingStorageMetatile(packed));
        }
    }

    internal void InitializeLinkGameplay()
    {
        this[0xcc39] = this[0xccaa] = 0xff;
        this[0xcc6a] = 20;
        for (int column = 0; column < 16; column++)
            this[0xcef0 + column] = this[0xce00 + this[0xcc86] + column] = 0xff;
        for (int row = 0; row < 11; row++)
            this[0xce0f + row * 16] = this[0xce00 + (this[0xcc87] >> 4) + row * 16] = 0xff;
    }

    internal void UpdateGameplay(int pressed, int held, int angle, int frameCounter)
    {
        this[0xcc00] = (byte)frameCounter;
        this[0xcc29] = (byte)held; this[0xcc2a] = (byte)pressed; this[0xcc2b] = (byte)angle;
        // updateSpecialObjects preparation/tail, with no companion or physical
        // controller input. The actual Link dispatch owns tile interaction,
        // item parents, wall probes, movement, facing and grab eligibility.
        this[0xcc64] = this[0xcc92] = this[0xcc66] = 0;
        this[0xcc95] |= 0x7f;
        this[0xcc60] &= 0x7f;
        CallLink(0x49b6);
        CallLink(0x4279); // updateLinkInvincibilityCounter.
        this[0xcc61] &= 0x0f;
        this[0xd02a] = this[0xcc67] = this[0xccd8] = 0;
        if (this[0xcc6b] != 0) this[0xcc6b]--;
        // Clear only after Link has read the preceding publication. ITEM$18
        // state3 republishes later in this update, including after a push.
        for (int address = 0xcc74; address < 0xcc84; address++) this[address] = 0;
        _rom.Call(0x4872, 7);
        _rom.Call(0x3b36, 0);
        if ((this[0xcc5a] & 0x80) != 0) _rom.Call(0x54df, 6);
        // This late graphics pass publishes wLinkPushingDirection for the
        // following Link update, using its retained adjacent-wall probes.
        _rom.Call(0x2b25, 0);
        _rom.Call(0x491a, 7);
    }

    private void CallLink(int address)
    {
        this[0xffae] = 0; this[0xffaf] = 0xd0;
        // Supply D/H as updateSpecialObjects does; no native instruction is
        // replaced. FrontendRom keeps the stack in unbanked WRAM.
        byte[] caller = [0x16, 0xd0, 0x62, 0xcd, (byte)address, (byte)(address >> 8), 0xc9];
        for (int index = 0; index < caller.Length; index++) this[0xc100 + index] = caller[index];
        _rom.Call(0xc100, 5);
    }
}
