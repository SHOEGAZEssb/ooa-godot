using System.IO;

namespace Oracles;

// Stops at the native room-pack handoff after applyWarpDest_b04's lookup and branch.
internal sealed class WarpDestinationRom : RomFixture
{
    private readonly bool _ages;
    private bool _stopping, _season;
    internal WarpDestinationRom(OracleGame game) : base($"{game} warp destination", game: game)
    {
        _ages = game == OracleGame.Ages;
    }
    internal bool Apply(int sourceGroup, int sourceDungeon, int rawGroup, int room, int position, int growth = 0)
    {
        Array.Clear(_memory);
        _memory[_ages ? 0xcc2d : 0xcc49] = (byte)sourceGroup;
        _memory[_ages ? 0xcc39 : 0xcc55] = unchecked((byte)sourceDungeon);
        _memory[_ages ? 0xcc47 : 0xcc63] = (byte)rawGroup;
        _memory[_ages ? 0xcc48 : 0xcc64] = (byte)room;
        _memory[_ages ? 0xcc4a : 0xcc66] = (byte)position;
        _memory[0xc6df] = (byte)growth;
        _bank = 4; _memory[0xff97] = 4; _stopping = true; _season = false;
        try { _cpu.RunCall(0x45d0); throw new InvalidOperationException("Native warp returned before its room-pack handoff."); }
        catch (OperationCanceledException) { }
        finally { _stopping = false; }
        return _season;
    }
    internal int ResolveSeason(OracleSaveData save, int pack, int season)
    {
        if (_ages || save.Game != OracleGame.Seasons) throw new ArgumentException("Native Seasons season selection requires Seasons state.");
        for (int address = 0xc5b0; address < 0xcb00; address++) _memory[address] = save.ReadWramByte(address);
        _memory[0xcc4d] = (byte)pack; _memory[0xcc4e] = (byte)season;
        _bank = 1; _memory[0xff97] = 1;
        _cpu.RunCall(0x7e6e); // checkRoomPackAfterWarp_body.
        return _memory[0xcc4e];
    }
    internal byte SetEnteredWarpPosition(int x, int y)
    {
        _memory[0xd00b] = (byte)y;
        _memory[0xd00d] = (byte)x;
        _bank = 1; _memory[0xff97] = 1;
        _cpu.RunCall(_ages ? 0x5c82 : 0x5a6f); // bank1.setEnteredWarpPosition.
        return _memory[_ages ? 0xcc8e : 0xcca8];
    }
    internal byte ClearScrollingRoomVariables(byte marker)
    {
        int address = _ages ? 0xcc8e : 0xcca8;
        _memory[address] = marker;
        _bank = 1; _memory[0xff97] = 1;
        _cpu.RunCall(_ages ? 0x49c9 : 0x4956); // bank1.func_49c9, before destination initialization.
        return _memory[address];
    }
    internal int Group => _memory[_ages ? 0xcc2d : 0xcc49];
    internal int Room => _memory[_ages ? 0xcc30 : 0xcc4c];
    internal int Transition => _memory[_ages ? 0xcc49 : 0xcc65];
    internal Vector2 Position => new(_memory[0xd00d],_memory[0xd00b]);
    protected override int Read(int address)
    {
        if (_stopping && address == _cpu.InstructionAddress && (_ages ? address == 0x341a : _bank == 4 && address is 0x4659 or 0x465f))
        { _season = !_ages && address == 0x4659; throw new OperationCanceledException(); }
        return base.Read(address);
    }
    protected override bool Allowed(int address) => address is >= 0xc000 and < 0xe000 or >= 0xff80;
}
