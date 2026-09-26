using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonColorGels()
    {
        var data = new EnemyDatabase();
        var record = data.ImportedEnemy(EnemyId.ColorChangingGel, 0);
        var profile = EnemyBehaviorTables.Shared.ColorChangingGel;
        FailIf(record is not { Health: 1, RadiusX: 2, RadiusY: 2, Palette: 2, TileBase: 28 } ||
            !profile.FloorColors.Select(v => v.Value).SequenceEqual(new[] { 0x9d, 2, 0x9e, 6, 0x9f, 1, 0xad, 2, 0xae, 6, 0xaf, 1 }) ||
            !profile.HopOffsets.Select(v => v.Value).SequenceEqual(new[] { -16, -16, -16, 0, -16, 16, 0, -16, 0, 16, 16, -16, 16, 0, 16, 16 }) ||
            !profile.RandomColors.Select(v => v.Value).SequenceEqual(new[] { 2, 6, 1, 6, 255, 255, 1, 2 }),
            "Color Gel source attributes, ordered floor keys, hop offsets or random-color map changed.");
        LoadValidationRoom(4, 0x71);
        Vector2 center = new(120, 88);
        _currentRoom.SetPositionTileAndCollision(center, 0xad, 0, 0);
        var random = new OracleRandom();
        var sounds = new List<int>();
        var gel = new ColorChangingGelCharacter();
        gel.Initialize(record, _currentRoom, center, random, data.ColorChangingGelPalettes, sounds.Add);
        gel.UpdateFrame();
        FailIf(gel.State != ColorChangingGelState.Waiting || gel.Counter != 150 ||
            gel.ColorCounter != 0 || gel.CollisionMode != 0x6e || random.Calls != 1,
            "Gel initialization lost common RNG, source state8, counter150 or mode$6e.");
        for (int i = 0; i < 12; i++) gel.UpdateFrame();
        FailIf(gel.Counter != 138 || gel.ColorCounter != 0, "Matching floor must allow state8 every update and leave counter2 at zero.");
        _currentRoom.SetPositionTileAndCollision(center, 0xaf, 0, 0);
        gel.UpdateFrame();
        FailIf(gel.Color != 2 || gel.CollisionMode != 0x35 || gel.ColorCounter != 90 ||
            gel.Counter != 138 || gel.StoredTile != 0xaf,
            "A changed floor must store its tile, start90, become vulnerable and skip state dispatch.");
        for (int i = 0; i < 88; i++) gel.UpdateFrame();
        FailIf(gel.ColorCounter != 2 || gel.Color != 2 || gel.Counter != 138,
            "Color timer advanced movement or selected a palette before counter2 reached1.");
        gel.UpdateFrame();
        FailIf(gel.ColorCounter != 1 || gel.Color != 1 || gel.CollisionMode != 0x6e || gel.Counter != 137,
            "Counter2=1 must apply the stored blue palette and resume state8 on that same update.");
        gel.UpdateFrame();
        FailIf(gel.Counter != 136 || gel.ColorCounter != 0, "Counter2=0 must remain zero while the floor matches.");

        _currentRoom.SetPositionTileAndCollision(center, 0xae, 0, 0);
        gel.UpdateFrame();
        _currentRoom.SetPositionTileAndCollision(center, 0xda, 0, 0);
        gel.UpdateFrame();
        FailIf(gel.CollisionMode != 0x35 || gel.ColorCounter != 89 || gel.Counter != 136,
            "Somaria must retain the old collision mode and the active timer's state-dispatch suppression.");
        for (int i = 0; i < 88; i++) gel.UpdateFrame();
        FailIf(gel.Color != 6 || gel.CollisionMode != 0x35 || gel.Counter != 135,
            "Somaria's return-Z path must allow state dispatch at counter2=1 without rewriting collision mode.");
        _currentRoom.SetPositionTileAndCollision(center, 0xae, 0, 0);
        gel.UpdateFrame();
        int before = gel.Counter;
        FailIf(!gel.TakeSwitchHookHit(2) || gel.Health != 1 || gel.InvincibilityCounter != 0 || sounds.Count != 0,
            "Matching-color hook effect1c must only queue the hit signal.");
        gel.UpdateFrame();
        FailIf(gel.InvincibilityCounter != -11 || gel.Counter != before - 1 ||
            !sounds.SequenceEqual(new[] { SoundId.SndDamageEnemy }),
            "Gel JUST_HIT must write -12 before common advancement and continue its normal handler.");
        for (int i = 0; i < 11; i++) gel.UpdateFrame();
        var source = data.GetRoomObjects(4, 0x71).First(r => r.Id == 0x47);
        var adapter = new ColorChangingGelRoomEntity(gel,
            data.EnemyHandlers.ResolveHandler(source).CombatSource(source, 0), sounds.Add);
        var spawns = new List<RoomEntitySpawn>();
        foreach (int seed in new[] { 0x20, 0x21, 0x22 })
        {
            FailIf(adapter.ApplySeedHit(gel.CollisionBounds, center, seed, spawns) != SeedHitResult.Activate,
                $"Matching Gel did not consume seed ${seed:x2} through effect20.");
            gel.UpdateFrame();
            FailIf(gel.Health != 1 || gel.InvincibilityCounter != 0 || adapter.IsSeedBurning || sounds.Count != 1,
                $"Matching seed ${seed:x2} incorrectly damaged, burned or produced sword deflection.");
        }
        FailIf(gel.InvincibilityCounter != 0 ||
            adapter.ApplySeedHit(gel.CollisionBounds, center, ItemId.MysterySeed, spawns) != SeedHitResult.Activate,
            "Gel did not accept a Mystery Seed after deflection expired.");
        long calls = random.Calls;
        gel.UpdateFrame();
        FailIf(gel.Color is not (1 or 2) || random.Calls != calls + 1 || gel.Health != 1,
            "Mystery hit must consume exactly one RNG value and choose one of the other two colors.");
        gel.Swallow();
        adapter.OnFinished(spawns);
        FailIf(spawns.OfType<EnemyDeathPuffSpawn>().Any(), "Dimitri swallowing a Gel must not create the normal hit-death puff.");
        gel.Free();

        // A cardinal hop moves until both high bytes are within +/-1 of its
        // target. Height integrates -$180 with $30 gravity for17 updates.
        LoadValidationRoom(4, 0x71);
        center = Enumerable.Range(1, 9).SelectMany(y => Enumerable.Range(1, 13).Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
            .First(p => Enumerable.Range(-1, 3).All(y => Enumerable.Range(-1, 3).All(x =>
                _currentRoom.GetTerrainInfo(p + new Vector2(x * 16, y * 16)).Collision == 0 &&
                _currentRoom.GetTerrainInfo(p + new Vector2(x * 16, y * 16)).Hazard == HazardType.None)));
        _currentRoom.SetPositionTileAndCollision(center, 0xad, 0, 0);
        random = new OracleRandom();
        var prediction = new OracleRandom();
        prediction.Next();
        int hopIndex = prediction.Next().Value & 0x0e;
        var offsets = new Vector2[] { new(-16,-16), new(0,-16), new(16,-16), new(-16,0), new(16,0), new(-16,16), new(0,16), new(16,16) };
        Vector2 target = center + offsets[hopIndex / 2];
        _currentRoom.SetPositionTileAndCollision(target, 0xad, 0, 0);
        gel = new ColorChangingGelCharacter();
        gel.Initialize(record, _currentRoom, center, random, data.ColorChangingGelPalettes, _ => { });
        gel.UpdateFrame();
        for (int i = 0; i < 149; i++) gel.UpdateFrame();
        FailIf(gel.Counter != 1 || random.Calls != 1, "Gel chose its hop before the150-update wait expired.");
        var retryRandom = random.CaptureState();
        _currentRoom.SetPositionTileAndCollision(target, 0xad, 1, 0);
        gel.UpdateFrame();
        FailIf(gel.State != ColorChangingGelState.Waiting || gel.Counter != 1 || gel.HopTarget != target,
            "Gel must retain the attempted high-byte target and retry next update when any collision bit is set.");
        _currentRoom.SetPositionTileAndCollision(target, 0xad, 0, 0);
        random.RestoreState(retryRandom);
        gel.UpdateFrame();
        FailIf(gel.State != ColorChangingGelState.Preparing || gel.Counter != 60 || gel.HopTarget != target || random.Calls != 2,
            "Gel hop target lost source offset order or common-before-hop RNG consumption.");
        for (int i = 0; i < 59; i++) gel.UpdateFrame();
        FailIf(gel.State != ColorChangingGelState.Preparing || gel.Counter != 1 || gel.Position != center,
            "Gel moved during its60-update preparation.");
        gel.UpdateFrame();
        FailIf(gel.State != ColorChangingGelState.Hopping || gel.ZHigh != 0, "Hop entry must precede its first Z integration.");
        for (int i = 0; i < 16; i++) gel.UpdateFrame();
        FailIf(gel.State != ColorChangingGelState.Hopping || gel.ZHigh != -2,
            "Gel must remain airborne at Z=-$0180 on hop update16.");
        gel.UpdateFrame();
        FailIf(gel.State != ColorChangingGelState.Waiting || gel.ZHigh != 0 || gel.Position != target || gel.Counter != 150,
            "Gel hop17 must center both coordinates and restore the150-update wait.");
        gel.Free();

        ValidateSkullColorGelHookLoop();
        ValidateColorGelScreenTransition();
    }

    private void ValidateColorGelScreenTransition()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count = 1, Action? observe = null)
        {
            input.CaptureForValidation([], [], Vector2.Zero);
            scheduler.Advance(count / 60.0, () => { update(); observe?.Invoke(); });
        }
        string State(ColorChangingGelCharacter gel) =>
            $"{gel.Position}/{gel.State}/{gel.Counter}/{gel.ColorCounter}/{gel.Color}/{gel.CollisionMode}/{gel.ZHigh}/{gel.AnimationIndex}/{gel.AnimationFrame}/{gel.Visible}";

        // objects/ages/enemyData.s: seven positioned $47:$00 in 4:3e,
        // five random ($a0) in 4:71, four positioned in 5:3f.
        foreach (var fixture in new[] { (Group: 4, Room: 0x3e, Count: 7), (Group: 4, Room: 0x71, Count: 5), (Group: 5, Room: 0x3f, Count: 4) })
        {
            var random = CaptureOracleRandomForValidation();
            string[] Run(bool batched)
            {
                RestoreOracleRandomForValidation(random);
                _saveData.SetRoomFlag(fixture.Group, fixture.Room, 0xff, false);
                LoadValidationRoom(fixture.Group, fixture.Room);
                var direction = new[] { Vector2I.Down, Vector2I.Up, Vector2I.Left, Vector2I.Right }
                    .First(d => _rooms.TryGetNeighbor(d, out _));
                _rooms.TryGetNeighbor(direction, out int sourceRoom);
                LoadValidationRoom(fixture.Group, sourceRoom);
                _transitions.BeginScroll(_player, -direction, fixture.Room);
                var gels = _entities.Entities<ColorChangingGelCharacter>().ToArray();
                FailIf(gels.Length != fixture.Count, $"{fixture.Group}:{fixture.Room:x2} lost its source Gel count.");
                foreach (var gel in gels)
                    FailIf(!gel.Visible || gel.State != ColorChangingGelState.Waiting || gel.Counter != 150 ||
                        gel.ColorCounter != 0 || gel.Color != 2 || gel.CollisionMode != 0x6e || gel.AnimationIndex != 3,
                        $"{fixture.Group}:{fixture.Room:x2} Gel preload lost source state $08/counter 150/animation $03: {State(gel)}.");
                var frozen = gels.Select(State).ToArray();
                long calls = _random.Calls;
                foreach (var adapter in _entities.EntityAdapters<ColorChangingGelRoomEntity>())
                    adapter.PrepareForScreenTransition(new List<RoomEntitySpawn>());
                FailIf(_random.Calls != calls || !gels.Select(State).SequenceEqual(frozen), "Initialized Gel preload must be idempotent.");
                void CheckFrozen() => FailIf(!gels.Select(State).SequenceEqual(frozen), "Gel advanced during scrolling.");
                int total = _transitions.ScrollTotalFrames;
                if (batched) Step(total, CheckFrozen);
                else for (int i = 0; i < total; i++) Step(1, CheckFrozen);
                FailIf(_transitions.ScrollActive || gels.Any(g => g.TransitionDrawOffset != Vector2.Zero), "Gel scroll did not finish.");
                Step();
                FailIf(gels.Any(g => !g.Visible || g.Counter != 149 || g.State != ColorChangingGelState.Waiting),
                    "Gel reinitialized instead of resuming its wait after scrolling.");
                return gels.Select(State).Append(_random.Calls.ToString()).ToArray();
            }
            FailIf(!Run(false).SequenceEqual(Run(true)), "Gel scroll/re-entry differs between individual and batched updates.");
        }

        // colorChangingGel_updateColor discards state dispatch on a mismatching
        // floor until counter2=1. State-zero common properties/RNG reload on
        // every retry; the successful retry sets counter1=150 and counter2=0.
        LoadValidationRoom(4, 0x71);
        var delayed = _entities.Entities<ColorChangingGelCharacter>().First();
        _currentRoom.SetPositionTileAndCollision(delayed.Position, 0xaf, 0, 0);
        var adapterDelayed = _entities.EntityAdapters<ColorChangingGelRoomEntity>().First();
        long before = _random.Calls;
        FailIf(adapterDelayed.PrepareForScreenTransition(new List<RoomEntitySpawn>()) != ScreenTransitionPresentation.Hidden ||
            delayed.State != ColorChangingGelState.Uninitialized || delayed.ColorCounter != 90 || _random.Calls != before + 1,
            "Blue-floor Gel must retain hidden state $00 and its 90-update color delay.");
        for (int i = 0; i < 88; i++) adapterDelayed.UpdateDuringScreenTransition();
        FailIf(delayed.Visible || delayed.ColorCounter != 2 || _random.Calls != before + 89,
            "Hidden Gel state-zero retries lost their color counter or RNG calls.");
        adapterDelayed.UpdateDuringScreenTransition();
        FailIf(!delayed.Visible || delayed.Color != 1 || delayed.Counter != 150 || delayed.ColorCounter != 0 ||
            delayed.State != ColorChangingGelState.Waiting || _random.Calls != before + 90,
            "Blue-floor Gel did not complete state zero at counter2=1.");
        string complete = State(delayed);
        adapterDelayed.UpdateDuringScreenTransition();
        FailIf(State(delayed) != complete || _random.Calls != before + 90, "Initialized blue Gel advanced during scrolling.");
        LoadValidationRoom(4, 0x70);
        FailIf(_entities.OutgoingEntities<ColorChangingGelCharacter>().Count != 0, "Room replacement retained scrolling Gels.");
    }

    private void ValidateSkullColorGelHookLoop()
    {
        void Step(int n = 1, bool press = false) =>
            StepGameplayUpdates(n, Vector2.Zero, press ? ["attack"] : [], press ? ["attack"] : [], batched: true);
        _inventory.GiveTreasure(TreasureId.SwitchHook, 1);
        _inventory.EquipA(TreasureId.SwitchHook);
        var random = CaptureOracleRandomForValidation();
        (int Health, bool Dead, int Mode, int Counter) Run(bool vulnerable, bool batch)
        {
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, 0x71);
            ColorChangingGelCharacter? gel = null;
            foreach (var candidate in _entities.Entities<ColorChangingGelCharacter>())
            {
                foreach (Vector2I direction in new[] { Vector2I.Down, Vector2I.Up, Vector2I.Left, Vector2I.Right })
                {
                    Vector2 origin = candidate.Position + (Vector2)direction * 24;
                    Vector2 side = new(-direction.Y, direction.X);
                    if (origin.X < 12 || origin.X >= _currentRoom.Width - 12 || origin.Y < 12 || origin.Y >= _currentRoom.Height - 12) continue;
                    if (!Enumerable.Range(0, 25).All(i => Enumerable.Range(-5, 11).All(j =>
                        !_currentRoom.IsSolid(candidate.Position + (Vector2)direction * i + side * j) &&
                        _currentRoom.GetTerrainInfo(candidate.Position + (Vector2)direction * i + side * j).Hazard == HazardType.None))) continue;
                    if (_entities.Entities<EnemyCharacter>().Any(e => e != candidate && e.Position.DistanceTo(origin) < 24)) continue;
                    _player.WarpTo(origin);
                    _player.Face(-direction);
                    gel = candidate;
                    break;
                }
                if (gel is not null) break;
            }
            if (gel is null) throw new InvalidOperationException("No native 4:71 Gel hook corridor.");
            Step();
            if (vulnerable)
            {
                _currentRoom.SetPositionTileAndCollision(gel.Position, 0xaf, 0, 0);
                Step();
            }
            Vector2 originPosition = _player.PrecisePosition;
            Step(press: true);
            var hook = _entities.SwitchHook!.Item!;
            while (hook.State == 1 && gel.InvincibilityCounter == 0 && !hook.Finished) Step();
            // Invincibility appears late immediately for damage, one update
            // later for a matching-color deflection handled by enemyCode47.
            FailIf(gel.Health != (vulnerable ? 0 : 1) || gel.IsDead ||
                gel.InvincibilityCounter != (vulnerable ? 32 : -11) || _entities.SwitchHook.ExchangeActive,
                "Gel hook collision lost the matching/vulnerable damage boundary.");
            if (vulnerable)
            {
                Step();
                FailIf(gel.IsDead || gel.InvincibilityCounter != 31 || hook.State != 2,
                    "Lethal Gel hook damage must still execute JUST_HIT before death.");
                Step();
                FailIf(!gel.IsDead || _entities.Entities<EnemyDeathPuffEffect>().Count != 1,
                    "Gel health-zero dispatch failed to create exactly one death puff.");
            }
            else
            {
                if (batch) Step(11); else for (int i = 0; i < 11; i++) Step();
                FailIf(gel.InvincibilityCounter != 0, "Matching Gel hook invincibility lasted beyond its12 source updates.");
                for (int i = 0; _player.IsUsingSwitchHook && i < 80; i++) Step();
                Step(press: true);
                var second = _entities.SwitchHook.Item!;
                FailIf(second == hook, "Gel blocked a repeated hook action after completion.");
                for (int i = 0; !second.Finished && i < 120; i++) Step();
                FailIf(!second.Finished || gel.Health != 1, "Repeated matching Gel hook should finish without damage.");
            }
            FailIf(_player.PrecisePosition != originPosition || _entities.SwitchHook.ExchangeActive,
                "Gel hook collision incorrectly exchanged Link's position.");
            return (gel.Health, gel.IsDead, gel.CollisionMode, gel.Counter);
        }
        FailIf(Run(false, false) != Run(false, true), "Batched Gel deflection differs from individual updates.");
        Run(true, false);
    }
}
