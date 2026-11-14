
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsUnavailableWarps()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                session.DebugTravel(0, 0xe8);
                var floor = new Vector2I(88, 100);
                session.Link.WarpTo(floor);
                FailIf(session.Collision.Collides(floor), "Unavailable warp fixture must stand on real floor south of Vasu's doorway.");
                byte[] disk = root.FileSlotSnapshot(0, OracleGame.Seasons);
                // Native special arrival and custom WRAM renderer: neither may consume the current room.
                foreach (var target in new[] { (Group: 3, Room: 0xa3, Position: 4, Mode: 7), (Group: 7, Room: 0xff, Position: 0x44, Mode: 4) })
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    int rng = session.Random.Calls;
                    var room = session.Room;
                    int checkpoint = session.Memory.ReadField("wLinkForceState");
                    session.Transitions.ApplyWarp(session.Link, new Warp(0, 0xe8, 0x65, 0, 4,
                        target.Group, target.Room, target.Position, 0, target.Mode));
                    FailIf(session.Transitions.IsTransitioning || !ReferenceEquals(session.Room, room) ||
                        session.Link.Position != floor || session.Random.Calls != rng ||
                        session.Memory.ReadField("wLinkForceState") != checkpoint || !root._scene.Dialogue.IsOpen ||
                        root._scene.Dialogue.CurrentMessage != "This area is not\navailable yet." ||
                        session.Memory.ReadField("wEnteredWarpPosition") != 0x65,
                        $"Unavailable destination ${target.Group:x1}:{target.Room:x2} must leave Link and the room intact before fading.");
                    app.Step(16, Vector2.Zero, batched: batched);
                    root._scene.Dialogue.Close();
                    app.Step(16, Vector2.Zero, batched: batched);
                    FailIf(session.Transitions.IsTransitioning || session.RoomId != 0xe8 || root._scene.Dialogue.IsOpen,
                        "Closing an unavailable warp message must return to ordinary gameplay without a repeated prompt.");
                }
                FailIf(!root.FileSlotSnapshot(0, OracleGame.Seasons).SequenceEqual(disk),
                    "Refused warps must not write the file slot.");
            }
            finally { root.Free(); }
        }
    }

    private void ValidateSeasonsNativeWarpDestinations()
    {
        var rom = ValidationRom.LoadCleanUs(OracleGame.Seasons);
        int Byte(int address) => rom.Span[address];
        int Word(int address) => Byte(address) | Byte(address + 1) << 8;
        // Native bank $04 tables: counts include duplicate anchors and unused destination records.
        int[] counts = [124, 39, 60, 76, 93, 176, 16, 11];
        var targets = new HashSet<(int Group, int Room)>();
        var root = StartSeasonsAfterImpa(false);
        try
        {
            var session = root._seasonsOpening!;
            var world = session.Data.World;
            var warps = WarpDatabase.For(SeasonsGameContent.AssetRoot);
            session.Memory.SetWramByte(0xc6df, 0); // Native group $2 destination-index growth offset.
            int total = 0, special = 0;
            for (int group = 0; group < 8; group++)
            {
                int destinations = 0x10000 + (Word(0x12d4e + group * 2) & 0x3fff);
                int assignments = 0x10000 + (Word(0x1133c + group * 2) & 0x3fff);
                for (int index = 0; index < counts[group]; index++)
                {
                    int address = destinations + index * 3;
                    int room = Byte(address), position = Byte(address + 1), packed = Byte(address + 2);
                    string context = $"Seasons group{group}WarpDestTable[${index:x2}] -> ${group:x1}:{room:x2}";
                    var request = new Warp(0, 0, 0, 0, 0, group, index, 0, 0, 0) { DestinationIsIndex = true };
                    var resolved = warps.ResolveDestination(request, session.Memory);
                    FailIf(resolved.DestinationRoom != room || resolved.DestinationPosition != position ||
                        resolved.DestinationParameter != packed >> 4 || resolved.DestinationTransition != (packed & 15),
                        context + ": imported destination differs from the clean US ROM.");
                    total++;
                    if ((packed & 15) == 7)
                    {
                        special++;
                        FailIf(group != 3 || room != 0xa3 || position != 4,
                            context + ": the only special arrival is Holly's chimney ($3:a3:$04).");
                    }
                    targets.Add((group, room));
                    for (int season = 0; season < 4; season++)
                    {
                        int assignment = Byte(assignments + room), tileset = assignment & 0x7f;
                        int header = 0x10c84 + tileset * 8;
                        if (Byte(header) == 0xff) header = 0x10000 + (Word(header + 1) & 0x3fff) + season * 8;
                        FailIf(!world.HasRoom(group, room), context + ": native warp destination has no room binding.");
                        var loaded = world.LoadRoom(room, season, group);
                        FailIf(loaded.Group != group || loaded.Id != room || loaded.TilesetId != tileset ||
                            loaded.TilesetFlags != Byte(header + 1) || loaded.ActiveCollisions != Byte(header) >> 4 ||
                            loaded.Width != (group < 4 ? 160 : 240) || loaded.Height != (group < 4 ? 128 : 176),
                            context + $", season ${season:x2}: native geometry, flags or collision binding differs.");
                    }
                }
            }
            FailIf(total != 595 || targets.Count != 434 || special != 1,
                $"Seasons: expected $253 native destination records, $1b2 physical rooms and one special arrival; got ${total:x3}, ${targets.Count:x3}, ${special:x2}.");
            // Construct each actual destination's room entities through the selected-game owner as well as its terrain.
            foreach (var target in targets)
            for (int season = 0; season < 4; season++)
            {
                try { session.LoadRoom(target.Room, season, group: target.Group); }
                catch (Exception error)
                {
                    throw new InvalidOperationException($"Seasons native warp target ${target.Group:x1}:{target.Room:x2}, season ${season:x2}: room construction failed.", error);
                }
            }
            foreach (int room in new[] { 0x78, 0x96 })
            {
                session.LoadRoom(room, 0, group: 4);
                // These native streams contain ENEMY $22/$21, not INTERAC remote-Maku/keyhole records.
                FailIf(session.Entities.EntityAdapters<SeasonsRemoteMakuRoomEntity>().Any() ||
                    session.Entities.EntityAdapters<SeasonsGnarledKeyholeRoomEntity>().Any(),
                    $"Seasons $4:{room:x2}: enemy IDs must not dispatch through the interaction namespace.");
            }
        }
        finally { root.Free(); }
    }

    private static void ValidateSeasonsWorldRooms()
    {
        var rom = ValidationRom.LoadCleanUs(OracleGame.Seasons);
        int Byte(int address) => rom.Span[address];
        int Word(int address) => Byte(address) | Byte(address + 1) << 8;
        var world = new SeasonsWorldData();
        int admitted = 0, custom = 0;
        foreach (int group in new[] { 6, 7 })
        for (int room = 0; room < 256; room++)
        for (int season = 0; season < 4; season++)
        {
            // Native group6/7 assignments alias group4/5 in bank $04.
            int assignment = Byte(0x1154c + (group - 6) * 256 + room);
            int tileset = assignment & 0x7f, header = 0x10c84 + tileset * 8;
            if (Byte(header) == 0xff) header = 0x10000 + (Word(header + 1) & 0x3fff) + season * 8;
            bool sideview = (Byte(header + 1) & 0x20) != 0;
            // tileset $62 uses GFXH_DRAGON_ONOX's custom WRAM tilemaps.
            bool unsupported = sideview && tileset == 0x62;
            string context = $"Seasons room ${group:x1}:{room:x2}, season ${season:x2}";
            FailIf(world.HasRoom(group, room) != (sideview && !unsupported),
                context + ": admission differs from the native tileset flags and supported renderer contract.");
            if (!sideview) continue;
            if (unsupported)
            {
                custom++;
                bool rejected = false;
                try { world.LoadRoom(room, season, group); }
                catch (NotSupportedException error)
                {
                    rejected = error.Message.Contains("gfxHeaders.s:GFXH_DRAGON_ONOX", StringComparison.Ordinal) &&
                        error.Message.Contains("w6DragonOnoxTileMap1", StringComparison.Ordinal);
                }
                FailIf(!rejected, context + ": custom WRAM graphics must retain an explicit source-aware rejection.");
                continue;
            }
            admitted++;
            var loaded = world.LoadRoom(room, season, group);
            int dungeon = Byte(header) & 15;
            FailIf(loaded.Group != group || loaded.Id != room || loaded.TilesetId != tileset ||
                loaded.TilesetFlags != Byte(header + 1) || loaded.ActiveCollisions != Byte(header) >> 4 ||
                loaded.LoadsUniqueGraphicsAfterScroll != ((assignment & 0x80) != 0) ||
                world.GetDungeonIndex(group, room) != (dungeon == 15 ? -1 : dungeon) ||
                loaded.Width != 240 || loaded.Height != 176 || loaded.Texture.GetSize() != new Vector2(240, 176),
                context + ": native group alias, graphics, collision, dungeon or large-room geometry differs.");
        }
        FailIf(admitted != 136 || custom != 4,
            "Seasons: preserve all 34 admitted side-view rooms and the custom-renderer room across four season inputs.");
        for (int room = 0; room < 256; room++)
        for (int season = 0; season < 4; season++)
        {
            int assignment = Byte(0x1144c + room), tileset = assignment & 0x7f;
            int header = 0x10c84 + tileset * 8;
            if (Byte(header) == 0xff) header = 0x10000 + (Word(header + 1) & 0x3fff) + season * 8;
            var loaded = world.LoadRoom(room, season, 1);
            FailIf(!world.HasRoom(1, room) || loaded.Group != 1 || loaded.Id != room || loaded.TilesetId != tileset ||
                loaded.TilesetFlags != Byte(header + 1) || loaded.ActiveCollisions != Byte(header) >> 4 ||
                loaded.Width != 160 || loaded.Height != 128 || loaded.Texture.GetSize() != new Vector2(160, 128),
                $"Seasons Subrosia room $1:{room:x2}, season ${season:x2}: native assignment, flags, graphics or geometry differs.");
        }
        for (int season = 0; season < 4; season++)
        foreach (int interior in new[] { 0x83, 0x91 })
        {
            // tilesetAssignments.s aliases groups $1/$2/$3, but the active group remains $3.
            var loaded = world.LoadRoom(interior, season, 3);
            FailIf(!world.HasRoom(3, interior) || loaded.Group != 3 || loaded.Id != interior ||
                loaded.TilesetId != 0x2c || loaded.ActiveCollisions != 3 || loaded.TilesetFlags != 4 ||
                loaded.Width != 160 || loaded.Height != 128 ||
                loaded.GetPackedStorageMetatile(0x74) != 0xe0 || loaded.GetPackedStorageMetatile(0x75) != 0xe1 ||
                world.GetDungeonIndex(3, interior) != -1,
                $"Interior $3:{interior:x2}, season ${season:x2}: missing native binding or incorrect group/geometry.");
        }
    }

    // group0WarpSources $e8 -> group3WarpDestTable[$15] = $91/$ff/$9/$3.
    private void ValidateSeasonsVasuShopDoorway() => ValidateSeasonsInteriorDoorway(0xe8, 0x91, 0x45);

    // warpSource75e3 $43 -> group3WarpDestTable[$03] = $83/$ff/$9/$3;
    // group3WarpSources $83 -> group0WarpDestTable[$11] = $d7/$43/$0/$1.
    private void ValidateSeasonsTickTockDoorway() => ValidateSeasonsInteriorDoorway(0xd7, 0x83, 0x43);

    private void ValidateSeasonsInteriorDoorway(int exterior, int interior, int doorway)
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = StartSeasonsAfterImpa(batched);
            try
            {
                var session = root._seasonsOpening!;
                var app = new ApplicationValidationFixture(root);
                byte[] disk = root.FileSlotSnapshot(0, OracleGame.Seasons);
                session.Memory.SetField("wRoomStateModifier", batched ? (byte)3 : (byte)0);
                session.DebugTravel(0, exterior);
                var arrival = new Vector2I((doorway & 15) * 16 + 8, (doorway >> 4) * 16 + 8);
                session.Link.WarpTo(arrival + new Vector2I(0, 28));
                FailIf(session.Collision.Collides(session.Link.Position),
                    $"The approach south of $0:{exterior:x2}:${doorway:x2} must be collision-valid.");
                for (int visit = 0; visit < 2; visit++)
                {
                    int updates = 0;
                    while (!session.Transitions.IsTransitioning && updates++ < 90) app.Step(1, Vector2.Up);
                    FailIf(!session.Transitions.IsTransitioning,
                        $"Walking up to $0:{exterior:x2}:${doorway:x2} must start its native interior warp.");
                    app.Step(160, Vector2.Zero, batched: batched);
                    FailIf(session.Rooms.ActiveGroup != 3 || session.RoomId != interior ||
                        session.Transitions.IsTransitioning || session.Link.Position != new Vector2I(80, 100) ||
                        session.Rooms.CurrentDungeonIndex != -1,
                        $"The entrance must walk into $3:{interior:x2} XY($50,$64): group={session.Rooms.ActiveGroup}, room=${session.RoomId:x2}, XY={session.Link.Position}.");
                    app.Step(16, Vector2.Zero, batched: batched);
                    FailIf(session.Transitions.IsTransitioning || session.Collision.Collides(session.Link.Position),
                        $"Interior $3:{interior:x2} arrival must stay on valid floor without immediately exiting.");
                    updates = 0;
                    while (!session.Transitions.IsTransitioning && updates++ < 90) app.Step(1, Vector2.Down);
                    FailIf(!session.Transitions.IsTransitioning,
                        $"Walking down from $3:{interior:x2}'s actual arrival must reach the native exit.");
                    app.Step(160, Vector2.Zero, batched: batched);
                    FailIf(session.Rooms.ActiveGroup != 0 || session.RoomId != exterior ||
                        session.Transitions.IsTransitioning || session.Link.Position != arrival,
                        $"Interior $3:{interior:x2}'s exit must return to $0:{exterior:x2}:${doorway:x2}.");
                    app.Step(16, Vector2.Zero, batched: batched);
                    FailIf(session.Transitions.IsTransitioning,
                        "The return doorway must suppress re-entry until Link walks off it.");
                    app.Step(16, Vector2.Down, batched: batched);
                }
                FailIf(!root.FileSlotSnapshot(0, OracleGame.Seasons).SequenceEqual(disk),
                    $"Visiting interior $3:{interior:x2} must not write the file slot.");
            }
            finally { root.Free(); }
        }
    }
}
