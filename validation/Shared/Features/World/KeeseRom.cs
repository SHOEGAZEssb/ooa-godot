
namespace Oracles;

// Execute both clean-US enemy $32 handlers; no AI, animation or RNG is stubbed.
internal sealed class KeeseRom
{
    private readonly LinkCollisionRom _rom;
    private readonly OracleGame _game;
    private readonly int _subId;
    private int Bank => _game == OracleGame.Ages ? 0x0e : 0x0d;
    internal int State => _rom[0xd084];
    internal int Counter1 => _rom[0xd086];
    internal int Counter2 => _rom[0xd087];
    internal int Angle => _rom[0xd089];
    internal int Speed => _rom[0xd090];
    internal int Z => unchecked((sbyte)_rom[0xd08f]);
    internal int TurnAmount => unchecked((sbyte)_rom[0xd0b0]);
    internal int RandomUpdates => _rom.RandomUpdates;
    internal byte Rng1 => _rom[0xff94];
    internal byte Rng2 => _rom[0xff95];
    internal Vector2 Position => new(_rom.Word(0xd08c) / 256f,_rom.Word(0xd08a) / 256f);

    internal KeeseRom(OracleGame game, int subId, Vector2 position, OracleRandomState random)
    {
        _game = game; _subId = subId; _rom = new(game:game);
        _rom[game == OracleGame.Ages ? 0xffae : 0xffac] = 0x80;
        _rom[0xff94] = random.Rng1; _rom[0xff95] = random.Rng2;
        _rom[0xd080] = 1; _rom[0xd081] = 0x32; _rom[0xd082] = (byte)subId;
        int gfx = game == OracleGame.Ages ? 0xcc08 : 0xcc07;
        _rom[gfx] = game == OracleGame.Ages ? (byte)0x9d : (byte)0x84; _rom[gfx+1] = 1;
        _rom.Word(0xd08a,(int)(position.Y * 256)); _rom.Word(0xd08c,(int)(position.X * 256));
        _rom[0xcd0a] = 160; _rom[0xcd0b] = 128;
        // ecom_bounceOffScreenBoundary reads the room collision padding.
        for (int packed = 0; packed < 256; packed++)
            _rom[0xce00+packed] = (packed>>4) < 8 && (packed&15) < 10 ? (byte)0 : (byte)0xff;
        _rom.Call(game == OracleGame.Ages ? 0x282b : 0x2772,bank:Bank,accumulator:0,objectOffset:0x81);
    }

    // Include enemyStandardUpdate's initialization draw and properties, not
    // only the species' state-zero handler.
    internal void Initialize() => _rom.Call(_game == OracleGame.Ages ? 0x2ea5 : 0x2e86,
        bank:0,restoresBank:false);

    internal void Update(Vector2 link, Vector2 sampledTarget, int frameCounter)
    {
        _rom[0xcc00] = (byte)frameCounter;
        _rom[0xd00b] = unchecked((byte)MathF.Floor(link.Y)); _rom[0xd00d] = unchecked((byte)MathF.Floor(link.X));
        int target = _game == OracleGame.Ages ? 0xffb0 : 0xffae;
        _rom[target] = unchecked((byte)MathF.Floor(sampledTarget.Y)); _rom[target + 1] = unchecked((byte)MathF.Floor(sampledTarget.X));
        _rom.Call(_game == OracleGame.Ages ? _subId == 0 ? 0x474f : 0x47ca : _subId == 0 ? 0x476e : 0x47e9,
            bank:Bank,objectOffset:0x84);
    }
}
