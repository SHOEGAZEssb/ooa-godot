using Godot;

namespace oracleofages;

// Native bank5 species dispatch, shared collision/movement and the post-object
// func_410d rider synchronization. Animation code executes normally.
internal sealed class CompanionRom
{
    private readonly LinkCollisionRom _rom = new(bankedStack: true);
    private readonly int _entry;
    internal CompanionRom(int id, Vector2 position, int direction, OracleRoomData room)
    {
        _entry = id switch { 0x0b => 0x6d1e, 0x0c => 0x7382, _ => 0x7865 };
        this[0xd100] = 1; this[0xd101] = (byte)id; this[0xd104] = 5;
        this[0xd108] = (byte)direction; this[0xd109] = 0xff;
        this[0xd139] = 0x10; this[0xd13c] = 1;
        this[0xd000] = 1; this[0xd001] = 9; this[0xd004] = 1;
        this[0xcc2c] = 0xd1; this[0xcc96] = 1; this[0xccaa] = 0xff;
        this[0xcc30] = (byte)room.Id; this[0xcc33] = (byte)room.ActiveCollisions;
        this[0xffae] = 0; this[0xcd00] = 1;
        this[0xcd0c] = 0xa0; this[0xcd0d] = 0x80;
        this[0xd024] = 0x80; this[0xd124] = 0x80;
        this[0xc6aa] = this[0xc6ab] = 12; this[0xd029] = 1;
        for (int tile = 0; tile < 256; tile++) _rom.Bank(3, 0xdb00 + tile, room.GetCollision((byte)tile));
        _rom.Word(0xd10c, (int)(position.X * 256)); _rom.Word(0xd10a, (int)(position.Y * 256));
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            bool inside = x < room.WidthInTiles && y < room.HeightInTiles;
            Vector2 point = new(x * 16 + 8, y * 16 + 8);
            this[0xcf00 + y * 16 + x] = inside ? room.GetMetatile(point) : (byte)0;
            _rom.Bank(3, 0xdf00 + y * 16 + x, inside ? room.GetUnderlyingMetatile(point) : (byte)0);
            this[0xce00 + y * 16 + x] = inside ? room.GetTerrainInfo(point).Collision : (byte)0xff;
        }
        int animation = (id == 0x0b ? 0x20 : id == 0x0c ? 0 : 0x13) + direction;
        _rom.Call(0x2b0a, objectPage: 0xd1, accumulator: animation);
        _rom.Call(0x410d, objectPage: 0xd1);
    }
    internal byte this[int address] { get => _rom[address]; set => _rom[address] = value; }
    internal System.Collections.Generic.IReadOnlyList<int> Sounds => _rom.SoundRequests;
    internal int SignedWord(int address) => unchecked((short)_rom.Word(address));
    internal void Word(int address, int value) => _rom.Word(address, value);
    internal void SetAnimation(int animation) => _rom.Call(0x2b0a, objectPage: 0xd1, accumulator: animation);
    internal void SetTile(int x, int y, byte tile, byte collision)
    { this[0xcf00 + y * 16 + x] = tile; this[0xce00 + y * 16 + x] = collision; }
    internal Vector2 Position
    {
        get
        {
            float x = _rom.Word(0xd10c) / 256f, y = _rom.Word(0xd10a) / 256f;
            if (this[0xd104] == 0x0c)
            {
                if (this[0xd108] == 1 && x >= 248) x -= 256;
                if (this[0xd108] == 2 && y >= 248) y -= 256;
            }
            return new(x, y);
        }
    }
    internal Vector2 LinkPixels => new(this[0xd00d], this[0xd00b]);
    internal void WaitForMount(Vector2 linkPosition)
    {
        this[0xd104] = 1; this[0xd13c] = 0;
        this[0xd001] = 0; this[0xd024] = 0x80;
        this[0xcc2c] = 0xd0; this[0xcc96] = 0;
        _rom.Word(0xd00c, (int)(linkPosition.X * 256)); _rom.Word(0xd00a, (int)(linkPosition.Y * 256));
        int animation = this[0xd101] == 0x0b ? 0x17 : (this[0xd101] == 0x0c ? 0x1c : 1) + this[0xd108];
        _rom.Call(0x2b0a, objectPage: 0xd1, accumulator: animation);
    }
    internal void EnterFromFlute(int direction)
    {
        WaitForMount(new(8, 8));
        this[0xd104] = 0x0c; this[0xd103] = 0;
        this[0xd108] = (byte)direction; this[0xd109] = (byte)(direction * 8);
        this[0xc646] = 0x80; this[0xc647] = 0x80; this[0xc648] = 0x80;
    }
    internal void ThrowDimitri(Vector2 heldPosition, int z, Vector2 link, int direction, bool drop)
    {
        WaitForMount(link);
        this[0xd104] = 2; this[0xd105] = 2; this[0xd13f] = 0xff;
        this[0xd108] = this[0xd008] = (byte)direction;
        Word(0xd10c, (int)heldPosition.X * 256); Word(0xd10a, (int)heldPosition.Y * 256);
        Word(0xd10e, z * 256);
        _rom.Call(0x2b0a, objectPage: 0xd1, accumulator: 0x18 + direction);
        this[0xdc00] = 1; this[0xdc01] = 0x16;
        Word(0xdc18, 0xd100);
        Word(0xdc0c, (int)heldPosition.X * 256); Word(0xdc0a, (int)heldPosition.Y * 256);
        Word(0xdc0e, z * 256);
        this[0xdc09] = drop ? (byte)0xff : (byte)(direction * 8);
        this[0xdc38] = 0x40; // dimitriState2Substate0 writes wLinkGrabState2=$40.
    }
    internal void Update(int angle, byte buttons = 0, byte held = 0)
    {
        _rom.SoundRequests.Clear();
        this[0xcc2b] = (byte)angle; this[0xcc2a] = buttons; this[0xcc29] = held;
        this[0xcc00]++;
        _rom.Call(_entry, objectPage: 0xd1);
        this[0xcc98] = 0; // updateSpecialObjects clears this before Link.
        if (this[0xcc2c] == 0xd0 && this[0xcc5c] != 0)
        {
            // companionDismount resets Link to state 0. Its same-pass
            // initialization returns before linkUpdateInAir on that update.
            if (this[0xd004] == 0) this[0xd004] = 1;
            else _rom.Call(0x5af3);
        }
        if (this[0xcc2c] == 0xd1)
        {
            _rom.Call(0x6313); // specialObjectCode_linkRidingAnimal
            _rom.Call(0x4279); // updateLinkInvincibilityCounter
            _rom.Call(0x410d, objectPage: 0xd1);
        }
        else if (this[0xcc5c] == 0 && angle != 0xff)
        {
            _rom.Call(LinkCollisionRom.Probe);
            _rom.Call(LinkCollisionRom.Move, speed: 0x28, angle: angle);
        }
        // Native item slots run after both special objects; later allocations
        // participate in this same pass. No collision outcomes are fabricated.
        for (int page = 0xd6; page <= 0xdf; page++)
        {
            if (this[page << 8] == 0) continue;
            int entry = this[(page << 8) + 1] switch
            {
                0x16 => 0x62c6, 0x28 => 0x5b8c, 0x2a => 0x5b00, 0x2b => 0x514d,
                _ => 0
            };
            if (entry != 0) _rom.Call(entry, bank: 7, objectPage: page);
        }
        for (int page = 0xd0; page <= 0xdf; page++)
        {
            int address = (page << 8) + 0x40;
            if (this[address] == 0 || this[address + 1] > 0x0c) continue;
            this[0xffae] = 0x40;
            _rom.Call(0x4000, bank: 8, objectPage: page); // breakTileDebris.s
        }
        this[0xffae] = 0;
        if (this[0xd080] != 0)
        {
            this[0xffae] = 0x80; this[0xffaf] = 0xd0;
            _rom.Call(ObjectCollisionRom.Scan, bank: 7);
            this[0xffae] = 0;
        }
        _rom.Call(0x427d, bank: 1); // updateScreenShake, including shared RNG draws.
    }
}
