
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedTileProperties()
    {
        // Clean-US tileTypesTable/hazardCollisionTable pointer streams, not
        // counts inferred from the runtime dictionaries (which lose aliases).
        foreach (var expected in new[]
        {
            (Root: AgesGameContent.AssetRoot,
                Modes: new[] { "overworld", "indoors", "dungeons", "sidescrolling", "underwater", "five" },
                Types: new[] { 22, 30, 32, 16, 22, 30 }, Hazards: new[] { 15, 16, 16, 6, 15, 16 }),
            (Root: SeasonsGameContent.AssetRoot,
                Modes: new[] { "overworld", "subrosia", "makutree", "indoors", "dungeons", "sidescrolling" },
                Types: new[] { 25, 30, 4, 32, 32, 19 }, Hazards: new[] { 13, 23, 0, 16, 16, 9 })
        })
        {
            var modes = GeneratedTable.Load(expected.Root + "metadata/collision_modes.tsv",
                new("source collision modes", GeneratedTableKeySemantics.Ordered,
                    ["mode", "name", "side-scrolling", "source"], headerRequired: true));
            FailIf(!modes.Rows.Select(row => row.RequiredString(1)).SequenceEqual(expected.Modes),
                $"{expected.Root}: native collision-mode pointer order differs from the clean US source.");
            foreach (var stream in new[] { (Name: "tile_types", Counts: expected.Types), (Name: "tile_hazards", Counts: expected.Hazards) })
            {
                var table = GeneratedTable.Load(expected.Root + $"metadata/{stream.Name}.tsv",
                    new(stream.Name, GeneratedTableKeySemantics.Ordered,
                        ["mode", "index", "tile", "value", "source"], headerRequired: true));
                for (int mode = 0; mode < 6; mode++)
                    FailIf(table.Rows.Count(row => row.Decimal(0) == mode) != stream.Counts[mode],
                        $"{expected.Root}{stream.Name}: native mode ${mode:x2} must retain every pair before its $00 terminator.");
            }
            OracleGame game = OracleAssetPack.GameFor(expected.Root);
            var native = new LinkCollisionRom(game:game);
            var data = TilePropertyDatabase.For(expected.Root);
            native[0xd00b] = 64; native[0xd00d] = 72;
            for (int mode = 0; mode < 6; mode++)
            for (int tile = 0; tile < 256; tile++)
            {
                native[game == OracleGame.Ages ? 0xcc33 : 0xcc4f] = (byte)mode;
                native[0xcf44] = (byte)tile;
                native.Call(game == OracleGame.Ages ? 0x2216 : 0x21d4,bank:0); // objectCheckIsOverHazard.
                byte flags = data.NativeHazardFlags(mode,(byte)tile);
                FailIf(flags != native[0xc200] || (flags != 0) != ((native[0xc201] & 0x10) != 0),
                    $"{game}: native object hazard flags/carry differ for mode ${mode:x2}, tile ${tile:x2}.");
            }
        }

        var ages = TilePropertyDatabase.For(AgesGameContent.AssetRoot);
        var seasons = TilePropertyDatabase.For(SeasonsGameContent.AssetRoot);
        FailIf(ReferenceEquals(ages, seasons) || !ReferenceEquals(ages, TilePropertyDatabase.For(AgesGameContent.AssetRoot)),
            "Terrain tables must share immutable data only within their own game pack.");
        FailIf(ages.Type(2, 0x0e) != TerrainType.RaisableFloor || ages.Type(2, 0x0f) != TerrainType.RaisableFloor ||
            ages.Type(1, 0x0e) != TerrainType.Normal || ages.Type(5, 0x0f) != TerrainType.Normal,
            "Ages' raisable-floor prefix belongs only to tileTypesTable@dungeons, before the indoors fallthrough.");
        FailIf(ages.Type(0, 0xf9) != TerrainType.Puddle || seasons.Type(0, 0xf9) != TerrainType.Grass ||
            ages.Type(0, 0xfc) != TerrainType.SeaWater || seasons.Type(0, 0xfc) != TerrainType.Puddle ||
            seasons.Type(0, 0xfd) != TerrainType.Water || seasons.Type(0, 0x20) != TerrainType.Stump ||
            seasons.Type(0, 0xdb) != TerrainType.CrackedIce || seasons.NativeTileType(0, 0xdb) != 0x0e,
            "Shared terrain dispatch must retain each game's native top-down tile identities.");
        FailIf(seasons.SideScrollType(0x0c) != SideScrollTileType.Hole || seasons.NativeTileType(5, 0x0c) != 1 ||
            seasons.Hazard(5, 0x0c) != HazardType.Lava || ages.SideScrollType(0x20) != SideScrollTileType.Ice,
            "bank0.s:lookupKey must keep Seasons $0c's first HOLE pair, independently of its LAVA hazard classification.");
        FailIf(seasons.NativeHazardFlags(3, 0xd0) != 0x42 || seasons.NativeHazardFlags(4, 0xd0) != 0x42 ||
            seasons.Hazard(3, 0xd0) != HazardType.Hole ||
            Enumerable.Range(0, 256).Any(tile => seasons.NativeHazardFlags(2, (byte)tile) != 0),
            "Seasons must retain hazard $42's high flag and the empty Maku Tree hazard stream.");

        OracleRoomData agesRoom = new AgesWorldData().LoadRoom(0, 0x60);
        OracleRoomData seasonsRoom = new SeasonsWorldData().LoadRoom(0xa7, 0);
        foreach (OracleRoomData room in new[] { agesRoom, seasonsRoom })
        {
            byte previous = room.Layout[0];
            try
            {
                room.Layout[0] = 0xf9;
                TerrainInfo terrain = room.GetTerrainInfo(new Vector2(8, 8));
                bool isAges = room.AssetRoot == AgesGameContent.AssetRoot;
                FailIf(terrain.Type != (isAges ? TerrainType.Puddle : TerrainType.Grass) ||
                    terrain.NativeTileType != (isAges ? 0x11 : 5) || terrain.Collision != room.Collisions[0xf9],
                    "Room terrain queries must use the room's selected game pack without a separate Seasons resolver.");
                FailIf(room.GetTerrainInfo(new Vector2(-1, 8)).Collision != 0xff,
                    "An out-of-room terrain query must preserve the collision-buffer sentinel.");
            }
            finally { room.Layout[0] = previous; }
        }
        bool rejected = false;
        try { _ = seasons.NativeTileType(6, 0xf9); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        FailIf(!rejected, "A missing native collision-mode pointer must not silently become normal floor.");
    }
}
