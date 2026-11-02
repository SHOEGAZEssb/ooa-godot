
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedCommonEffects()
    {
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(0, 0x45);
            Check(new ApplicationValidationFixture(this), _player, _rooms, _entities, _sound);
            var root = new FrontendValidationRoot();
            AddChild(root);
            try
            {
                var selector = root.InitializeGameSelect();
                var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
                save.SetLinkName("SEAS"); save.SetGlobalFlag(0x21); save.SetGlobalFlag(0x0a);
                save.SetDeathRespawnPoint(0, 0xb6, 3, 2, 0x48, 0x78); save.SetRoomFlag(0, 0xb6, 0x50);
                root.SeedFileSlot(0, save);
                selector.RequestPointerSelection(OracleGame.Seasons, true); root.Step(1, false); root.CompleteBootLoading();
                root.Step(1, false); root.Step(1, false, "inventory"); root.Step(32, false);
                root.Step(1, false, "attack"); root.Step(1, false); root.Step(1, false, "attack"); root.Step(32, false);
                var session = root._seasonsOpening ?? throw new InvalidOperationException("Seasons file entry omitted gameplay.");
                Check(new ApplicationValidationFixture(root), session.Link, session.Rooms, session.Entities, root._sound);
                CheckSeasonsSword(new ApplicationValidationFixture(root), session, batched);
                CheckSeasonsDrowning(new ApplicationValidationFixture(root), session, batched);
            }
            finally { root.Free(); }

            void Check(ApplicationValidationFixture app, Player player, RoomSession rooms, RoomEntityManager entities, OracleSoundEngine sound)
            {
                entities.Clear();
                var memory = rooms.RuntimeState;
                string context = $"{memory.Game}, batched={batched}";
                var audit = sound.AttachPlayRequestAudit();
                int modifier = memory.ReadField("wGrassAnimationModifier");
                var even = entities.Spawn<GrassDebrisEffect>(new GrassDebrisSpawn(new(64, 64), Flickers: true));
                var odd = entities.Spawn<GrassDebrisEffect>(new GrassDebrisSpawn(new(80, 64), Flickers: true));
                var rock = entities.Spawn<RockDebrisEffect>(new RockDebrisSpawn(new(64, 64)));
                var rock2 = entities.Spawn<RockDebrisEffect>(new RockDebrisSpawn(new(80, 64), InteractionId.RockDebris2));
                player.World.SpawnDrowningSplash(new(64, 64), HazardType.Water);
                player.World.SpawnDrowningSplash(new(80, 64), HazardType.Lava);
                var water = entities.Entities<SplashEffect>().Single(effect => !effect.IsLava);
                var lava = entities.Entities<SplashEffect>().Single(effect => effect.IsLava);
                FailIf(audit.Requests.Count != 0 || water.Initialized || lava.Initialized,
                    $"{context}: allocation must defer native debris/splash cues to state0.");
                memory.SetField("wGrassAnimationModifier", 0x22);
                Step(1);
                FailIf(even.PaletteIndex != 2 || odd.PaletteIndex != 2 ||
                    audit.RequestsFor(SoundId.SndCutGrass) != 2 || audit.RequestsFor(SoundId.SndBreakRock) != 2 ||
                    audit.RequestsFor(SoundId.SndSplash) != 2 || !water.Initialized || !lava.Initialized,
                    $"{context}: both actual factories must read live grass palette bits and route each state0 cue once.");
                Step(1);
                FailIf(even.Visible != ((memory.FrameCounter & 1) == 0) || even.Visible == odd.Visible,
                    $"{context}: grass flicker must XOR physical interaction-page parity with the global frame.");
                Step(11);
                FailIf(water.Finished || water.CurrentParameter != 0xff, $"{context}: water must retain its terminal frame at update13.");
                Step(1);
                FailIf(!water.Finished, $"{context}: water must delete at update14.");
                Step(3);
                FailIf(rock.Finished || rock2.Finished || rock.CurrentParameter != 0xff || rock2.CurrentParameter != 0xff,
                    $"{context}: both rock IDs must retain their terminal frame at update17.");
                Step(1);
                FailIf(!rock.Finished || !rock2.Finished, $"{context}: rock debris must delete at update18.");
                Step(3);
                FailIf(lava.Finished || lava.CurrentParameter != 0xff, $"{context}: lava must retain its terminal frame at update21.");
                Step(1);
                FailIf(!lava.Finished, $"{context}: lava must delete at update22.");
                Step(11);
                FailIf(even.Finished || odd.Finished || even.CurrentParameter != 0xff,
                    $"{context}: grass must retain its terminal frame at update33.");
                Step(1);
                FailIf(!even.Finished || !odd.Finished || audit.RequestsFor(SoundId.SndCutGrass) != 2 ||
                    audit.RequestsFor(SoundId.SndSplash) != 2 || audit.RequestsFor(SoundId.SndBreakRock) != 2,
                    $"{context}: effects must retire at native boundaries without repeated cues.");
                memory.SetField("wGrassAnimationModifier", (byte)modifier);
                entities.Clear();
                audit.Clear();
                for (int slot = 0; slot < 14; slot++)
                    entities.Spawn<GrassDebrisEffect>(new GrassDebrisSpawn(new(64, 64)));
                player.World.SpawnDrowningSplash(new(64, 64), HazardType.Water);
                FailIf(entities.InteractionSlotAvailable || entities.Entities<SplashEffect>().Count != 0 || audit.Requests.Count != 0,
                    $"{context}: exhausted INTERACTION pages must reject a splash without a cue.");
                entities.Clear();

                // Approach an existing blocking tile through its actual geometry.
                var passability = new ItemCliffDatabase(rooms.AssetRoot);
                Vector2? start = null;
                for (int y = 16; y < 112 && start is null; y++)
                for (int x = 16; x < 141; x++)
                {
                    Vector2 point = new(x, y), next = point + Vector2.Right * 3;
                    byte tile = rooms.CurrentRoom.GetMetatile(next);
                    if (!rooms.CurrentRoom.IsSolid(point) && rooms.CurrentRoom.IsSolid(next) &&
                        !passability.CanPassWithoutElevation(rooms.CurrentRoom.ActiveCollisions, tile) &&
                        !passability.TryGetDelta(rooms.CurrentRoom.ActiveCollisions, tile, 2, out _))
                    { start = point; break; }
                }
                FailIf(start is null, $"{context}: fixture needs an existing blocking tile beside open floor.");
                FailIf(!entities.TrySpawnSwordBeam(start!.Value - Vector2.Right * 12, 1) ||
                    entities.TrySpawnSwordBeam(start.Value - Vector2.Right * 12, 1), $"{context}: native beam cap changed.");
                var beam = entities.Entities<SwordBeamEffect>().Single();
                FailIf(audit.RequestsFor(SoundId.SndSwordBeam) != 0, $"{context}: beam cue ran during allocation.");
                Step(1);
                FailIf(!beam.Initialized || beam.Position != start.Value || audit.RequestsFor(SoundId.SndSwordBeam) != 1,
                    $"{context}: beam state0 must show without moving and request one native cue.");
                Step(1);
                FailIf(!beam.Finished || entities.Entities<ClinkEffect>().Count != 1,
                    $"{context}: native solid-tile collision must delete the beam and allocate INTERAC_CLINK:$81.");
                Step(10);
                FailIf(entities.Entities<ClinkEffect>().Count != 0 || audit.RequestsFor(SoundId.SndClink) != 0,
                    $"{context}: beam clink must retire without the enemy-clink cue.");
                entities.Clear();
                CheckSharedItemDrops(app, player, rooms, entities, sound, batched);
                void Step(int count) => app.Step(count, Vector2.Zero, batched: batched);
            }
        }
    }

    private static void CheckSeasonsDrowning(ApplicationValidationFixture app, SeasonsOpeningSession session, bool batched)
    {
        Vector2? dry = null;
        // Use a native $d4 shoreline and walk across it, rather than inserting
        // Link into water or changing the room's collision bytes.
        for (int room = 0; room < 256 && dry is null; room++)
        {
            session.LoadRoom(room, 0, spawn: false);
            for (int y = 24; y < 104 && dry is null; y += 16)
            for (int x = 40; x < 136; x += 16)
            {
                Vector2 water = new(x, y), start = water - new Vector2(16, 5);
                if (session.Room.GetMetatile(water) != 0xd4 ||
                    session.Link.World.GetActiveTerrain(start).Terrain.Hazard != HazardType.None ||
                    session.Collision.Collides(start)) continue;
                bool clear = true;
                for (int step = 0; step <= 16; step++)
                    clear &= !session.Collision.Collides(start + Vector2.Right * step);
                if (clear) { dry = start; break; }
            }
        }
        FailIf(dry is null, "Seasons must have a native, reachable $d4 shoreline for the drowning handoff regression.");
        var audit = session.Sound.AttachPlayRequestAudit();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            session.Link.WarpTo(dry!.Value);
            int health = session.Inventory.HealthQuarters;
            audit.Clear();
            EnterWater();
            var splash = session.Entities.Entities<SplashEffect>().Single();
            FailIf(!splash.Initialized || splash.IsLava ||
                audit.RequestsFor(SoundId.SndSplash) != 1 || audit.RequestsFor(SoundId.SndDamageLink) != 1,
                "Seasons real water entry must allocate and dispatch its shared splash and both native cues once.");
            app.Step(40, Vector2.Zero, batched: batched);
            FailIf(session.Link.IsDrowning || session.Inventory.HealthQuarters != health - 2 ||
                session.Entities.Entities<SplashEffect>().Count != 0 || !splash.Finished ||
                audit.RequestsFor(SoundId.SndSplash) != 1 ||
                session.Link.World.GetActiveTerrain(session.Link.Position).Terrain.Hazard != HazardType.None,
                "Seasons drowning must retire its splash, apply one half-heart and return to dry ground before re-entry.");
        }
        session.Link.WarpTo(dry!.Value);
        audit.Clear();
        EnterWater();
        var cancelled = session.Entities.Entities<SplashEffect>().Single();
        session.DebugTravel(0, 0xb6);
        app.Step(1, Vector2.Zero, batched: batched);
        FailIf(session.Link.IsDrowning || session.Entities.Entities<SplashEffect>().Count != 0 ||
            session.Entities.Entities<SplashEffect>().Contains(cancelled) || audit.RequestsFor(SoundId.SndSplash) != 1,
            "Replacing the Seasons room must cancel the old hazard/effect owner without replaying its cue.");

        void EnterWater()
        {
            for (int update = 0; update < 24 && !session.Link.IsDrowning; update++)
                app.Step(1, Vector2.Right, batched: batched);
            FailIf(!session.Link.IsDrowning, $"Seasons native shoreline in room $00:{session.RoomId:x2} was not reachable from dry {dry}.");
        }
    }
}
