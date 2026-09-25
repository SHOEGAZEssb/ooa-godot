using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookShroudedStalfos()
    {
        var collisions = SwitchHookCollisionDatabase.Shared;
        foreach (int mode in new[] { 0x11, 0x20, 0x36, 0x55 })
            FailIf(collisions.Effect(mode) != 0x2e, $"Source hook collision mode ${mode:x2} must select exchange $2e.");
        FailIf(!collisions.EnemyEnabled(0x22) || !collisions.EnemyEnabled(0x49),
            "Shrouded Stalfos lost the source hook eligibility bits.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count = 1, bool press = false)
        {
            input.CaptureForValidation(press ? ["attack"] : [], press ? ["attack"] : [], Vector2.Zero);
            scheduler.Advance(count / 60.0, update);
        }
        _inventory.GiveTreasure(TreasureDatabase.TreasureSwitchHook, 1);
        _inventory.EquipA(InventoryState.ItemSwitchHook);
        var random = CaptureOracleRandomForValidation();
        (EnemyCharacter Enemy, Vector2 Origin) Prepare(bool sword, int subid)
        {
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, subid == 1 ? 0x8f : sword ? 0x82 : 0x7c);
            EnemyCharacter[] candidates = sword ? _entities.Entities<SwordEnemyCharacter>().Where(e => e.Record.SubId == subid).Cast<EnemyCharacter>().ToArray()
                : _entities.Entities<ArrowMoblinCharacter>().Where(e => e.Record.SubId == subid).Cast<EnemyCharacter>().ToArray();
            foreach (var enemy in candidates)
            foreach (Vector2I direction in new[] { Vector2I.Down, Vector2I.Up, Vector2I.Left, Vector2I.Right })
            {
                Vector2 origin = enemy.Position + (Vector2)direction * 32;
                if (origin.X < 12 || origin.X >= _currentRoom.Width - 12 || origin.Y < 12 || origin.Y >= _currentRoom.Height - 12)
                    continue;
                Vector2 sideways = new(-direction.Y, direction.X);
                if (!Enumerable.Range(0, 33).All(i => Enumerable.Range(-5, 11).All(j =>
                    {
                        Vector2 point = enemy.Position + (Vector2)direction * i + sideways * j;
                        return !_currentRoom.IsSolid(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None;
                    }))) continue;
                if (_entities.Entities<EnemyCharacter>().Any(other => other != enemy &&
                    other.Position.DistanceTo((origin + enemy.Position) / 2) < 24)) continue;
                _player.WarpTo(origin);
                _player.Face(-direction);
                return (enemy, origin);
            }
            throw new InvalidOperationException($"No native open hook corridor for ${(sword ? 0x49 : 0x22):x2}:${subid:x2}.");
        }
        SwitchHookItem Hit(EnemyCharacter enemy)
        {
            var target = (ISwitchHookEnemy)enemy;
            Step(press: true);
            var hook = _entities.SwitchHook!.Item!;
            for (int i = 0; !target.SwitchHookHeld && !hook.Finished && i < 80; i++) Step();
            FailIf(!target.SwitchHookHeld || hook.State != 1 || enemy.CollisionEnabled,
                $"Shrouded Stalfos did not latch through its corridor: target={enemy.Position}, hook={hook.Position}/{hook.State}.");
            Step();
            FailIf(hook.State != 3 || hook.Substate != 0, "Pending Shrouded Stalfos hook hit lost the next-item handoff.");
            return hook;
        }
        foreach (bool sword in new[] { false, true })
        foreach (int subid in new[] { 0, 1 })
        {
            var fixture = Prepare(sword, subid);
            var item = Hit(fixture.Enemy);
            int counter = sword ? ((SwordEnemyCharacter)fixture.Enemy).Counter1 : ((ArrowMoblinCharacter)fixture.Enemy).Counter;
            int health = fixture.Enemy.Health, count = _entities.RoomEnemyCount;
            Vector2 caught = fixture.Enemy.Position;
            Step(18 + 16);
            FailIf(item.Substate != 2 || fixture.Enemy.Position != caught || fixture.Enemy.CollisionEnabled ||
                (sword ? ((SwordEnemyCharacter)fixture.Enemy).Counter1 : ((ArrowMoblinCharacter)fixture.Enemy).Counter) != counter,
                "Held Shrouded Stalfos changed XY or countdown before its swap.");
            Step();
            FailIf(_player.PrecisePosition != caught.Floor() || fixture.Enemy.Position != fixture.Origin.Floor() + caught - caught.Floor(),
                "Shrouded Stalfos swap lost source high-byte-only target coordinates.");
            Step(16);
            FailIf(!item.Finished || !fixture.Enemy.CollisionEnabled || fixture.Enemy.Health != health ||
                _entities.RoomEnemyCount != count, "Shrouded Stalfos release changed health/count or failed to restore body collision.");
            if (fixture.Enemy is SwordEnemyCharacter swordsman)
            {
                FailIf(swordsman.State != SwordEnemyState.PreparingChase || swordsman.Counter1 != 16,
                    "Sword Shrouded Stalfos must release into state9 with counter$10.");
                Step(15);
                FailIf(swordsman.State != SwordEnemyState.PreparingChase || swordsman.Counter1 != 1,
                    "Post-hook sword chase preparation ended before update16.");
                Step();
                FailIf(swordsman.State != SwordEnemyState.Chasing || swordsman.Counter1 != 96,
                    "Sword state9 must enter its96-update chase on preparation update16.");
            }
            else
            {
                var archer = (ArrowMoblinCharacter)fixture.Enemy;
                FailIf(archer.State != ArrowMoblinState.Moving || archer.Counter != counter,
                    "Archer hook release must resume state8 with its existing counter, without an RNG reset.");
                Step();
            }
            for (int i = 0; (_player.IsUsingSwitchHook || _player.KnockbackFrames > 0) && i < 40; i++) Step();
            var previous = item;
            Step(press: true);
            item = _entities.SwitchHook!.Item!;
            FailIf(item == previous || !_player.StartedItemAnimationThisUpdate, "Shrouded Stalfos exchange prevented another hook use.");
            _entities.SwitchHook.Cancel();
        }

        (Vector2 Link, Vector2 Enemy, int Z, int Substate) Run(bool batch, bool sword)
        {
            var fixture = Prepare(sword, 0);
            var item = Hit(fixture.Enemy);
            if (batch) Step(35); else for (int i = 0; i < 35; i++) Step();
            return (_player.PrecisePosition, fixture.Enemy.Position, item.ZHigh, item.Substate);
        }
        foreach (bool sword in new[] { false, true })
            FailIf(Run(false, sword) != Run(true, sword), "Batched gameplay changed the Shrouded Stalfos exchange.");

        // Shared state dispatch probes: scent cannot replace held state3, and
        // cancellation must preserve the counter/RNG while gravity runs.
        var database = new EnemyDatabase();
        LoadValidationRoom(4, 0x7c);
        Vector2 center = new(184, 88);
        var rng = new OracleRandom();
        var archerProbe = new ArrowMoblinCharacter();
        archerProbe.Initialize(database.ImportedEnemy(0x22), _currentRoom, center, rng);
        archerProbe.UpdateFrame(center + Vector2.Left * 32);
        int duration = archerProbe.Counter, calls = rng.Calls;
        archerProbe.BeginSwitchHook(center + Vector2.Left * 32);
        archerProbe.UpdateFrame(center, center + Vector2.Right * 20);
        FailIf(!archerProbe.SwitchHookHeld || archerProbe.SwitchHookSubstate != 1 || archerProbe.Counter != duration,
            "Scent Seed overwrote the archer's held state.");
        archerProbe.CopySwitchHookPosition(center, -5);
        archerProbe.ReleaseSwitchHook();
        for (int i = 0; i < 9; i++) archerProbe.UpdateFrame(center, center + Vector2.Right * 20);
        FailIf(archerProbe.State != ArrowMoblinState.SwitchHook || archerProbe.ZFixed != -128,
            "Archer release lost gravity's old-speed-first integration.");
        archerProbe.UpdateFrame(center, center + Vector2.Right * 20);
        FailIf(archerProbe.State != ArrowMoblinState.Moving || archerProbe.Counter != duration || rng.Calls != calls,
            "Archer cancellation landing must preserve its counter and consume no RNG.");
        archerProbe.UpdateFrame(center, center + Vector2.Right * 20);
        FailIf(archerProbe.State != ArrowMoblinState.FollowingScentSeed, "Released archer did not resume scent response on the following update.");
        archerProbe.Free();

        LoadValidationRoom(4, 0x82);
        var swordProbe = new SwordEnemyCharacter();
        swordProbe.Initialize(database.ImportedEnemy(0x49), _currentRoom, new(120, 104), new OracleRandom());
        swordProbe.UpdateFrame(Vector2.Zero);
        // Use the original facing sector, which is blocked by the separate blade.
        Vector2 link = swordProbe.Position + (Vector2)(swordProbe.AnimationIndex switch {
            0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, _ => Vector2I.Left }) * 32;
        swordProbe.BeginSwitchHook(link);
        swordProbe.UpdateFrame(link, link);
        var part = new EnemySwordRoomEntity(swordProbe, _ => { }, () => true);
        var spawns = new List<RoomEntitySpawn>();
        part.UpdateFrame(default, spawns);
        part.UpdateFrame(default, spawns);
        FailIf(!swordProbe.SwitchHookHeld || !swordProbe.SwordBlocking || swordProbe.CollisionEnabled || !part.CollisionEnabled,
            "A held sword enemy must preserve the blade's independent health/var30 collision gate.");
        swordProbe.CopySwitchHookPosition(swordProbe.Position, -5);
        part.UpdateFrame(default, spawns);
        FailIf(part.Node.Position != swordProbe.EnemySwordPosition || !part.CollisionEnabled,
            "Enemy sword must follow high XY without inheriting its held parent's Z.");
        swordProbe.ReleaseSwitchHook();
        for (int i = 0; i < 10; i++)
        {
            swordProbe.UpdateFrame(link);
            FailIf(swordProbe.Counter1 != 16, "Sword release must rewrite counter$10 on every falling dispatch.");
        }
        FailIf(swordProbe.State != SwordEnemyState.PreparingChase || swordProbe.ZFixed != 0,
            "Sword cancellation failed to land in chase preparation state9.");
        part.Node.Free();
        swordProbe.Free();
    }
}
