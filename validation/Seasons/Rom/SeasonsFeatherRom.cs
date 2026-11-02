
namespace Oracles;

// Clean US Seasons addresses from the pinned linked symbols. Execute the
// whole Link dispatch, including parent allocation, gravity and animation.
internal sealed class SeasonsFeatherRom
{
    private readonly LinkCollisionRom _rom = new(bankedStack:true,game:OracleGame.Seasons);
    internal byte this[int address] { get => _rom[address]; set => _rom[address] = value; }
    internal int ZFixed => unchecked((short)_rom.Word(0xd00e));
    internal int SpeedZ => unchecked((short)_rom.Word(0xd014));
    internal bool Airborne => this[0xcc77] != 0;
    internal IReadOnlyList<int> Sounds => _rom.SoundRequests;
    internal void PutLinkOnGround() => _rom.Call(0x1986,bank:0);

    internal SeasonsFeatherRom(OracleRoomData room,Vector2 position,int level,bool primary)
    {
        this[0xcc49] = (byte)room.Group; this[0xcc4c] = (byte)room.Id;
        this[0xcc4f] = (byte)room.ActiveCollisions; this[0xcc50] = (byte)room.TilesetFlags;
        this[0xcd0a] = (byte)room.Width; this[0xcd0b] = (byte)room.Height;
        this[0xcca0] = (byte)room.Height; this[0xcca1] = (byte)room.Width;
        this[0xcc4a] = room.WidthInTiles > 10 ? (byte)1 : (byte)0;
        this[0xcc07] = 0x5c; this[0xcc08] = 1;
        this[0xcc47] = this[0xccc5] = 0xff;
        this[0xc6a2] = this[0xc6a3] = 12; this[0xc6b4] = (byte)level;
        this[primary ? 0xc681 : 0xc680] = 0x17;
        this[0xd000] = this[0xd004] = this[0xd029] = 1;
        this[0xd009] = 0xff; this[0xd024] = 0x80; this[0xd026] = this[0xd027] = 6;
        _rom.Word(0xd00c,(int)(position.X * 256)); _rom.Word(0xd00a,(int)(position.Y * 256));
        this[0xcc3d] = (byte)position.Y; this[0xcc3e] = (byte)position.X;
        for (int tile = 0; tile < 256; tile++) _rom.Bank(3,0xdb00 + tile,room.GetCollision((byte)tile));
        for (int x = 0; x < 16; x++) this[0xcef0 + x] = this[0xce00 + room.HeightInTiles * 16 + x] = 0xff;
        for (int y = 0; y < 11; y++) this[0xce0f + y * 16] = this[0xce00 + room.WidthInTiles + y * 16] = 0xff;
        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
        {
            Vector2 point = new(x * 16 + 8,y * 16 + 8);
            int index = y * 16 + x;
            this[0xcf00 + index] = room.GetMetatile(point);
            this[0xce00 + index] = (byte)room.GetTerrainInfo(point).Collision;
            _rom.Bank(3,0xdf00 + index,room.GetUnderlyingMetatile(point));
        }
        _rom.Call(0x2a51,bank:0,accumulator:0x10);
    }

    internal void Update(bool held,bool pressed,bool primary)
    {
        byte button = (byte)(primary ? 1 : 2);
        UpdateKeys(held ? button : 0,pressed ? button : 0);
    }

    internal void UpdateKeys(int held,int pressed)
    {
        this[0xcc45] = (byte)held;
        this[0xcc46] = (byte)pressed;
        this[0xcc00]++;
        this[0xccaf] |= 0x7f; this[0xcc7b] &= 0x7f; this[0xccb2] = 0;
        _rom.Call(0x496f);
        _rom.Call(0x4237);
        this[0xcc7c] &= 0x0f;
        this[0xd02a] = this[0xcc82] = this[0xccef] = 0;
    }
}
