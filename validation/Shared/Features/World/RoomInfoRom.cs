namespace Oracles;

// Runs the native allocation predicate and complete interaction dispatcher.
// Graphics are already resident in slot zero; no graphics routine is replaced.
internal sealed class RoomInfoRom
{
    private readonly LinkCollisionRom _rom;
    private readonly bool _ages;
    internal int Slot { get; private set; }
    internal byte this[int offset] => Slot < 0 ? (byte)0 : _rom[0xd040 + Slot * 256 + offset];
    internal RoomInfoRom(OracleSaveData save, OracleRoomData room, int pack, int season, bool full = false)
    {
        _ages = save.Game == OracleGame.Ages;
        _rom = new(game:save.Game);
        for (int address = 0xc5b0; address < 0xcb00; address++) _rom[address] = save.ReadWramByte(address);
        _rom[_ages ? 0xcc2d : 0xcc49] = (byte)room.Group;
        _rom[_ages ? 0xcc34 : 0xcc50] = room.TilesetFlags;
        SetSeason(pack,season);
        _rom[_ages ? 0xcc08 : 0xcc07] = (byte)(_ages ? 0x70 : 0x55);
        _rom[_ages ? 0xcc09 : 0xcc08] = 1;
        _rom[0xcd00] = 1;
        if (full) for (int slot = 2; slot < 16; slot++) _rom[0xd040 + slot * 256] = 1;
        Slot = -1;
    }
    internal void SetSeason(int pack, int season)
    {
        _rom[_ages ? 0xcc45 : 0xcc61] = (byte)pack;
        _rom[_ages ? 0xcc32 : 0xcc4e] = (byte)season;
    }
    internal void SetFreeze(int kind)
    {
        _rom[_ages ? 0xcc8a : 0xcca4] = (byte)(kind == 1 ? 2 : 0);
        _rom[0xcba0] = (byte)(kind == 2 ? 1 : 0);
        _rom[0xcd00] = (byte)(kind == 3 ? 8 : 1);
    }
    internal bool TryDisplay()
    {
        _rom.Call(_ages ? 0x5e7d : 0x5c65,bank:1);
        for (int slot = 2; slot < 16; slot++)
            if (_rom[0xd040 + slot * 256] != 0 && _rom[0xd041 + slot * 256] == 0xe0)
            { Slot = slot; return true; }
        return false;
    }
    internal bool Suppressed => (_rom[(_ages ? 0xc6d0 : 0xc6ca) + (_ages ? 0x16 : 0x2f) / 8] &
        (1 << ((_ages ? 0x16 : 0x2f) & 7))) != 0;
    internal void Update() => _rom.Call(_ages ? 0x3b36 : 0x3b0a,bank:1,restoresBank:false);
    internal void BeginScroll()
    {
        _rom.Call(_ages ? 0x49d7 : 0x4964,bank:1);
        _rom[0xcd00] = 8;
    }
    internal void FinishScroll() => _rom.Call(_ages ? 0x4a17 : 0x49a4,bank:1);
}
