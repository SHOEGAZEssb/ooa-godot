using Godot;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedScreenPalettes()
    {
        var seasons = new ScreenTransitionPaletteDatabase(SeasonsGameContent.AssetRoot);
        var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
        // bank1.s and data/seasons/paletteTransitions.s: eight ordered
        // routes, each selecting six BG palettes through four season words.
        (int Room, Vector2I Direction)[] routes = [(0xe0, Vector2I.Up), (0xf0, Vector2I.Down),
            (0x63, Vector2I.Up), (0x73, Vector2I.Down), (0x83, Vector2I.Up), (0x93, Vector2I.Down),
            (0x23, Vector2I.Up), (0x33, Vector2I.Down)];
        var bytes = new List<byte>();
        foreach (var route in routes)
        for (int season = 0; season < 4; season++)
        {
            bool found = seasons.TryGet(0, route.Room, route.Direction, save, season, out var colors);
            FailIf(!found, $"Seasons palette route $0:${route.Room:x2}, season ${season:x2} is absent.");
            bytes.AddRange(colors.Source);
            bytes.AddRange(colors.Destination);
        }
        // Independent clean-US ROM pointer walk: paletteTransitionSeasonData
        // at $01:$4845, palette color words in bank $16.
        FailIf(bytes.Count != 4608 || Convert.ToHexString(SHA256.HashData(bytes.ToArray())) !=
                "C7C69643D7814456CE79D886B48A4126D86B40DA47F787F52A8CB2A776D5868B",
            "Seasons smooth-palette routes must retain all 32 native source/destination pairs in seasonal pointer order.");
        save.SetGlobalFlag(AgesGlobalFlag.TuniNutPlaced);
        FailIf(!seasons.TryGet(0, 0xe0, Vector2I.Up, save, 3, out _) ||
            seasons.TryGet(1, 0xe0, Vector2I.Up, save, 3, out _) ||
            seasons.TryGet(0, 0xe0, Vector2I.Left, save, 3, out _) ||
            seasons.TryGet(2, 0xe0, Vector2I.Up, save, 0xff, out _) ||
            seasons.TryGet(1, 0xe0, Vector2I.Up, save, 0xff, out _),
            "Seasons must use its own group/direction selection without Ages' flag gate or a premature season read.");
        bool rejected = false;
        try { seasons.TryGet(0, 0xe0, Vector2I.Up, save, 4, out _); }
        catch (NotSupportedException) { rejected = true; }
        FailIf(!rejected, "A matched Seasons route with an unimported seasonal index must fail with source context.");
        rejected = false;
        try { seasons.TryGet(0, 0xe0, Vector2I.Up, OracleSaveData.CreateStandardGame(OracleGame.Ages), 0, out _); }
        catch (ArgumentException) { rejected = true; }
        FailIf(!rejected, "Cross-game palette lookup must reject the save before evaluating flags.");
    }
}
