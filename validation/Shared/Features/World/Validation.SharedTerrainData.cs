using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedTerrainData()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            string pack = OracleAssetPack.ForGame(game);
            bool ages = game == OracleGame.Ages;
            var breakables = new BreakableTileDatabase(pack);
            var ledges = new LedgeJumpDatabase(pack);
            if (ages)
            {
                Check(0,0xc6,0x04,0x6b1b7,0,0xc0,0xdc,7,30);
                Check(0,0xcb,0x12,0x40,0,0xca,0xd2,7,50);
                Check(2,0x10,0x1d,0x125,2,6,0xa0,0xff,0);
                FailIf(!ledges.IsCliffTile(0,0x05,0x10) || !ledges.IsCliffTile(4,0xff,0x10) ||
                    !ledges.IsLandableSolidTile(1,0x0e), "Ages native cliff aliases/landing exceptions changed.");
            }
            else
            {
                // Native pointer streams include fallthrough from overworld
                // into Subrosia's flags and maturity rows before Maku's $00.
                Check(0,0xc5,0x02,0x6b1b7,0,0xc0,0xe6,7,30);
                Check(0,0xc6,0x03,0x6b1b7,0,0xc0,0xe0,7,50);
                Check(0,0xcb,0x12,0x1000,0,0xdf,0xe7,7,30);
                Check(3,0x10,0x1f,0x125,2,6,0xa0,0xff,0);
                Check(4,0x10,0x1f,0x125,2,6,0xa0,0xff,0);
                FailIf(!ledges.IsCliffTile(0,0x54,0x10) || !ledges.IsCliffTile(1,0xea,0x10) ||
                    ledges.IsCliffTile(0,0x05,0x10) || !ledges.IsCliffTile(3,0x05,0) ||
                    !ledges.IsCliffTile(4,0x06,0x10) || !ledges.IsLandableSolidTile(0,0xeb) ||
                    !ledges.IsLandableSolidTile(0,0x20) || ledges.IsLandableSolidTile(3,0x0e),
                    "Seasons native overworld/Subrosia/indoor cliff and landing streams lost their collision-set identities.");
            }
            FailIf(ledges.InitialSpeedZ != -448 || ledges.TransitionSpeedZ != -256 || ledges.Gravity != 0x20 ||
                ledges.SpeedRaw(1) != 0x14 || ledges.SpeedRaw(11) != 0x78 || ledges.SpeedRaw(50) != 0x78,
                $"{game}: native ledge physics and length clamp changed.");
            CheckUnderlyingSelection(game);
            CheckItemPassage(game);
            void Check(int collision, int tile, int mode, int mask, int drop, int effect, int replacement, int flag, int gasha)
            {
                FailIf(!breakables.TryGet(collision,tile,out var row) ||
                    row != new BreakableTileRecord(collision,tile,mode,mask,drop,effect,replacement,flag,gasha),
                    $"{game}: source breakable row ${collision:x2}:${tile:x2} changed: {row}.");
                var save = OracleSaveData.CreateStandardGame(game);
                row.ApplyPersistentEffects(save,0,0x97);
                FailIf(save.GashaMaturity != gasha || save.HasRoomFlag(0,0x97,0x80) != ((effect & 0x80) != 0 && flag == 7),
                    $"{game}: breakable ${collision:x2}:${tile:x2} did not apply source maturity/room-flag side effects.");
            }
        }
    }

    private static void CheckItemPassage(OracleGame game)
    {
        string pack = OracleAssetPack.ForGame(game);
        bool ages = game == OracleGame.Ages;
        var cliffs = new ItemCliffDatabase(pack);
        // Native itemPassableTiles.s: Ages aliases overworld/underwater and
        // dungeon/five; Seasons aliases overworld/Subrosia and has no Maku list.
        int dungeon = ages ? 2 : 4;
        FailIf(!cliffs.CanPassWithoutElevation(0, 0xfd) ||
            !cliffs.CanPassWithoutElevation(ages ? 4 : 1, 0xfd) ||
            !cliffs.CanPassWithoutElevation(dungeon, 0x90) ||
            cliffs.CanPassWithoutElevation(ages ? 3 : 5, 0x90) ||
            !ages && cliffs.CanPassWithoutElevation(2, 0xfd),
            $"{game}: unconditional item passability lost native aliases, mode order or fallthrough terminators.");
        byte cliff = ages ? (byte)0x05 : (byte)0x54;
        FailIf(!cliffs.TryGetDelta(0, cliff, 4, out byte down) || down != 1 ||
            !cliffs.TryGetDelta(0, cliff, 0, out byte up) || up != 0xff ||
            !cliffs.TryGetDelta(0, cliff, 7, out byte diagonal) || diagonal != 0xff ||
            cliffs.TryGetDelta(ages ? 3 : 1, cliff, 4, out _),
            $"{game}: native cliff byte deltas/diagonal direction order changed.");
        var layout = new byte[80]; Array.Fill(layout, cliff);
        var room = new OracleRoomData(0, 0x97, 0, 0, 0, 0, layout, new byte[256],
            OracleGraphicsCache.LoadImage(pack + "gfx/spr_link.png"),
            OracleGraphicsCache.LoadImage(pack + "gfx/gfx_hud.png"), new byte[2048], new Color[6,4],
            new BackgroundPaletteState(new Color[4], new Color[3,4]), new OracleAnimationData(pack)) { AssetRoot = pack };
        var passage = new ItemTilePassage(pack);
        FailIf(!passage.CanPass(room, new(8,8), 0x10) || !passage.CanPass(room, new(8,8), 0x10) ||
            !passage.CanPass(room, new(24,8), 0),
            $"{game}: unchanged position/tile must not apply elevation twice.");
        FailIf(passage.CanPass(room, new(40,8), 0) || passage.CanPass(room, new(40,8), 0),
            $"{game}: negative elevation must reject passage and invalidate the cached position/tile.");
        room.Layout[2] = 0xfd;
        FailIf(!passage.CanPass(room, new(40,8), 0),
            $"{game}: a changed unconditional tile must pass without consulting a game-specific bomb record.");
    }

    private static void CheckUnderlyingSelection(OracleGame game)
    {
        string pack = OracleAssetPack.ForGame(game);
        Image source = OracleGraphicsCache.LoadImage(pack + "gfx/spr_link.png");
        Image hud = OracleGraphicsCache.LoadImage(pack + "gfx/gfx_hud.png");
        var palettes = new BackgroundPaletteState(new Color[4], new Color[3,4]);
        var animations = new OracleAnimationData(pack);
        foreach (int group in new[] { 0,2,3,4 })
        foreach (int collision in new[] { 0,1,2,3,4 })
        foreach (int tile in new[] { 0x10,0xdb,0xc5 })
        foreach (byte originalCollision in new byte[] { 0,0x0f,0x10 })
        {
            var layout = new byte[80]; Array.Fill(layout,(byte)0x44);
            var collisions = new byte[256]; collisions[0x44] = originalCollision;
            var room = new OracleRoomData(group,0x97,0,0,collision,0,layout,collisions,source,hud,
                new byte[2048],new Color[6,4],palettes,animations) { AssetRoot = pack };
            var record = new BreakableTileRecord(collision,tile,0,1,0,0,0xa0,0xff,0);
            // code/breakableTiles.s: Ages diamond or collision $01/$02 pot;
            // Seasons pot only when wActiveGroup >= $03, independent of mode.
            bool eligible = game == OracleGame.Ages
                ? tile == 0xdb || tile == 0x10 && collision is 1 or 2
                : tile == 0x10 && group >= 3;
            byte expected = eligible && originalCollision != 0x0f ? (byte)0x44 : (byte)0xa0;
            FailIf(record.ReplacementFor(room,new Vector2(8,8)) != expected,
                $"{game}: native underlying tile gate changed for group ${group:x2}, collision ${collision:x2}, tile ${tile:x2}, original collision ${originalCollision:x2}.");
        }
    }
}
