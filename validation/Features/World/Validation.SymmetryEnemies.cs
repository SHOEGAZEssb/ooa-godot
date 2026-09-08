using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSymmetryEnemies()
    {
        var database = new EnemyDatabase();
        // enemyData.s: the village and its west/south approaches in both eras.
        int[] rooms = [0x00, 0x01, 0x02, 0x03, 0x04, 0x10, 0x11, 0x12, 0x13, 0x14,
            0x20, 0x21, 0x22, 0x23, 0x24];
        foreach (int group in new[] { 0, 1 })
        foreach (int room in rooms)
        {
            _saveData.SetRoomFlag(group, room, 1, false);
            LoadValidationRoom(group, room);
            var placements = database.GetRoomObjects(group, room)
                .Where(row => row.Kind is RoomObjectKind.RandomEnemy or RoomObjectKind.FixedEnemy).ToArray();
            FailIf(placements.Any(row => !database.EnemyHandlers.ResolveHandler(row).SupportsOrderedConstruction),
                $"Symmetry ${group:x}:${room:x2} retains an unsupported enemy placement.");
            int Count(int id) => placements.Where(row => row.Id == id).Sum(row => row.Count);
            FailIf(_entities.Entities<ArrowDarknutCharacter>().Count != Count(0x21) ||
                _entities.Entities<PodobooTowerCharacter>().Count != Count(0x2d) ||
                _entities.Entities<LeeverCharacter>().Count != Count(0x0b) ||
                _entities.Entities<RiverZoraCharacter>().Count != Count(0x08),
                $"Symmetry ${group:x}:${room:x2} lost its ordered enemy placements.");
            foreach (var tower in _entities.Entities<PodobooTowerCharacter>())
                FailIf(!placements.Any(row => row.Id == 0x2d && tower.Position == new Vector2(row.X, row.Y)),
                    $"Symmetry ${group:x}:${room:x2} moved a fixed Podoboo Tower.");
        }
        foreach (int room in new[] { 0x02, 0x03, 0x04, 0x12, 0x14 })
        {
            _saveData.SetRoomFlag(0, room, 1, true);
            LoadValidationRoom(0, room);
            FailIf(_entities.Entities<PodobooTowerCharacter>().Count != 0,
                $"Restored Symmetry $0:${room:x2} still spawned a Podoboo Tower.");
        }
        _saveData.SetRoomFlag(0, 0x02, 1, false);
        _entities.BeginScreenTransition(0, _world.LoadRoom(0, 0x02), Vector2.Right * 160);
        var incomingTowers = _entities.Entities<PodobooTowerCharacter>();
        FailIf(incomingTowers.Count != 2 || incomingTowers.Any(t => t.Visible || t.State != 8 || t.Counter != 60),
            "Podoboo incoming-room preload did not initialize hidden state $08.");
        for (int i = 0; i < 8; i++) _entities.Update(1.0 / 60.0, _player);
        FailIf(incomingTowers.Any(t => t.Counter != 60 || t.Visible), "Incoming Podoboo towers advanced while scrolling.");
        _entities.FinishScreenTransition();
        _entities.BeginScreenTransition(0, _world.LoadRoom(0, 0x01), Vector2.Right * 160);
        var incomingDarknuts = _entities.Entities<ArrowDarknutCharacter>();
        var positions = incomingDarknuts.Select(t => t.Position).ToArray();
        var counters = incomingDarknuts.Select(t => t.Counter).ToArray();
        int randomCalls = _entities.RandomCalls;
        for (int i = 0; i < 8; i++) _entities.Update(1.0 / 60.0, _player);
        FailIf(incomingDarknuts.Count != 3 || incomingDarknuts.Any(t => !t.Visible || t.State != ArrowMoblinState.Moving) ||
            !positions.SequenceEqual(incomingDarknuts.Select(t => t.Position)) ||
            !counters.SequenceEqual(incomingDarknuts.Select(t => t.Counter)) || _entities.RandomCalls != randomCalls,
            "Incoming Darknuts failed to initialize or advanced movement/RNG while scrolling.");
        _entities.FinishScreenTransition();
        GD.Print("Validated Symmetry enemy placements in both eras and restoration-conditioned tower removal.");
    }

    private void ValidateArrowDarknuts()
    {
        var database = new EnemyDatabase();
        OracleRoomData room = _world.LoadRoom(0, 0x11);
        FailIf(database.ImportedEnemy(0x21, 0) is not { Health: 6, DamageQuarters: 2, Palette: 2 } ||
            database.ImportedEnemy(0x21, 1) is not { Health: 9, DamageQuarters: 4, Palette: 1 },
            "enemy21SubidData/enemy48SubidData alias lost red/blue Darknut attributes.");
        var source = database.GetRoomObjects(0, 0x11).Single(row => row.Id == 0x21);
        foreach (int subid in new[] { 0, 1 })
        {
            var random = new OracleRandom();
            var predictor = new OracleRandom();
            var enemy = new ArrowDarknutCharacter();
            enemy.Initialize(database.ImportedEnemy(0x21, subid), room, new Vector2(0x88, 0x18), random);
            int angle = predictor.NextCardinalAngle();
            int counter = 0x30 + (predictor.Next().Value & 0x3f);
            Vector2 target = new(0, enemy.Position.Y);
            enemy.UpdateFrame(target, target);
            FailIf(enemy.State != ArrowMoblinState.Moving || enemy.Angle != angle || enemy.Counter != counter || random.Calls != 2,
                $"Arrow Darknut $21:${subid:x2} lost its two-call initialization or followed scent.");
            bool sawHoming = false, sawRandom = false, sawRetainedAnimation = false;
            for (int cycle = 1; cycle <= 24; cycle++)
            {
                for (int i = 0; i < 120 && enemy.State == ArrowMoblinState.Moving; i++) enemy.UpdateFrame(target, target);
                FailIf(enemy.State != ArrowMoblinState.Turning || enemy.Counter != 8,
                    "Arrow Darknut did not enter the shared eight-update stand.");
                int calls = random.Calls;
                for (int i = 0; i < 7; i++) enemy.UpdateFrame(target, target);
                FailIf(enemy.Counter != 1 || random.Calls != calls, "Arrow Darknut turned early or consumed RNG while standing.");
                bool homing = (predictor.Next().Value & 3) == 0;
                sawHoming |= homing;
                sawRandom |= !homing;
                target = new Vector2(0, enemy.Position.Y);
                angle = homing ? 0x18 : predictor.NextCardinalAngle();
                counter = 0x30 + (predictor.Next().Value & 0x3f);
                int previousAngle = enemy.Angle;
                int previousFrame = enemy.AnimationFrame;
                if (angle == previousAngle)
                {
                    // Pin a nonterminal clock so this checks preservation
                    // even if the current frame happens to be frame zero.
                    enemy.Animation.SetFrameCounter(2);
                    sawRetainedAnimation = true;
                }
                int shot = enemy.UpdateFrame(target, target);
                FailIf(enemy.Angle != angle || enemy.Counter != counter || random.Calls != predictor.Calls ||
                    enemy.MoveCycles != cycle || shot != ((cycle & 1) != 0 && angle == 0x18 ? angle : -1),
                    $"Arrow Darknut $21:${subid:x2} cycle {cycle}: angle ${enemy.Angle:x2}/${angle:x2}, counter {enemy.Counter}/{counter}, RNG {random.Calls}/{predictor.Calls}, shots {shot}, position {enemy.Position}, target {target}.");
                if (angle == previousAngle)
                {
                    FailIf(enemy.AnimationFrame != previousFrame, "Same-direction Darknut route restarted its animation frame.");
                    enemy.Animation.Advance();
                    FailIf(enemy.AnimationFrame != previousFrame, "Same-direction Darknut route lost the remaining animation counter.");
                    enemy.Animation.Advance();
                    FailIf(enemy.AnimationFrame == previousFrame, "Same-direction Darknut route restarted its 12-update walk clock.");
                }
            }
            FailIf(!sawHoming || !sawRandom || !sawRetainedAnimation, "Darknut regression did not exercise both direction branches and retained animation.");
            enemy.Free();
        }
        var fighter = new ArrowDarknutCharacter();
        fighter.Initialize(database.ImportedEnemy(0x21), room, new Vector2(0x88, 0x18), new OracleRandom());
        fighter.UpdateFrame(Vector2.Zero);
        var adapter = new ArrowDarknutRoomEntity(fighter,
            database.EnemyHandlers.ResolveHandler(source).CombatSource(source, 1), _ => { });
        var spawns = new List<RoomEntitySpawn>();
        FailIf(adapter.ApplySeedHit(fighter.CollisionBounds, Vector2.Zero, 0x20, spawns) != SeedHitResult.None || adapter.IsSeedBurning,
            "Arrow Darknut accepted fire despite collision row $20.");
        adapter.SetLinkSwordState(SwordActionState.None, 1);
        FailIf(!adapter.ApplySwordHit(fighter.CollisionBounds, fighter.Position - Vector2.Right * 16, 1,
            EnemyKnockbackStrength.High, spawns) || fighter.KnockbackCounter != EnemyBehaviorTables.Shared.EnemySwordDamageProfiles[0].Second,
            "Arrow Darknut level-one sword hit did not use effect $08 low recoil.");
        fighter.Free();
        GD.Print("Validated red/blue Arrow Darknut RNG branches, scent immunity, alternating fire, and low sword recoil/fire immunity.");
    }

    private void ValidatePodobooTowers()
    {
        var database = new EnemyDatabase();
        var source = database.GetRoomObjects(0, 0x02).First(row => row.Id == 0x2d);
        var random = new OracleRandom();
        var predictor = new OracleRandom();
        var tower = new PodobooTowerCharacter();
        var position = new Vector2(source.X, source.Y);
        tower.Initialize(database.ImportedEnemy(0x2d), position, random);
        var spawns = new List<RoomEntitySpawn>();
        int frame = 0;
        void Step() => tower.UpdateFrame(++frame, spawns);
        Step();
        FailIf(tower.State != 8 || tower.Counter != 60 || tower.CollisionEnabled, "Podoboo $2d initialization lost its dormant counter/collision state.");
        for (int i = 1; i <= 59; i++)
        {
            Step();
            FailIf(tower.Visible != ((i & 1) != 0) || tower.CollisionEnabled || tower.State != 8,
                "Podoboo $2d flicker or collision changed before update 60.");
        }
        Step();
        FailIf(tower.State != 9 || !tower.CollisionEnabled, "Podoboo $2d failed to begin rising on update 60.");
        Step();
        FailIf(tower.CollisionBounds.Size != new Vector2(8, 12) || tower.Position != position,
            "Podoboo initial animation parameter $03 lost ground-level collision radii.");
        for (int i = 0; i < 3; i++) Step();
        FailIf(tower.Position.Y != position.Y - 7 || tower.CollisionBounds.Size != new Vector2(8, 16),
            "Podoboo animation parameter $06 did not change world Y/radii after four animation updates.");
        for (int i = 0; i < 12; i++) Step();
        FailIf(tower.State != 10 || tower.Counter != 149 || tower.Position.Y != position.Y - 14 ||
            tower.CollisionBounds.Size != new Vector2(8, 36), "Podoboo fully emerged state lost its fallthrough update or tall collision bounds.");
        int remaining = 180 - ((frame & 3) == 0 ? 1 : 0);
        int fireCounter = 149;
        int expectedShots = 0;
        while (tower.State == 10)
        {
            int next = frame + 1;
            if ((next & 3) == 0) remaining--;
            if (remaining != 0 && --fireCounter == 0)
            {
                fireCounter = 150;
                if (predictor.Next().Value < 0xb4) expectedShots++;
            }
            Step();
            FailIf(tower.EmergeCounter != remaining || random.Calls != predictor.Calls ||
                spawns.OfType<ZoraFireSpawn>().Count() != expectedShots,
                "Podoboo global fourth-frame counter or 150-update probabilistic fire diverged.");
        }
        FailIf(tower.State != 11 || tower.AnimationIndex != 1, "Podoboo failed to begin sinking after 180 global fourth-frame ticks.");
        for (int i = 0; i < 20; i++) Step();
        FailIf(tower.State != 12 || tower.CollisionEnabled || tower.Counter != 60 || tower.Position != position,
            "Podoboo sinking did not restore base Y and disable collision at parameter $ff.");
        for (int i = 0; i < 60; i++) Step();
        FailIf(tower.State != 13 || tower.Visible || tower.Counter != 180, "Podoboo post-sink flicker ended at the wrong boundary.");
        for (int i = 0; i < 180; i++) Step();
        FailIf(tower.State != 8 || tower.Counter != 60 || tower.AnimationIndex != 0,
            "Podoboo failed to restart its source animation after 180 underground updates.");
        for (int i = 0; i < 60; i++) Step();
        var adapter = new PodobooTowerRoomEntity(tower,
            database.EnemyHandlers.ResolveHandler(source).CombatSource(source, 1), _ => { });
        spawns.Clear();
        FailIf(adapter.ApplySeedHit(tower.CollisionBounds, position, 0x24, spawns) != SeedHitResult.Activate ||
            !tower.IsDead || spawns.OfType<EnemyDeathPuffSpawn>().Single() is not { DropsItem: false, DecrementsRoomCount: false } ||
            !adapter.TryTakeEnemyOutcome(out var outcome) || outcome.MarksRecentDefeat || !outcome.AdvancesKillCounters,
            "Podoboo mystery-seed death lost its uncounted/no-drop outcome or death-puff count transfer.");
        tower.Free();
        var ordinary = new PodobooTowerCharacter();
        ordinary.Initialize(database.ImportedEnemy(0x2d), position, new OracleRandom());
        var ordinaryAdapter = new PodobooTowerRoomEntity(ordinary,
            database.EnemyHandlers.ResolveHandler(source).CombatSource(source, 1), _ => { });
        spawns.Clear();
        for (int i = 0; i < 61; i++) ordinary.UpdateFrame(i, spawns);
        FailIf(!ordinaryAdapter.ApplySwordHit(ordinary.CollisionBounds, position, 1, EnemyKnockbackStrength.High, spawns) ||
            ordinary.KnockbackCounter != 0 || ordinary.Health != 4,
            "Podoboo ordinary sword hit did not use damage without recoil.");
        int beforeFrame = ordinary.AnimationFrame;
        for (int i = 0; i < 4; i++) ordinary.UpdateFrame(i, spawns);
        FailIf(ordinary.AnimationFrame == beforeFrame, "Living Podoboo damage froze its emergence animation.");
        ordinary.InvincibilityCounter = 0;
        FailIf(!ordinaryAdapter.ApplySwordHit(ordinary.CollisionBounds, position, 0x7f, EnemyKnockbackStrength.Low, spawns) ||
            !ordinaryAdapter.TryTakeEnemyOutcome(out var ordinaryOutcome) || !ordinaryOutcome.MarksRecentDefeat ||
            spawns.OfType<EnemyDeathPuffSpawn>().Single() is not { DropsItem: false, DecrementsRoomCount: true },
            "Podoboo ordinary death lost its counted no-drop puff or recent-defeat mark.");
        ordinary.Free();
        var puff = new EnemyDeathPuffEffect();
        puff.Initialize(position, enemyId: 0x2d);
        var dropRandom = new OracleRandom();
        var puffAdapter = new DeathPuffRoomEntity(puff, new ItemDropDatabase(), dropRandom,
            _inventory, _saveData, decrementsRoomCount: true, dropsItem: false);
        spawns.Clear();
        for (int i = 0; i < puff.DurationFrames; i++) puff.UpdateFrame(i);
        puffAdapter.OnFinished(spawns);
        FailIf(!puff.Finished || dropRandom.Calls != 0 || spawns.Count != 0 ||
            !puffAdapter.TryTakeEnemyOutcome(out var puffOutcome) || !puffOutcome.DecrementsRoomCount,
            "No-drop death puff consumed drop RNG, spawned an item, or lost its delayed room-count decrement.");
        puff.Free();
        GD.Print("Validated Podoboo Tower emergence, radii, global-frame timing, projectile RNG, full hide cycle, and mystery-seed no-drop defeat.");
    }
}
