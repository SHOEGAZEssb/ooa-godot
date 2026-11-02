using System.IO;

namespace Oracles;

// Executes bank4's source/edge lookup unchanged; room bytes and caller inputs are staged.
internal sealed class WarpLookupRom
{
    private readonly OracleGame _game;
    private readonly ReadOnlyMemory<byte> _rom;
    private readonly byte[] _memory = new byte[0x10000];
    private readonly OracleCpu _cpu;
    private readonly bool _ages;
    private int GroupAddress => _ages ? 0xcc2d : 0xcc49;
    private int RoomAddress => _ages ? 0xcc30 : 0xcc4c;
    private int DestinationAddress => _ages ? 0xcc47 : 0xcc63;

    internal WarpLookupRom(OracleGame game)
    {
        _game = game; _ages = game == OracleGame.Ages; _rom = ValidationRom.LoadCleanUs(game);
        _cpu = new(Read,Write,static _ => { },(pc,detail) => new InvalidDataException($"{game} warp ROM $04:${pc:x4}: {detail}."));
    }

    internal NativeWarpLookupResult Lookup(int group, int room, int packedPosition,
        Vector2I direction = default, int x = 0)
    {
        Array.Clear(_memory);
        _memory[GroupAddress] = (byte)group; _memory[RoomAddress] = (byte)room;
        _memory[_ages ? 0xcc2c : 0xcc48] = 0xd0;
        _memory[0xff8d] = (byte)packedPosition;
        _memory[DestinationAddress] = 0xff;
        _memory[0xff97] = 4;
        int entry = _ages ? 0x4629 : 0x4662;
        if (direction != Vector2I.Zero)
        {
            _memory[0xcd00] = 4;
            _memory[0xcd02] = (byte)(direction == Vector2I.Up ? 0 : direction == Vector2I.Down ? 2 : 1);
            _memory[0xd00d] = unchecked((byte)x);
            entry = _ages ? 0x46c8 : 0x46a8;
        }
        _cpu.RunCall(entry);
        int destinationGroup = _memory[DestinationAddress];
        return new(destinationGroup < 8,destinationGroup,_memory[DestinationAddress + 1],_memory[DestinationAddress + 2]);
    }

    private int Read(int address)
    {
        if (address < 0x4000) return _rom.Span[address];
        if (address < 0x8000) return _rom.Span[4 * 0x4000 + (address & 0x3fff)];
        if (Allowed(address)) return _memory[address];
        throw new InvalidDataException($"{_game} warp ROM: undeclared read ${address:x4}.");
    }
    private void Write(int address, int value)
    {
        if (Allowed(address)) { _memory[address] = (byte)value; return; }
        throw new InvalidDataException($"{_game} warp ROM: undeclared write ${address:x4}.");
    }
    private static bool Allowed(int address) => address is >= 0xcc00 and < 0xcd00 or 0xcd00 or 0xcd02 or 0xcec0 or
        >= 0xd000 and < 0xd040 or >= 0xdfe0 and <= 0xdff1 or 0xff8c or 0xff8d or 0xff97;
}

internal readonly record struct NativeWarpLookupResult(bool Found, int Group, int Index, int Transition);
