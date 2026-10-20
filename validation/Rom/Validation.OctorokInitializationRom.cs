using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private IRoomEntity CreateOctorokInitializationActor(int sub)
    {
        var database = EnemyDatabase.Shared;
        var source = new RoomObjectRecord(0, 0x33, 0, RoomObjectKind.FixedEnemy,
            9, sub, 2, 1, 72, 80, 0x47, 0);
        return new RoomEnemyFactory(_entities, database, new RoomEntityResources(_rooms), _rooms)
            .CreateOrderedEnemy(database.EnemyHandlers.ResolveHandler(source), source,
                _currentRoom, new(80.25f, 72.5f), 0, 0, EnemyPlacementContext.Unrestricted)
            ?? throw new InvalidOperationException($"Missing Octorok $09:${sub:x2} initializer.");
    }

    private void ValidateOctorokInitializationRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int sub in new[] { 0, 1, 2 })
        foreach (int seed in new[] { 0x0d37, 0x40ff })
        foreach (string gate in new[] { "none", "text", "mask04", "mask80", "palette", "scent" })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0);
            // A declared clear floor isolates initialization from terrain AI.
            byte floor = Enumerable.Range(0, _currentRoom.HeightInTiles)
                .SelectMany(y => Enumerable.Range(0, _currentRoom.WidthInTiles)
                    .Select(x => _currentRoom.GetTerrainInfo(new(x * 16 + 8, y * 16 + 8))))
                .First(info => info.Collision == 0 && info.Type == TerrainType.Normal && info.Hazard == HazardType.None).Tile;
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), floor, null, 0);
            _player.WarpTo(new(24, 24)); _player.BeginCutsceneControl();
            EmberSeedEffect? lure = null;
            if (gate == "scent")
            {
                var record = new SeedSatchelDatabase().Scent;
                lure = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(new(128, 106), Vector2I.Up, record, 0));
                for (int wait = 0; lure.ScentTarget is null && wait < 16; wait++)
                    StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                FailIf(lure.ScentTarget is null, "Initialization fixture must publish a real landed Scent Seed target.");
            }
            _random.RestoreState(_random.CaptureState() with { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8) });
            var initial = _random.CaptureState();
            var native = new SomariaRom(_saveData, initial, _currentRoom, 0, 24, 24);
            var adapter = CreateOctorokInitializationActor(sub);
            var actor = (OctorokCharacter)adapter.Node;
            _entities.RegisterEnemySlot(adapter, 0); _entities.AddEntity(adapter);
            FailIf(_random.Calls != initial.Calls || (int)actor.State != 0 || actor.Visible,
                $"Octorok $09:${sub:x2} construction must remain hidden/state0 without consuming RNG.");
            native[0xd080] = 1; native[0xd081] = 9; native[0xd082] = (byte)sub;
            native.Word(0xd08a, 0x4880); native.Word(0xd08c, 0x5040);
            // Native seed producer's published target is a declared input;
            // its animation/lifetime is not being compared by this scenario.
            if (lure?.ScentTarget is { } target)
            {
                native[0xccd9] = 1; native[0xffb2] = (byte)target.Y; native[0xffb3] = (byte)target.X;
            }
            var previousText = _entities.TextActiveSource;
            var previousPalette = _entities.PaletteFadeActiveSource;
            bool held = gate is not ("none" or "scent");
            _entities.TextActiveSource = () => held && gate == "text";
            _entities.PaletteFadeActiveSource = () => held && gate == "palette";
            void Gates()
            {
                native[0xcba0] = held && gate == "text" ? (byte)1 : (byte)0;
                native[0xc4ab] = held && gate == "palette" ? (byte)1 : (byte)0;
                native[0xcc8a] = held && gate == "mask04" ? (byte)4 : held && gate == "mask80" ? (byte)0x80 : (byte)0;
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, native[0xcc8a]);
            }
            int update = 0;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.AdvanceEnemies(_entities.FrameCounter); update++;
                var rng = _random.CaptureState();
                FailIf((int)actor.State != native[0xd084] || actor.Counter1 != native[0xd086] ||
                    actor.Angle != native[0xd089] || SomariaPrivate<int>(actor, "_walkCounter") != native[0xd0b0] ||
                    actor.ScentAttractionCounter != native[0xd0bd] ||
                    actor.Position != new Vector2(native.Word(0xd08c) / 256f, native.Word(0xd08a) / 256f) ||
                    actor.Visible != ((native[0xd09a] & 0x80) != 0) ||
                    rng.Calls - initial.Calls != native.RandomCalls || rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95],
                    $"Octorok $09:${sub:x2} seed=${seed:x4}, gate={gate}, update={update}, batch={batched}: " +
                    $"port state/counter/angle/walk/var3d={(int)actor.State:x2}/{actor.Counter1}/{actor.Angle:x2}/{SomariaPrivate<int>(actor, "_walkCounter")}/{actor.ScentAttractionCounter:x2}, XY={actor.Position}, RNG={rng.Calls - initial.Calls}; " +
                    $"source={native[0xd084]:x2}/{native[0xd086]}/{native[0xd089]:x2}/{native[0xd0b0]}/{native[0xd0bd]:x2}, XY=({native.Word(0xd08c):x4},{native.Word(0xd08a):x4}), RNG={native.RandomCalls}.");
            });
            try
            {
                Gates(); Step(1);
                FailIf(actor.State != OctorokState.Walking || actor.Position != new Vector2(80.25f, 72.5f) || native.RandomCalls != 2,
                    "Octorok first dispatch must consume common var3d then species RNG, ignore scent until enabled, and yield without moving.");
                Step(3);
                held = false; Gates(); Step(gate == "scent" ? 32 : 5);
                if (gate == "scent")
                {
                    _entities.RetireEntity(_entities.EntityAdapters<IRoomEntity>().Single(entity => ReferenceEquals(entity.Node, lure)));
                    native[0xccd9] = 0; Step(2);
                }
                else if (gate != "none") { held = true; Gates(); Step(3); }
            }
            finally
            {
                _entities.TextActiveSource = previousText; _entities.PaletteFadeActiveSource = previousPalette;
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); _entities.Clear();
            }
        }

        // Common initialization precedes ecom_checkHazards. Water/lava
        // delete before the species draw; a hole retains state0 and therefore
        // takes the common draw again on the next admitted visit.
        foreach (byte tile in new byte[] { 0xfa, 0xe4, 0xf3 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 24)); _player.BeginCutsceneControl();
            _currentRoom.SetPositionTileAndCollision(new(72, 72), tile, null, 0);
            _currentRoom.SetPositionTileAndCollision(new(88, 72), tile, null, 0);
            var initial = _random.CaptureState();
            var native = new SomariaRom(_saveData, initial, _currentRoom, 0, 24, 24);
            var adapter = CreateOctorokInitializationActor(0); var actor = (OctorokCharacter)adapter.Node;
            _entities.RegisterEnemySlot(adapter, 0); _entities.AddEntity(adapter);
            native[0xd080] = 1; native[0xd081] = 9; native[0xcdd1] = 1; // wNumEnemies.
            native.Word(0xd08a, 0x4880); native.Word(0xd08c, 0x5040);
            int update = 0;
            StepGameplayUpdates(tile == 0xf3 ? 3 : 1, Vector2.Zero, afterUpdate: () =>
            {
                native.AdvanceEnemies(_entities.FrameCounter); update++;
                var rng = _random.CaptureState();
                FailIf(rng.Calls - initial.Calls != update || native.RandomCalls != update ||
                    rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] ||
                    (tile == 0xf3 ? !actor.IsFallingIntoHole || native[0xd084] != 0 || actor.State != OctorokState.Uninitialized
                        : !actor.IsDead || native[0xd080] != 0),
                    $"Octorok state0 tile=${tile:x2} visit={update} must run common RNG before the hazard, without the species draw.");
            });
            _entities.Clear();
        }

        // Explicit preload entry: the native state-zero walk executes with
        // scroll mode $02. Repeated preload must share the same guarded path.
        foreach (int sub in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            var initial = _random.CaptureState();
            var native = new SomariaRom(_saveData, initial, _currentRoom, 0, 24, 24);
            var adapter = CreateOctorokInitializationActor(sub);
            try
            {
                native[0xd080] = 1; native[0xd081] = 9; native[0xd082] = (byte)sub;
                native.Word(0xd08a, 0x4880); native.Word(0xd08c, 0x5040); native[0xcd00] = 2;
                var preloader = (IScreenTransitionPreloadRoomEntity)adapter;
                var spawns = new List<RoomEntitySpawn>();
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    FailIf(preloader.PrepareForScreenTransition(spawns) != ScreenTransitionPresentation.Visible,
                        "Octorok's admitted state0 must expose its initialized transition pose.");
                    native.AdvanceEnemies(0);
                }
                var actor = (OctorokCharacter)adapter.Node; var rng = _random.CaptureState();
                FailIf((int)actor.State != native[0xd084] || actor.Counter1 != native[0xd086] || actor.Angle != native[0xd089] ||
                    actor.ScentAttractionCounter != native[0xd0bd] || SomariaPrivate<int>(actor, "_walkCounter") != native[0xd0b0] ||
                    rng.Calls - initial.Calls != 2 || native.RandomCalls != 2 || rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || spawns.Count != 0,
                    "Octorok preload must initialize exactly once, preserve its first pose/counters, and consume exactly the two native draws.");
            }
            finally { adapter.Node.Free(); }
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared Octorok $09:$00-$02 construction, first dispatch, both RNG draws/var3d, text/mask/palette gates, landed scent entry/refresh/exit, split/batched gameplay and declared repeated scroll-preload entries with clean-US updateEnemies.");
    }
}
