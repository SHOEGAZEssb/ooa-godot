using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// Original checkUseItems (including the parent pass), updateInteractions and
// graphics reload, in bank0.updateAllObjects order. The caller supplies Link's
// ground/input state; this bounded fixture does not execute a timewarp cutscene.
internal sealed class HarpRom
{
    private readonly FrontendRom _rom = new();
    private readonly ReadOnlyMemory<byte> _data = ValidationRom.LoadCleanUs();
    private readonly Dictionary<int, int> _noteOrder = new();
    private readonly int[] _linkTiles = Enumerable.Repeat(-1, 256).ToArray();
    internal byte this[int address] { get => _rom[address]; set => _rom[address] = value; }
    internal int Word(int address) => _rom.Word(address);
    internal int RandomCalls => _rom.RandomCalls;
    internal IReadOnlyList<int> Sounds => _rom.Sounds;
    internal int NoteCount { get; private set; }
    internal int[] Notes => _noteOrder.Keys.Where(address => this[address] != 0 && this[address + 1] == 0xa0)
        .OrderBy(address => _noteOrder[address]).ToArray();
    internal int NoteSerial(int address) => _noteOrder[address];
    internal MenuRom CreateMenuFixture() => new(_rom);

    internal HarpRom(OracleSaveData save, OracleRandomState random, int group, int room, byte flags, int x, int y)
    {
        for (int address = 0xc5b0; address < 0xcb00; address++) this[address] = save.ReadWramByte(address);
        this[0xff94] = random.Rng1; this[0xff95] = random.Rng2;
        this[0xcc2c] = 0xd0;
        this[0xcc2d] = (byte)group; this[0xcc30] = (byte)room;
        this[0xcc34] = flags; this[0xcd00] = 1;
        this[0xd000] = 1; this[0xd004] = 1; this[0xd024] = 0x80;
        this[0xd01a] = 0x81; this[0xd032] = 0xff;
        this[0xd00d] = (byte)x; this[0xd00b] = (byte)y;
    }

    internal void Update(int pressed, int held, int frameCounter)
    {
        this[0xcc00] = (byte)frameCounter;
        this[0xcc29] = (byte)held; this[0xcc2a] = (byte)pressed;
        // linkState01 clears the preceding parent's shared byte only when
        // text and wDisabledObjects & $81 allow its ordinary update.
        if (this[0xcba0] == 0 && (this[0xcc8a] & 0x81) == 0)
        {
            this[0xcc8d] = 0;
            _rom.Call(0x48b3, 6);
        }
        // updateSpecialObjects decrements this after Link's item dispatch.
        if (this[0xcc6b] != 0) this[0xcc6b]--;
        for (int page = 0xd0; page <= 0xdf; page++)
        {
            int address = page * 256 + 0x40;
            if (this[address] != 0 && this[address + 1] == 0xa0 && this[address + 4] == 0)
                _noteOrder[address] = NoteCount++;
        }
        _rom.Call(0x3b36, 0); // updateInteractions, including native eligibility.
        int previousFrame = this[0xd032];
        _rom.Call(0x2b25, 0); // loadLinkAndCompanionAnimationFrame.
        if (previousFrame != this[0xd032])
        {
            int source = LinkGraphicsSource;
            if (source != 0)
                for (int tile = 0; tile < (source & 0x1e); tile++)
                    _linkTiles[tile] = LinkSourceOffset / 16 + tile;
        }
    }

    internal void InitializePortal()
    {
        // Canonical room $0:$cd's source-placed INTERAC_TIMEPORTAL_SPAWNER.
        this[0xd040] = 1; this[0xd041] = 0xe1;
        this[0xd04b] = 0x28; this[0xd04d] = 0x68;
        _rom.Call(0x3b36, 0);
        if (this[0xd044] != 1)
            throw new InvalidOperationException("Harp ROM room $0:$cd portal did not enter dormant state $01.");
    }

    internal IntroOamPart[] LinkParts
    {
        get
        {
            int pointer = Word(0xd01e);
            int offset = (0x13 + (pointer >> 14)) * 0x4000 + (pointer & 0x3fff);
            int count = _data.Span[offset++];
            return Enumerable.Range(0, count).Select(index => new IntroOamPart(
                _data.Span[offset + index * 4], _data.Span[offset + index * 4 + 1],
                SourceTile(_data.Span[offset + index * 4 + 2]), _data.Span[offset + index * 4 + 3])).ToArray();
        }
    }

    private int SourceTile(int tile)
    {
        int source = _linkTiles[tile & 0xfe];
        if (source < 0 || _linkTiles[(tile & 0xfe) + 1] != source + 1)
            throw new InvalidOperationException($"Harp ROM unresolved retained Link tile ${tile:x2}.");
        return source;
    }

    private int LinkGraphicsSource
    {
        get
        {
            // bank6.getSpecialObjectGraphicsFrame, actual loaded var32 frame.
            int pointerOffset = 6 * 0x4000 + 0x451 + this[0xd001] * 2;
            int pointer = _data.Span[pointerOffset] | _data.Span[pointerOffset + 1] << 8;
            int record = 6 * 0x4000 + pointer - 0x4000 + this[0xd032] * 3;
            int source = _data.Span[record + 1] | _data.Span[record + 2] << 8;
            return source;
        }
    }
    private int LinkSourceOffset => (LinkGraphicsSource & 0xffe0) - 0x4000 + ((LinkGraphicsSource & 1) << 14);
}
