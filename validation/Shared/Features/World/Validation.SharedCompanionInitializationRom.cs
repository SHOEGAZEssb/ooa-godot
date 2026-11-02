
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedCompanionInitializationRom()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            string pack = OracleAssetPack.ForGame(game);
            var room = new OracleRoomData(0, 0x97, 0, 0, 0, 0, new byte[80], new byte[256],
                OracleGraphicsCache.LoadImage(pack + "gfx/spr_link.png"),
                OracleGraphicsCache.LoadImage(pack + "gfx/gfx_hud.png"), new byte[2048], new Color[6,4],
                new BackgroundPaletteState(new Color[4], new Color[3,4]), new OracleAnimationData(pack)) { AssetRoot = pack };
            var save = OracleSaveData.CreateStandardGame(game);
            var terrainMemory = new OracleRuntimeState(game);
            var terrainNative = new LinkCollisionRom(game: game, bankedStack: true);
            foreach (byte enabled in new byte[] { 0, 1, 3 })
            foreach (byte tile in new byte[] { 0, 0xf8, 0xf9, 0xfa, 0xfb, 0xfc })
            foreach (byte z in new byte[] { 0, 1, 16, 17, 0x7f, 0x80, 0xff })
            foreach (byte visible in new byte[] { 0, 0x43, 0x82, 0xff })
            {
                room.SetPositionTileAndCollision(new(72, 56), tile, 0, 0);
                terrainNative[0xcf34] = tile;
                foreach (var field in new (int Address, byte Value)[]
                {
                    (0xd100, enabled), (0xd101, 0x0b), (0xd10b, 56), (0xd10d, 72),
                    (0xd10e, 0x55), (0xd10f, z), (0xd11a, visible), (0xd13c, 0x5a), (0xcc6e, 0xa5)
                })
                {
                    terrainMemory.SetWramByte(field.Address, field.Value);
                    terrainNative[field.Address] = field.Value;
                }
                terrainNative.Call(game == OracleGame.Ages ? 0x490a : 0x48bb, bank: 5, objectPage: 0xd1);
                CompanionNativeTerrain.Update(terrainMemory, room);
                foreach (int address in new[] { 0xd10e, 0xd10f, 0xd11a, 0xcc6e })
                    FailIf(terrainMemory.ReadWramByte(address) != terrainNative[address],
                        $"{game}: companion native tail at ${address:x4}, enabled=${enabled:x2}, tile=${tile:x2}, Z=${z:x2}, visible=${visible:x2} differs.");
            }
            Vector2 fallback = new(104, 88);
            save.SetLastAnimalMountPoint(88, 104);
            foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
            foreach (bool occupied in new[] { false, true })
            foreach (int collision in new[] { 0, 1, 0x0c, 0x0f, 0x10, 0x11 })
            foreach (int fallbackCollision in new[] { 0, 1, 0x0f })
            foreach (bool fractional in new[] { false, true })
            foreach (int y in new[] { 60, 252 })
            {
                Vector2 initial = new Vector2(72, y) + (fractional ? new Vector2(.75f, .25f) : Vector2.Zero);
                Vector2 position = initial;
                room.SetPositionTileAndCollision(new(72, (y + 5) & 0xff), 0, (byte)collision, 0);
                room.SetPositionTileAndCollision(fallback, 0, (byte)fallbackCollision, 0);
                int occupancyReads = 0;
                var memory = OracleRuntimeState.CreateInitialized(game);
                memory.SetWramByte(0xd10a, fractional ? (byte)0x40 : (byte)0);
                memory.SetWramByte(0xd10c, fractional ? (byte)0xc0 : (byte)0);
                CompanionRuntimeState.BeginIdle(memory, id, initial);
                var initialization = new CompanionRoomInitialization(() => room, save, memory, _ => { occupancyReads++; return occupied; });
                var native = new CompanionInitializationRom(game, id, initial, fallback, collision, fallbackCollision, occupied);
                string context = $"{game}/${id:x2}/occupied={occupied}/collision=${collision:x2}/fallback=${fallbackCollision:x2}/fractional={fractional}/Y=${y:x2}";
                int updates = game == OracleGame.Ages ? 2 : 1;
                for (int update = 1; update <= updates; update++)
                {
                    bool expected = native.Advance();
                    bool actual = initialization.Advance(ref position);
                    foreach (int address in new[] { 0xd100, 0xd101, 0xd104, 0xd105, 0xd10a, 0xd10b, 0xd10c, 0xd10d,
                        0xd11b, 0xd11c, 0xd11d, 0xd124, 0xd132 })
                        FailIf(memory.ReadWramByte(address) != native[address],
                            $"{context}, update{update}: initialization must publish native byte${address:x4} through the live memory owner.");
                    FailIf(actual != expected || initialization.Pending == expected ||
                        initialization.Rejected != (native[0xd100] == 0) ||
                        native[0xd100] != 0 && position != native.Position,
                        $"{context}, update{update}: native initialization yield/rejection/position differs: runtime={actual}/{initialization.Rejected}/{position}, native={expected}/{native[0xd100] == 0}/{native.Position}.");
                    FailIf(game == OracleGame.Ages && update == 1 &&
                        (native[0xd104] != 0 || native[0xd105] != 1 || position != initial || occupancyReads != 0),
                        "Ages state0 must yield once before reading solid reservations or changing coordinates.");
                }
                FailIf(occupancyReads != (game == OracleGame.Ages ? 1 : 0) || initialization.Pending ||
                    save.LastAnimalMountX != 104 || save.LastAnimalMountY != 88,
                    $"{context}: only Ages consults solid reservations; initialization must not change the fallback save bytes.");
                if (!initialization.Rejected)
                    FailIf(native[0xd104] != 1 || native[0xd124] != 0x80,
                        $"{context}: the original helper must finish state0 with state1 and Link collision type $80.");
            }
        }
    }
}
