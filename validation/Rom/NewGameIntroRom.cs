using System.Collections.Generic;
using System;
using System.Linq;

namespace oracleofages;

// Main-thread game dispatch, original object passes and the following text
// thread. LCD-off execution drains graphics synchronously; no instruction or
// interrupt timing is projected onto the port's fixed 60-update clock.
internal sealed class NewGameIntroRom
{
    private readonly FrontendRom _rom = new();
    private readonly ReadOnlyMemory<byte> _data = ValidationRom.LoadCleanUs();
    private bool _textInitialized;
    internal byte this[int address] => _rom[address];
    internal int Word(int address) => _rom.Word(address);
    internal byte TextByte(int address) => _rom.BankByte(7, address);
    internal IReadOnlyList<int> Sounds => _rom.Sounds;
    internal int RandomCalls => _rom.RandomCalls;
    internal int GlyphCount => Enumerable.Range(0, 0xc0)
        .Count(offset => TextByte(0xd000 + offset) is >= 0x40 and < 0x80 && (TextByte(0xd000 + offset) & 1) == 0);
    internal string TextLine => new(Enumerable.Range(0, 16).Select(offset => TextByte(0xd400 + offset))
        .TakeWhile(value => value != 0).Select(value => (char)value).ToArray());

    internal IntroOamPart[] OamParts(int address)
    {
        // bank0.s:objectDraw derives the OAM bank from the two high pointer
        // bits. The remaining 14 bits address the source record in that bank.
        int pointer = Word(address + 0x1e);
        int offset = (0x13 + (pointer >> 14)) * 0x4000 + (pointer & 0x3fff);
        int count = _data.Span[offset++];
        var parts = new IntroOamPart[count];
        for (int index = 0; index < count; index++, offset += 4)
            parts[index] = new(_data.Span[offset], _data.Span[offset + 1], _data.Span[offset + 2], _data.Span[offset + 3]);
        return parts;
    }

    internal int LinkSourceOffset
    {
        get
        {
            // bank6.getSpecialObjectGraphicsFrame: source-ordered three-byte
            // records from specialObjectGraphicsTable at $06:$4451. The last
            // actually loaded frame is SpecialObject.var32, not its pending
            // animation frame during an invisible flicker update.
            int pointerOffset = 6 * 0x4000 + 0x4451 - 0x4000 + this[0xd001] * 2;
            int pointer = _data.Span[pointerOffset] | _data.Span[pointerOffset + 1] << 8;
            int record = 6 * 0x4000 + pointer - 0x4000 + this[0xd032] * 3;
            int source = _data.Span[record + 1] | _data.Span[record + 2] << 8;
            return (source & 0xffe0) - 0x4000 + ((source & 1) << 14);
        }
    }

    internal NewGameIntroRom(OracleSaveData save, OracleRandomState random)
    {
        for (int address = 0xc5b0; address < 0xcb00; address++)
            _rom[address] = save.ReadWramByte(address);
        _rom[0xff94] = random.Rng1;
        _rom[0xff95] = random.Rng2;
        Update(); // initializeGame -> cutscene0d -> pregame state $0a.
    }

    internal void Update(int pressed = 0, int held = 0)
    {
        // bank0.s:mainThreadStart increments all four bytes before dispatch;
        // wFrameCounter is its low byte, including the initialization update.
        for (int address = 0xc622; address <= 0xc625; address++)
            if (++_rom[address] != 0) break;
        _rom[0xcc00] = _rom[0xc622];
        _rom[0xc482] = (byte)pressed;
        _rom[0xc481] = (byte)held;
        _rom[0xff70] = 0;
        _rom.Call(0x596a, 1); // bank1.runGameLogic, including updateAllObjects.
        if (_rom[0xcba0] != 0)
        {
            if (!_textInitialized)
            {
                _rom.Call(0x4af7, 0x3f); // initTextbox.
                _textInitialized = true;
            }
            _rom.Call(0x4b1f, 0x3f); // updateTextbox, after the main/object pass.
        }
        else _textInitialized = false;
        _rom.AdvancePalette();
    }
}
