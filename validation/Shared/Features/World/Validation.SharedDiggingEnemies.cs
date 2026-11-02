
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedDiggingEnemies(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var memory = rooms.RuntimeState;
        var game = rooms.SaveData.Game;
        var app = new ApplicationValidationFixture(root);
        int originalGroup = rooms.ActiveGroup, originalRoom = rooms.CurrentRoom.Id;
        var initialRandom = entities.Random.CaptureState();
        void Load(int group, int room)
        {
            root._scene.Dialogue.Close();
            if (game == OracleGame.Ages) root.LoadDebugRoom(group, room);
            else root._seasonsOpening!.DebugTravel(group, room);
            entities.Clear(); player.BeginCutsceneControl(); entities.DisableLinkCollisionsAndMenu();
            player.Inventory.EquipA(0); player.Inventory.EquipB(0);
            player.Inventory.SetScriptedActiveRing(0xff);
            player.WarpTo(new(136, 112)); player.Face(Vector2I.Down);
            var tiles = TilePropertyDatabase.For(rooms.AssetRoot);
            byte floor = (byte)Enumerable.Range(0, 256).First(tile => tiles.NativeHazardFlags(rooms.CurrentRoom.ActiveCollisions, (byte)tile) == 0);
            for (int y = 0; y < rooms.CurrentRoom.HeightInTiles; y++)
            for (int x = 0; x < rooms.CurrentRoom.WidthInTiles; x++)
                rooms.CurrentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), floor, 0, 0);
        }
        BraceletRom Reference(int id) => SharedNativeEnemyReference(rooms,player,id);
        try
        {
            foreach (int roll in new[] { 0, 3, 0xdf })
            foreach (bool dug in new[] { false, true })
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Load(0, game == OracleGame.Ages ? 0x98 : 0xc9);
                int id = roll == 0 ? 0x10 : 0x51, subId = dug ? 3 : 2;
                SetDiggingRoll(entities.Random, roll);
                var seed = entities.Random.CaptureState();
                var drop = entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0f, new(40, 40), DugUp: dug));
                var native = Reference(id);
                native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
                native[0xd0c0] = 1; native[0xd0c1] = 1; native[0xd0c2] = 0x0f;
                native[0xd0c3] = (byte)subId; native[0xd0cb] = 40; native[0xd0cd] = 40;
                int updates = 0;
                string context = $"{game}: dug ENEMY${id:x2}:${subId:x2}, repeat={repeat}, batch={batched}";
                void Step(int count)
                {
                    app.Step(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                    {
                        native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); updates++;
                        var rng = entities.Random.CaptureState();
                        FailIf(drop.Finished != (native[0xd0c0] == 0) ||
                            rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != seed.Calls + native.RandomCalls,
                            context + $": update {updates}: native producer retirement or ordered RNG differs.");
                        EnemyCharacter actor;
                        int state, counter, angle, speed, z, speedZ;
                        if (id == 0x10)
                        {
                            var rope = entities.Entities<RopeCharacter>().Single(); actor = rope;
                            state = rope.InitializationPending ? 0 : rope.State switch
                            {
                                RopeState.SpawnSetup => 8,
                                RopeState.SpawnCharge or RopeState.SpawnBounce => 9,
                                RopeState.Wandering => 10,
                                RopeState.Charging => 11,
                                _ => throw new InvalidOperationException(context + ": unexpected native Rope state.")
                            };
                            counter = rope.Counter; angle = rope.Angle; speed = rope.SpeedRaw;
                            z = rope.ZFixed; speedZ = rope.SpeedZ;
                        }
                        else
                        {
                            var beetle = entities.Entities<BeetleCharacter>().Single(); actor = beetle;
                            state = beetle.State; counter = beetle.Counter; angle = beetle.Angle; speed = beetle.Speed;
                            z = beetle.ZFixed; speedZ = beetle.SpeedZ;
                        }
                        const int at = 0xd080;
                        FailIf(native[at] == 0 || native[at + 1] != id || native[at + 2] != subId ||
                            actor.Position != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                            state != native[at + 4] || counter != native[at + 6] || angle != native[at + 9] ||
                            speed != native[at + 16] || (ushort)z != native.Word(at + 14) || (ushort)speedZ != native.Word(at + 20) ||
                            actor.Visible != ((native[at + 0x1a] & 128) != 0),
                            context + $": update {updates}: native birth/AI/XY/Z/visibility differs; " +
                            $"runtime={state:x2}/{counter:x2}/{actor.Position}/${(ushort)z:x4}/${(ushort)speedZ:x4}/angle{angle:x2}/speed{speed:x2}/visible{actor.Visible}, " +
                            $"native={native[at + 4]:x2}/{native[at + 6]:x2}/{native.Word(at + 12):x4},{native.Word(at + 10):x4}/${native.Word(at + 14):x4}/${native.Word(at + 20):x4}/angle{native[at + 9]:x2}/speed{native[at + 16]:x2}/flags{native[at + 0x1a]:x2}.");
                    });
                }
                Step(1);
                FailIf(!drop.Finished || entities.RoomEnemyCount != native[0xcdd1] || entities.RoomEnemyCount != 1 || native[0xd084] != 0,
                    context + ": counted PART-phase allocation must leave the new ENEMY state zero for the next enemy pass.");
                root._scene.Dialogue.ShowMessage("Native enemy initialization and hold.", player.Position.Y);
                native[0xcba0] = 1; Step(1); Step(3);
                root._scene.Dialogue.Close(); native[0xcba0] = 0;
                Step(7);
                Step(72);
            }
            foreach (int mode in new[] { 0, 1, 2 })
            {
                bool rare = mode == 2;
                Load(rare && game == OracleGame.Seasons ? 1 : 0,
                    rare && game == OracleGame.Seasons ? 0 : game == OracleGame.Ages ? 0x98 : 0xc9);
                SetDiggingRoll(entities.Random, rare ? 0xe0 : 3);
                var seed = entities.Random.CaptureState(); var native = Reference(0x51);
                native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
                if (mode == 0) { memory.SetField("wDiggingUpEnemiesForbidden", 0x80); native[0xccde] = 0x80; }
                if (mode == 1)
                {
                    for (int slot = 0; slot < 16; slot++)
                    {
                        var reservation = new ItemPhaseValidationEntity(static () => { });
                        entities.RegisterEnemySlot(reservation, slot); entities.AddEntity(reservation);
                        native[0xd080 + slot * 256] = 1; native[0xd084 + slot * 256] = 8;
                    }
                    root._scene.Dialogue.ShowMessage("Retain the occupied native pool.", player.Position.Y);
                    native[0xcba0] = 1;
                }
                var drop = entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0f, new(40, 40), DugUp: true));
                native[0xd0c0] = 1; native[0xd0c1] = 1; native[0xd0c2] = 0x0f; native[0xd0c3] = 3;
                native[0xd0cb] = native[0xd0cd] = 40;
                app.Step(1, Vector2.Zero, batched: batched);
                native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts();
                var rng = entities.Random.CaptureState();
                bool deleted = !rare || game == OracleGame.Seasons;
                FailIf(drop.Finished != deleted || drop.Finished != (native[0xd0c0] == 0) ||
                    entities.FindFreeEnemySlot() != (mode == 1 ? -1 : 0) ||
                    rng.Calls != seed.Calls + 1 || native.RandomCalls != 1 ||
                    rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95],
                    $"{game}: digging gate mode {mode}, batch={batched}: forbidden/full-pool/rare branches must retain one producer RNG draw and native retirement.");
                memory.SetField("wDiggingUpEnemiesForbidden", 0); memory.SetField("wDisabledObjects", 0);
                root._scene.Dialogue.Close();
            }
            foreach (int repeat in new[] { 0, 1 })
            {
                Load(game == OracleGame.Seasons ? 1 : 0, game == OracleGame.Seasons ? 0 : 0x98);
                SetDiggingRoll(entities.Random, 0);
                var seed = entities.Random.CaptureState();
                if (game == OracleGame.Seasons)
                    entities.Spawn<ItemDropEffect>(new ItemDropSpawn(0x0f, new(72, 72), DugUp: true));
                else
                    FailIf(!entities.TrySpawnDebugEnemy(0x2d, 0, new(72, 72), out string error), error);
                var native = Reference(0x2d);
                native[0xff94] = seed.Rng1; native[0xff95] = seed.Rng2;
                if (game == OracleGame.Seasons)
                {
                    native[0xd0c0] = 1; native[0xd0c1] = 1; native[0xd0c2] = 0x0f; native[0xd0c3] = 3;
                    native[0xd0cb] = native[0xd0cd] = 72;
                }
                else
                {
                    native[0xd080] = 1; native[0xd081] = 0x2d; native[0xd08b] = native[0xd08d] = 72;
                }
                int updates = 0;
                app.Step(1200, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.AdvanceEnemies(entities.FrameCounter); native.AdvanceParts(); updates++;
                    var tower = entities.Entities<PodobooTowerCharacter>().Single();
                    var rng = entities.Random.CaptureState();
                    FailIf(tower.State != native[0xd084] || tower.Counter != native[0xd086] || tower.EmergeCounter != native[0xd087] ||
                        tower.Position != new Vector2(native.Word(0xd08c) / 256f, native.Word(0xd08a) / 256f) ||
                        tower.Visible != ((native[0xd09a] & 128) != 0) ||
                        rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != seed.Calls + native.RandomCalls,
                        $"{game}: Podoboo Tower repeat={repeat}, batch={batched}, update {updates}: native selection/state/counter/XY/visibility/RNG differs.");
                    var fires = entities.Entities<ZoraFireProjectile>().Where(fire => !fire.Finished)
                        .OrderBy(fire => fire.Position.X).ThenBy(fire => fire.Position.Y).ToArray();
                    var parts = Enumerable.Range(0xd0, 16).Select(page => (page << 8) + 0xc0)
                        .Where(at => native[at] != 0 && native[at + 1] == 0x31)
                        .OrderBy(at => native.Word(at + 12)).ThenBy(at => native.Word(at + 10)).ToArray();
                    FailIf(fires.Length != parts.Length, $"{game}: tower update {updates}: native PART$31 allocation/deletion differs.");
                    for (int index = 0; index < fires.Length; index++)
                    {
                        var fire = fires[index]; int at = parts[index];
                        FailIf(fire.AssetRoot != rooms.AssetRoot || fire.PartId != 0x31 || fire.State != native[at + 4] ||
                            fire.Counter != native[at + 6] || fire.Angle != native[at + 9] ||
                            SomariaPrivate<OracleObjectPosition>(fire, "_position").PrecisePosition != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                            SomariaPrivate<int>(fire, "_palette") != (native[at + 0x1c] & 7),
                            $"{game}: tower update {updates}: native PART$31 birth, targeting, movement or palette differs; " +
                            $"runtime={fire.State}/{fire.Counter}/angle{fire.Angle:x2}/{SomariaPrivate<OracleObjectPosition>(fire, "_position").PrecisePosition}/palette{SomariaPrivate<int>(fire, "_palette")}, " +
                            $"native={native[at + 4]}/{native[at + 6]}/angle{native[at + 9]:x2}/{native.Word(at + 12):x4},{native.Word(at + 10):x4}/flags{native[at + 0x1c]:x2}.");
                    }
                });
            }
        }
        finally
        {
            memory.SetField("wDisabledObjects", 0); memory.SetField("wDiggingUpEnemiesForbidden", 0); player.EndCutsceneControl();
            Load(originalGroup, originalRoom); player.EndCutsceneControl();
            entities.EnableLinkCollisionsAndMenu();
            entities.Random.RestoreState(initialRandom);
        }
    }
}
