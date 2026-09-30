using Godot;
using System.Collections.Generic;

namespace oracleofages;

// Clean-US shared Bracelet parent, reserved $dc child, native wall probes and
// Link movement, late grabbed-position attachment, and item post pass.
internal sealed class BraceletRom
{
    private readonly LinkCollisionRom _rom = new(bankedStack: true);
    internal IReadOnlyList<int> Sounds => _rom.SoundRequests;
    internal byte this[int address] { get => _rom[address]; set => _rom[address] = value; }
    internal int Word(int address) => _rom.Word(address);
    internal void Word(int address, int value) => _rom.Word(address, value);
    internal byte Underlying(int offset) => _rom.BankByte(3, 0xdf00 + offset);
    internal bool ChildActive => this[0xdc00] != 0;

    internal BraceletRom(OracleRoomData room, Vector2 link, int direction, int ring, int level = 1)
    {
        this[0xcc2c] = 0xd0;
        this[0xc6cb] = (byte)ring;
        this[0xc6b8] = (byte)level;
        this[0xcc08] = 0x78;
        this[0xcc09] = 1;
        this[0xcc30] = (byte)room.Id;
        this[0xcc33] = (byte)room.ActiveCollisions;
        this[0xcc34] = (byte)room.TilesetFlags;
        this[0xcc86] = (byte)room.Height;
        this[0xcc87] = (byte)room.Width;
        this[0xcc39] = this[0xccaa] = 0xff;
        this[0xd000] = this[0xd004] = 1;
        this[0xd008] = (byte)direction;
        this[0xd009] = (byte)(direction * 8);
        this[0xd024] = 0x80;
        this[0xd026] = this[0xd027] = 6;
        Word(0xd00c, (int)(link.X * 256));
        Word(0xd00a, (int)(link.Y * 256));
        for (int tile = 0; tile < 256; tile++) _rom.Bank(3, 0xdb00 + tile, room.GetCollision((byte)tile));
        CopyRoom(room);
        Probe();
    }

    internal void CopyRoom(OracleRoomData room)
    {
        // loadRoomCollisions.@blankDataAroundCollisions installs wrapped
        // top/left edges and the small/large bottom/right padding.
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

    internal void Probe() => _rom.Call(LinkCollisionRom.Probe);
    internal void Walk(int angle)
    {
        _rom.Call(LinkCollisionRom.Probe, angle: angle);
        _rom.Call(LinkCollisionRom.Move, angle: angle);
    }
    internal void Call(int bank, int address) => _rom.Call(address, bank: bank);
    internal void BeginSmasherBallThrow(Vector2 position, int z, int angle)
    {
        // Declared handoff from parent state3: the related native enemy has
        // just entered grabbed substate2, and the new physical child starts
        // with zero low position bytes (objectCopyPosition_rawAddress).
        this[0xd080] = 3;
        this[0xd081] = 0x74;
        this[0xd084] = 2;
        this[0xd085] = 2;
        this[0xd0a6] = this[0xd0a7] = 6;
        this[0xd0a9] = 5;
        this[0xd0b0] = 0xff;
        Word(0xd098, 0xd180);
        this[0xd1ab] = 0x40; // Declared invulnerable parent, no boss hit branch.
        this[0xdc00] = 1;
        this[0xdc01] = 0x16;
        this[0xdc09] = (byte)angle;
        this[0xdc38] = 0x20;
        Word(0xdc18, 0xd080);
        Word(0xdc0a, (int)position.Y * 256);
        Word(0xdc0c, (int)position.X * 256);
        Word(0xdc0e, z * 256);
    }
    internal void Update(int angle = 0xff, bool held = false, bool pressed = false,
        bool primary = true, bool moveLink = false, bool otherPressed = false, bool updateEnemies = false)
    {
        this[primary ? 0xc689 : 0xc688] = 0x16;
        this[primary ? 0xc688 : 0xc689] = 0;
        byte button = (byte)(primary ? 1 : 2);
        int directionKey = angle switch { 0 => 0x40, 8 => 0x10, 16 => 0x80, 24 => 0x20, _ => 0 };
        this[0xcc29] = (byte)(directionKey | (held ? button : 0));
        this[0xcc2a] = (byte)((pressed ? button : 0) | (otherPressed ? primary ? 2 : 1 : 0));
        this[0xcc2b] = (byte)angle;
        if (this[0xcba0] == 0)
        {
            Call(6, 0x48b3); // checkUseItems, including original parent dispatch.
            if (moveLink && this[0xcc61] == 0 && angle < 0x20)
            {
                Walk(angle);
            }
        }
        Call(7, 0x4872); // updateItems includes the reserved Bracelet child.
        // bank0.updateEnemies intentionally leaves the last enemy code bank
        // selected, unlike the bank-restoring bounded geometry calls.
        if (updateEnemies) _rom.Call(0x2ea5, bank: 0, restoresBank: false);
        if ((this[0xcc5a] & 0x80) != 0) Call(6, 0x54df);
        Call(7, 0x491a);
    }
}
