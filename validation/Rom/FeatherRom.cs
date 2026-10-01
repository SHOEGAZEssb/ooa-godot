using Godot;
using System.Collections.Generic;

namespace oracleofages;

// Bounded clean-US Link state dispatch. Parent allocation, terrain, gravity,
// rising/descending movement and landing execute original instructions.
internal sealed class FeatherRom
{
    private readonly LinkCollisionRom _rom = new(bankedStack: true);
    internal byte this[int address] { get => _rom[address]; set => _rom[address] = value; }
    internal int Word(int address) => _rom.Word(address);
    internal void Word(int address, int value) => _rom.Word(address, value);
    internal IReadOnlyList<int> Sounds => _rom.SoundRequests;
    internal Vector2 Position => new(Word(0xd00c) / 256f, Word(0xd00a) / 256f);
    internal int ZFixed => unchecked((short)Word(0xd00e));
    internal int SpeedZ => unchecked((short)Word(0xd014));
    internal bool Airborne => this[0xcc5c] != 0;
    internal void PutLinkOnGround() => _rom.Call(0x19ad, bank: 0); // clearAllItemsAndPutLinkOnGround

    internal FeatherRom(OracleRoomData room, Vector2 link, bool primary)
    {
        this[0xcc2c] = 0xd0;
        this[0xcc2d] = (byte)room.Group;
        this[0xcc2e] = 1;
        this[0xcc30] = (byte)room.Id;
        this[0xcc33] = (byte)room.ActiveCollisions;
        this[0xcc34] = (byte)room.TilesetFlags;
        this[0xcc86] = (byte)room.Height;
        this[0xcc87] = (byte)room.Width;
        this[0xcc39] = this[0xccaa] = 0xff;
        this[0xcc08] = 0x78;
        this[0xcc09] = 1;
        this[0xc6aa] = this[0xc6ab] = 12;
        this[0xc6b9] = 1;
        this[primary ? 0xc689 : 0xc688] = 0x17;
        this[0xd000] = this[0xd004] = this[0xd029] = 1;
        this[0xd008] = 0;
        this[0xd009] = 0xff;
        this[0xd024] = 0x80;
        this[0xd026] = this[0xd027] = 6;
        Word(0xd00c, (int)(link.X * 256));
        Word(0xd00a, (int)(link.Y * 256));
        this[0xcc21] = (byte)link.Y;
        this[0xcc22] = (byte)link.X;
        this[0xcc23] = 0;
        for (int tile = 0; tile < 256; tile++) _rom.Bank(3, 0xdb00 + tile, room.GetCollision((byte)tile));
        CopyRoom(room);
        _rom.Call(0x2b0a, accumulator: 0x10); // LINK_ANIM_MODE_WALK
    }

    internal void CopyRoom(OracleRoomData room)
    {
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
            _rom.Bank(3, 0xdf00 + offset, room.GetUnderlyingMetatile(point));
        }
    }

    internal void Update(int angle = 0xff, bool held = false, bool pressed = false, bool primary = true)
    {
        int directionKeys = angle switch
        {
            0 => 0x40, 4 => 0x50, 8 => 0x10, 12 => 0x90,
            16 => 0x80, 20 => 0xa0, 24 => 0x20, 28 => 0x60, _ => 0
        };
        byte button = (byte)(primary ? 1 : 2);
        this[0xcc29] = (byte)(directionKeys | (held ? button : 0));
        this[0xcc2a] = pressed ? button : (byte)0;
        this[0xcc2b] = (byte)angle;
        this[0xcc00]++;
        // updateSpecialObjects' preparation and tail, with no companion.
        this[0xcc95] |= 0x7f;
        this[0xcc60] &= 0x7f;
        this[0xcc98] = 0;
        _rom.Call(0x49b6); // specialObjectCode_link, including states01/02.
        _rom.Call(LinkCollisionRom.Invincibility);
        this[0xcc61] &= 0x0f;
        this[0xd02a] = this[0xcc67] = this[0xccd8] = 0;
    }
}
