using Godot;

namespace Oracles;

// Calls clean-US companionCheckCanSpawn through a species-style wrapper:
// its pop/ret skips the wrapper tail when yielding or deleting the object.
internal sealed class CompanionInitializationRom
{
    private readonly LinkCollisionRom _rom;
    internal CompanionInitializationRom(OracleGame game, int id, Vector2 position,
        Vector2 fallback, int collision, int fallbackCollision, bool occupied)
    {
        _rom = new(bankedStack: true, game: game);
        int entry = game == OracleGame.Ages ? 0x4822 : 0x47fd;
        byte[] wrapper = [0xcd, (byte)entry, (byte)(entry >> 8), 0x3e, 1, 0xea, 2, 0xc2, 0xc9];
        for (int index = 0; index < wrapper.Length; index++) _rom[0xc180 + index] = wrapper[index];
        _rom[0xd100] = 1; _rom[0xd101] = (byte)id;
        _rom.Word(0xd10a, (int)(position.Y * 256)); _rom.Word(0xd10c, (int)(position.X * 256));
        _rom[0xc638] = (byte)fallback.Y; _rom[0xc639] = (byte)fallback.X;
        _rom[0xcd00] = 1; _rom[0xcd01] = 1;
        _rom[0xce00 + Packed(position + new Vector2(0, 5))] = (byte)collision;
        _rom[0xce00 + Packed(fallback)] = (byte)fallbackCollision;
        int packed = Packed(position);
        if (occupied) _rom.Bank(2, 0xd980 + packed / 8, (byte)(1 << (packed & 7)));
    }

    private static int Packed(Vector2 position) => ((int)position.Y & 0xf0) | ((int)position.X >> 4);
    internal byte this[int address] => _rom[address];
    internal Vector2 Position => new(_rom.Word(0xd10c) / 256f, _rom.Word(0xd10a) / 256f);
    internal bool Advance()
    {
        _rom[0xc202] = 0;
        _rom.Call(0xc180, objectPage: 0xd1);
        return _rom[0xc202] != 0 || _rom[0xd100] == 0;
    }
}
