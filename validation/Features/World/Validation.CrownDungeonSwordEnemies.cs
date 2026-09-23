using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownDungeonSwordEnemies()
    {
        var database = new EnemyDatabase();
        foreach (int id in new[] { 0x3d, 0x48 })
        foreach (int subid in new[] { 0, 1 })
        {
            var record = database.ImportedEnemy(id, subid);
            int health = id == 0x3d ? (subid == 0 ? 3 : 5) : (subid == 0 ? 6 : 9);
            int damage = id == 0x3d ? 3 : (subid == 0 ? 2 : 4);
            FailIf(record.Health != health || record.DamageQuarters != damage ||
                record.Palette != (subid == 0 ? 2 : 1) || record.RadiusX != 6 || record.RadiusY != 6 ||
                record.TileBase != 0 || record.Animations.Length != 4,
                $"Sword enemy ${id:x2}:${subid:x2} lost enemyData.s subid/extra-data properties.");

            var enemy = new SwordEnemyCharacter();
            enemy.Initialize(record, Room060MovementFixture(), new Vector2(64, 64), new OracleRandom());
            enemy.UpdateFrame(enemy.Position + Vector2.Right * 32);
            int cooldown = subid == 0 ? 20 : 16;
            for (int i = 0; i < cooldown - 1; i++) enemy.UpdateFrame(enemy.Position + Vector2.Right * 32);
            FailIf(enemy.State != SwordEnemyState.Wandering || enemy.Counter2 != 1,
                $"Sword enemy ${id:x2}:${subid:x2} chased before its source cooldown expired.");
            enemy.UpdateFrame(enemy.Position + Vector2.Right * 32);
            FailIf(enemy.State != SwordEnemyState.PreparingChase || enemy.Counter1 != 16 || enemy.Angle != 8,
                $"Sword enemy ${id:x2}:${subid:x2} lost the cooldown-zero chase boundary.");
            for (int i = 0; i < 15; i++) enemy.UpdateFrame(enemy.Position + Vector2.Right * 32);
            FailIf(enemy.State != SwordEnemyState.PreparingChase || enemy.Counter1 != 1,
                "Sword chase preparation must retain its full $10-update duration.");
            enemy.UpdateFrame(enemy.Position + Vector2.Right * 32);
            var before = enemy.Position;
            enemy.UpdateFrame(enemy.Position + Vector2.Left * 32);
            FailIf(enemy.State != SwordEnemyState.Chasing || enemy.Counter1 != 95 ||
                enemy.Position != before + Vector2.Right * (id == 0x48 ? 0.75f : 0.625f),
                $"Sword enemy ${id:x2}:${subid:x2} lost source SPEED_c0/SPEED_a0 chase movement.");
            enemy.UpdateFrame(enemy.Position + Vector2.Left * 32);
            FailIf(enemy.Angle != (id == 0x48 ? 9 : 8),
                "Darknuts must turn every second chase update; Sword Moblins turn every fourth.");
            enemy.UpdateFrame(enemy.Position + Vector2.Down * 32, enemy.Position + Vector2.Down * 32);
            FailIf((enemy.State == SwordEnemyState.FollowingScentSeed) != (id == 0x3d),
                "Only Sword Moblins, not Sword Darknuts, follow Scent Seeds.");
            enemy.Free();
        }

        // Real ordered room streams, without a dungeon progression fixture.
        LoadValidationRoom(4, 0xb0);
        var moblins = _entities.Entities<SwordEnemyCharacter>();
        FailIf(moblins.Count != 2 || !moblins.Select(e => (e.Record.Id, e.Record.SubId, e.Position)).SequenceEqual(new[] {
            (0x3d, 0, new Vector2(0x58, 0x28)), (0x3d, 1, new Vector2(0x98, 0x58)) }),
            "Crown Dungeon $4:$b0 must retain both Sword Moblin variants in source order.");
        LoadValidationRoom(4, 0x9c);
        var mixed = _entities.Entities<SwordEnemyCharacter>();
        FailIf(mixed.Count != 2 || !mixed.Select(e => (e.Record.Id, e.Record.SubId, e.Position)).SequenceEqual(new[] {
            (0x3d, 1, new Vector2(0x38, 0x68)), (0x48, 0, new Vector2(0xd8, 0x68)) }),
            "Crown Dungeon $4:$9c lost its Sword Moblin/Darknut placements.");
        GD.Print("Validated Crown Dungeon Sword Moblin/Darknut variants, source properties, ordered placements, chase boundaries/speeds/cadence and Scent eligibility.");
    }

    private void ValidateCrownDungeonSwordSeedCollisions()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        void Step(int count = 1)
        {
            input.CaptureForValidation([], [], Vector2.Zero);
            scheduler.Advance(count / 60.0, update);
        }
        var seeds = new SeedSatchelDatabase();
        var shooter = SeedShooterRecord.Load();
        EmberSeedEffect Shoot(SwordEnemyCharacter target, int seedItem)
        {
            FailIf(!seeds.TryGet(seedItem, out var seed), $"Missing seed ${seedItem:x2}.");
            Vector2 muzzle = target.Position + Vector2.Down * 12;
            return _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(muzzle - shooter.Offsets[0], Vector2I.Up, seed, 4, SeedLaunchKind.Shooter));
        }
        foreach (bool batch in new[] { false, true })
        {
            LoadValidationRoom(4, 0xb7);
            var target = _entities.Entities<SwordEnemyCharacter>().Single();
            _player.WarpTo(target.Position + Vector2.Down * 24, recordSafe: false);
            FailIf(_currentRoom.IsSolid(_player.Position), "Sword Moblin seed fixture must use $4:$b7's open floor.");
            Step();
            Shoot(target, 0x22);
            for (int i = 0; i < 12 && target.StunCounter == 0; i++) Step();
            FailIf(target.StunCounter != 240 || target.Health != 3 || target.InvincibilityCounter != -16,
                "Pegasus Seed must apply ENEMYDMG_38 ($f0 stun, $f0 invincibility) without damaging the Moblin.");
            Step();
            var blade = _entities.EntityAdapters<EnemySwordRoomEntity>().Single();
            FailIf(target.SwordBlocking || blade.CollisionEnabled,
                "A stunned Sword Moblin must publish the unguarded body and disable its blade in the later part pass.");
            var state = target.State;
            int counter = target.Counter1;
            int remaining = 2 * target.StunCounter - ((_entities.FrameCounter & 1) == 0 ? 1 : 0);
            if (batch) Step(remaining); else for (int i = 0; i < remaining; i++) Step();
            FailIf(target.StunCounter != 0 || target.State != state || target.Counter1 != counter,
                "Sword Moblin Pegasus stun must consume exactly 240 odd-frame ticks before AI resumes.");

            LoadValidationRoom(4, 0xb7);
            target = _entities.Entities<SwordEnemyCharacter>().Single();
            _player.WarpTo(target.Position + Vector2.Down * 24, recordSafe: false);
            Step();
            int count = _entities.RoomEnemyCount;
            Shoot(target, 0x20);
            for (int i = 0; i < 12 && target.Health != 0; i++) Step();
            FailIf(target.Health != 0 || target.IsDead || target.CollisionEnabled,
                "Ember must clear Sword Moblin health/collision before the delayed flame handoff.");
            Step();
            var flame = _entities.Entities<BurningEnemyPart>().Single();
            FailIf(target.Health != 1 || flame.Counter != 58 || _entities.RoomEnemyCount != count,
                "The flame must borrow one health in its first part pass without releasing the room enemy count.");
            if (batch) Step(58); else for (int i = 0; i < 58; i++) Step();
            FailIf(target.Health != 0 || target.IsDead || _entities.RoomEnemyCount != count,
                "Burn restoration must leave death for the next enemy pass.");
            Step();
            FailIf(_entities.Entities<SwordEnemyCharacter>().Count != 0 ||
                _entities.EntityAdapters<EnemySwordRoomEntity>().Any() || _entities.RoomEnemyCount != count,
                "Burned Moblin and its blade must retire before the death puff releases the room count.");
            Step(32);
            FailIf(_entities.RoomEnemyCount != count - 1,
                "Burned Moblin death puff did not release exactly one room enemy count.");
        }
        foreach (int seedItem in new[] { 0x20, 0x23 })
        {
            LoadValidationRoom(4, 0xbe);
            var darknut = _entities.Entities<SwordEnemyCharacter>().Single();
            _player.WarpTo(darknut.Position + Vector2.Down * 24, recordSafe: false);
            FailIf(_currentRoom.IsSolid(_player.Position), "Darknut seed fixture must use $4:$be's open floor.");
            Step();
            var seed = Shoot(darknut, seedItem);
            for (int i = 0; i < 12 && seed.CollisionEnabled; i++) Step();
            FailIf(seed.CollisionEnabled || darknut.Health != 9 || darknut.StunCounter != 0 || darknut.GaleCollisionDisabled ||
                _entities.Entities<BurningEnemyPart>().Any(),
                $"Sword Darknut $48:$01 must consume seed ${seedItem:x2} through effect $20 without burning or entering Gale state $05.");
        }
        GD.Print("Validated actual Crown Dungeon sword-enemy seed collisions, Darknut fire/Gale immunity, stun/guard/part ordering, burning death/count handoff and batched updates.");
    }
}
