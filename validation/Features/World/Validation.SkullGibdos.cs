using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonGibdos()
    {
        var database = new EnemyDatabase();
        var definition = database.ImportedEnemy(0x12);
        FailIf(string.Concat(EnemyBehaviorTables.Shared.GibdoActiveCollisions.Select(v => v.Value)) != "11111111111101110000011111111110",
            "Gibdo active collision mask lost source item ordering or eligibility.");
        FailIf(definition is not { Health: 8, DamageQuarters: 4, RadiusX: 6, RadiusY: 6, Palette: 2, TileBase: 0 } ||
            definition.Animations.Length != 1 || definition.Animations[0] != "16@8,0,0,0;8,8,2,0|16,1@8,0,2,32;8,8,0,32",
            "ENEMY_GIBDO lost extraEnemyData $17 or its 16-update animation/parameter stream.");
        FailIf(!EnemyBehaviorTables.Shared.Gibdo.Select(v => v.Value).SequenceEqual(new[] { 20, 24, 127, 64, 30, 49, 2 }) ||
            !EnemyBehaviorTables.Shared.GibdoCollisionEffects.Select(v => v.Value).SequenceEqual(new[] {
                2,6,6,6,11,11,11,11,11,11,11,8,0,46,11,37,0,0,0,0,0,47,11,34,11,11,32,39,11,40,41,0 }),
            "Gibdo source walk/conversion operands or collision row $16 changed.");
        foreach (var (room, expected) in new[] {
            (0x6e, new Vector2[] { new(120,56), new(184,40) }),
            (0x81, new Vector2[] { new(104,72), new(178,72) }) })
        {
            _saveData.SetRoomFlag(4, room, 0xff, false);
            LoadValidationRoom(4, room);
            FailIf(!_entities.Entities<GibdoCharacter>().Select(g => g.Position).SequenceEqual(expected),
                $"Room 4:{room:x2} lost the exact ordered Gibdo coordinates.");
        }
        LoadValidationRoom(4, 0x6e);
        var random = new OracleRandom();
        var probe = new OracleRandom();
        var walker = new GibdoCharacter();
        Vector2 origin = new(120,56);
        walker.Initialize(definition, _currentRoom, origin, random);
        walker.UpdateFrame();
        probe.Next();
        FailIf(walker.State != 8 || random.Calls != 1 || walker.Position != origin,
            "Gibdo state0 must consume common RNG before any movement decision.");
        var roll = probe.Next();
        walker.UpdateFrame();
        FailIf(walker.State != 9 || walker.Angle != (roll.High & 24) || walker.Counter != (roll.Low & 127) + 64 || random.Calls != 2,
            "Gibdo state8 lost its high-byte cardinal angle/low-byte duration selection.");
        var counter = typeof(GibdoCharacter).GetField("<Counter>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        counter.SetValue(walker, 1);
        walker.UpdateFrame();
        FailIf(walker.State != 8 || walker.Position != origin,
            "Gibdo must skip movement on the walk counter's zero update.");
        FailIf(!walker.BeginPegasusHit(), "Gibdo rejected a Pegasus stun.");
        for (int i = 0; i < 479; i++) walker.UpdateFrame(i);
        FailIf(walker.StunCounter != 1 || walker.Health != 8,
            "Gibdo Pegasus stun must consume 240 odd-frame ticks without damage.");
        walker.UpdateFrame(479);
        FailIf(walker.StunCounter != 0 || walker.State != 8, "Gibdo resumed before the zero stun dispatch returned.");
        walker.Free();

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Step(int n = 1) =>
            StepGameplayUpdates(n, Vector2.Zero, [], [], batched: true);
        var shooter = SeedShooterRecord.Load();
        var ember = new SeedSatchelDatabase().Ember;
        var snapshot = CaptureOracleRandomForValidation();
        (Vector2 Position, int State, int Health, int RoomCount, int Calls) Run(bool batch, bool lethal = false)
        {
            RestoreOracleRandomForValidation(snapshot);
            LoadValidationRoom(4, 0x6e);
            var target = _entities.Entities<GibdoCharacter>()[0];
            _player.WarpTo(target.Position + Vector2.Down * 30);
            FailIf(_currentRoom.IsSolid(_player.Position), "Gibdo attack fixture must stand in the real open room.");
            Step();
            target.Position += new Vector2(0.25f, 0.25f);
            if (lethal) target.Health = 1;
            int roomCount = _entities.RoomEnemyCount;
            // Spawn the actual shooter child in the open approach corridor;
            // its post-object collision allocates the part for the next update.
            Vector2 muzzle = target.Position + Vector2.Down * 12;
            _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(muzzle - shooter.Offsets[0], Vector2I.Up, ember, 4, SeedLaunchKind.Shooter));
            for (int i = 0; i < 10 && target.State != 10; i++) Step();
            FailIf(target.State != 10 || target.Counter != 30 || target.Health != 1 ||
                _entities.Entities<BurningEnemyPart>().Count != 1 || _entities.Entities<BurningEnemyPart>()[0].Counter != 58 ||
                _entities.RoomEnemyCount != roomCount || lethal && target.CollisionEnabled,
                "Ember collision must start Gibdo state $0a, then flame state0 must hold health at one and decrement59 to58 in the same part pass.");
            Vector2 replacementPosition = new(Mathf.Floor(target.Position.X), Mathf.Floor(target.Position.Y));
            // PART_BURNING_ENEMY ignores its own DEAD status; a Moldorm-style
            // raw write must preserve the flame/target restoration lifetime.
            _entities.EntityAdapters<BurningEnemyRoomEntity>().Single().ClearHealthAndCollision();
            if (batch) Step(29); else for (int i = 0; i < 29; i++) Step();
            FailIf(target.Counter != 1 || _entities.Entities<StalfosCharacter>().Count != 0,
                "Gibdo converted before the full 30-update state $0a countdown.");
            FailIf(_entities.Entities<BurningEnemyPart>()[0].AnimationFrame != 5,
                "Burning-enemy animation lost its three introductory frames or loop at partAnimation5b965.");
            Step();
            var result = _entities.Entities<StalfosCharacter>().Single();
            FailIf(result.State != StalfosState.Uninitialized || result.Health != 4 || result.Position != replacementPosition ||
                _entities.Entities<BurningEnemyPart>().Count != 0 || _entities.Entities<EnemyDeathPuffEffect>().Count != 0 ||
                _entities.RoomEnemyCount != roomCount,
                "enemyReplaceWithID must retain the enemy count and high position, clear old health, defer new state0, and delete the old-ID flame without death rewards.");
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_enemySlots", flags)!.GetValue(_entities)!;
            FailIf(slots.Single(p => p.Key.Node == result).Value != 0,
                "Gibdo conversion changed the enemy slot/order.");
            int calls = _entities.RandomCalls;
            Step();
            FailIf(result.State != StalfosState.Deciding || _entities.RandomCalls < calls + 1,
                "Replacement Stalfos did not execute fresh common initialization on the next enemy pass.");
            return (result.Position, (int)result.State, result.Health, _entities.RoomEnemyCount, _entities.RandomCalls);
        }
        var singles = Run(false);
        FailIf(singles != Run(true), "Gibdo conversion differs between individual updates and a batched host frame.");
        Run(true, lethal: true);

        foreach (bool lethal in new[] { false, true })
        {
            LoadValidationRoom(4, 0x6e);
            var fullTarget = _entities.Entities<GibdoCharacter>()[0];
            _player.WarpTo(fullTarget.Position + Vector2.Down * 30);
            Step();
            var add = typeof(RoomEntityManager).GetMethod("AddEntity", flags)!;
            for (int i = 0; i < 16; i++)
            {
                var occupied = new StalfosBoneProjectile();
                occupied.Initialize(StalfosBoneRecord.Load(), _currentRoom, new Vector2(120, 40), p => p);
                add.Invoke(_entities, [new StalfosBoneRoomEntity(occupied)]);
            }
            var active = (List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities", flags)!.GetValue(_entities)!;
            var targetAdapter = (GibdoRoomEntity)active.Single(e => e.Node == fullTarget);
            var pending = new List<RoomEntitySpawn>();
            if (lethal) fullTarget.Health = 1;
            FailIf(targetAdapter.ApplySeedHit(fullTarget.CollisionBounds, fullTarget.Position, 0x20, pending) != SeedHitResult.Activate || pending.Count != 0,
                "Full shared part pool must reject the flame without rejecting the Ember collision.");
            Step();
            FailIf(fullTarget.Health != (lethal ? 0 : 6) || fullTarget.Counter != 30, "Failed flame allocation incorrectly held Gibdo health at one.");
            if (lethal)
            {
                Step();
                FailIf(_entities.Entities<GibdoCharacter>().Count != 1 || _entities.Entities<EnemyDeathPuffEffect>().Count != 0,
                    "A lethal Ember hit must reach enemyDie, whose death-puff allocation also fails while the native PART pool remains full.");
                Step(90);
                FailIf(_entities.RoomEnemyCount != 2 || _entities.Entities<StalfosCharacter>().Count != 0,
                    "Failed death-puff allocation must leave the defeated Gibdo's room count unreleased, without converting to Stalfos.");
            }
            else
            {
                Step(30);
                FailIf(_entities.Entities<StalfosCharacter>().Count != 1, "Nonlethal Gibdo Ember conversion incorrectly depends on a free flame slot.");
            }
        }
        LoadValidationRoom(4, 0x91);
        FailIf(_entities.Entities<BurningEnemyPart>().Count != 0, "Leaving the room retained a burning-enemy part.");
        _entities.ClearRecentEnemyDefeats();
        LoadValidationRoom(4, 0x6e);
        FailIf(_entities.Entities<GibdoCharacter>().Count != 2 || _entities.Entities<StalfosCharacter>().Count != 0,
            "Room re-entry retained the transient Gibdo replacement as a permanent defeat.");
        GD.Print("Validated Skull Dungeon Gibdos: source placements, movement RNG/counter, Pegasus cadence, actual Ember collision, lethal/nonlethal conversion, slot/order/count retention, flame identity teardown, batched updates and re-entry.");
    }
}
