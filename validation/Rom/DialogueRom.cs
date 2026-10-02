using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// Bounded clean-US text thread. Compressed ROM text, dictionary calls,
// controls, glyph composition and tile mapping execute unchanged instructions.
// showText's thread-restart outputs are declared when opening directly; NPC
// comparisons instead let updateInteractions call showText and run its script.
internal sealed class DialogueRom
{
    private readonly FrontendRom _rom = new();
    private bool _initialized;
    internal byte this[int address] { get => _rom[address]; set => _rom[address] = value; }
    internal byte Text(int address) => _rom.BankByte(7, address);
    internal IReadOnlyList<int> Sounds => _rom.Sounds;
    internal int RandomCalls => _rom.RandomCalls;
    internal int Word(int address) => _rom.Word(address);
    internal int State => Text(0xd0c0);
    internal bool Active => this[0xcba0] != 0;
    internal int GlyphCount => Enumerable.Range(0, 0xc0)
        .Count(offset => Text(0xd000 + offset) is >= 0x40 and < 0x80 && (Text(0xd000 + offset) & 1) == 0);

    internal DialogueRom(OracleSaveData save)
    {
        for (int address = 0xc5b0; address < 0xcb00; address++) this[address] = save.ReadWramByte(address);
        this[0xcc2b] = 0xff;
        this[0xcd00] = 1;
        this[0xd000] = this[0xd004] = 1;
        this[0xd024] = 0x80;
        this[0xd029] = 1;
        this[0xd026] = this[0xd027] = 6;
    }

    internal void Open(int textId, int speed, int linkY, int flags = 0)
    {
        this[0xc629] = (byte)speed;
        this[0xd00b] = (byte)linkY;
        this[0xcba0] = 1; this[0xcba1] = 0;
        this[0xcba2] = (byte)textId;
        this[0xcba3] = this[0xcba4] = (byte)((textId >> 8) + 4);
        this[0xcba5] = 0xff; this[0xcba6] = 2; this[0xcba7] = 0x98;
        this[0xcbae] = (byte)flags;
        _initialized = false;
    }

    internal void AdvanceText(int pressed, int held)
    {
        this[0xc482] = (byte)pressed; this[0xc481] = (byte)held;
        if (!Active) { _initialized = false; return; }
        if (!_initialized)
        {
            _rom.Call(0x4af7, 0x3f); // initTextbox, immediately before the first update.
            _initialized = true;
        }
        _rom.Call(0x4b1f, 0x3f); // updateTextbox, after gameplay/object passes.
        this[0xff70] = 0;
    }

    internal void InitializeGameplay(OracleRoomData room, OracleRandomState random, int x, int y)
    {
        this[0xff94] = random.Rng1; this[0xff95] = random.Rng2;
        this[0xcc2d] = (byte)room.Group; this[0xcc30] = (byte)room.Id; this[0xcc2e] = 1;
        this[0xcc33] = (byte)room.ActiveCollisions; this[0xcc34] = room.TilesetFlags;
        this[0xcc86] = (byte)room.Height; this[0xcc87] = (byte)room.Width;
        this[0xd00b] = (byte)y; this[0xd00d] = (byte)x;
        this[0xcc39] = this[0xccaa] = 0xff; this[0xcc6a] = 20;
        for (int tile = 0; tile < 256; tile++)
            _rom.SetBankByte(3, 0xdb00 + tile, room.GetCollision((byte)tile));
        for (int row = 0; row < room.HeightInTiles; row++)
        for (int column = 0; column < room.WidthInTiles; column++)
        {
            Godot.Vector2 point = new(column * 16 + 8, row * 16 + 8);
            int packed = row * 16 + column;
            this[0xcf00 + packed] = room.GetMetatile(point);
            this[0xce00 + packed] = (byte)room.GetTerrainInfo(point).Collision;
            _rom.SetBankByte(3, 0xdf00 + packed, room.GetUnderlyingStorageMetatile(row * room.WidthInTiles + column));
        }
        for (int column = 0; column < 16; column++)
            this[0xcef0 + column] = this[0xce00 + room.Height + column] = 0xff;
        for (int row = 0; row < 11; row++)
            this[0xce0f + row * 16] = this[0xce00 + (room.Width >> 4) + row * 16] = 0xff;
    }

    internal void AddBird(int slot, int subid, int x, int y)
    {
        int address = 0xd040 + slot * 0x100;
        this[address] = 1; this[address + 1] = 0xe3; this[address + 2] = (byte)subid;
        this[address + 0x0b] = (byte)y; this[address + 0x0d] = (byte)x;
    }

    internal void AdvanceGameplay(int pressed, int held, int angle, int frameCounter)
    {
        this[0xff70] = 0;
        this[0xcc00] = (byte)frameCounter;
        this[0xcc29] = (byte)held; this[0xcc2a] = (byte)pressed; this[0xcc2b] = (byte)angle;
        this[0xcc64] = this[0xcc92] = this[0xcc66] = 0;
        this[0xcc95] |= 0x7f; this[0xcc60] &= 0x7f;
        CallLink(0x49b6); // native Link includes A-button-sensitive object selection.
        CallLink(0x4279);
        this[0xcc61] &= 0x0f; this[0xd02a] = this[0xcc67] = this[0xccd8] = 0;
        if (this[0xcc6b] != 0) this[0xcc6b]--;
        _rom.Call(0x4872, 7);
        _rom.Call(0x3b36, 0); // native bird dispatch AND interactionRunScript.
        _rom.Call(0x2b25, 0);
        _rom.Call(0x491a, 7);
        AdvanceText(pressed, held);
    }

    private void CallLink(int entry)
    {
        this[0xffae] = 0; this[0xffaf] = 0xd0;
        byte[] caller = [0x16, 0xd0, 0x62, 0xcd, (byte)entry, (byte)(entry >> 8), 0xc9];
        for (int index = 0; index < caller.Length; index++) this[0xc100 + index] = caller[index];
        _rom.Call(0xc100, 5);
    }
}
