using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownWhispSeeds()
    {
        var seeds = new SeedSatchelDatabase();
        var shooter = SeedShooterRecord.Load();
        foreach (bool batched in new[] { false, true })
        foreach (int item in Enumerable.Range(0x20, 5))
        {
            LoadValidationRoom(4, 0x9f);
            _player.WarpTo(new(24, 24));
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step();
            var actor = _entities.Entities<WhispCharacter>().First();
            var adapter = _entities.EntityAdapters<WhispRoomEntity>().First();
            for (int row = 3; row <= 7; row++)
            for (int column = 5; column <= 9; column++)
                _currentRoom.SetPositionTileAndCollision(new(column * 16 + 8, row * 16 + 8), 0xa0, 0, 0);
            actor.Position = new(120, 88);
            int health = actor.Health;
            FailIf(!seeds.TryGet(item, out var seed), $"Missing seed${item:x2}.");
            var projectile = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                actor.Position + Vector2.Down * 8 - shooter.Offsets[0], Vector2I.Up,
                seed, 4, SeedLaunchKind.Shooter));
            Step(); // Initialize the seed and resolve Mystery's randomized type.
            bool gale = projectile.CollisionType == 0x1e;
            for (int i = 0; i < 12 && projectile.CollisionEnabled; i++) Step();
            FailIf(projectile.CollisionEnabled || adapter.IsSeedBurning || actor.Health != health || adapter.GaleCaught != gale,
                $"Whisp seed${item:x2} must consume the projectile and enter only the selected Gale response.");
            if (gale)
            {
                var motion = (GaleSeedEnemyMotion)typeof(CombatEnemyRoomEntityAdapter<WhispCharacter>)
                    .GetField("_gale", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
                FailIf(motion.Counter != 30 || actor.CollisionEnabled,
                    "Whisp Gale contact must start counter$1e and disable enemy collisions.");
                Step();
                FailIf(motion.Counter != 29, "Whisp JUST_HIT must fall through to state5 without an extra skipped update.");
                Step(2);
                FailIf(motion.Counter != 27, "Whisp Gale counter must match batched and individual updates.");
                Step(100);
                FailIf(_entities.Entities<WhispCharacter>().Contains(actor), "Whisp Gale motion must finish with enemy deletion.");
            }
            else
            {
                Vector2 before = actor.Position;
                Step(2);
                FailIf(actor.Position == before || !actor.CollisionEnabled || actor.Health != health,
                    "Ineffective Whisp seeds must not stop movement or change health/collision.");
            }
        }

        // The raw Mystery column differs from the randomized projectile's
        // $1b..$1e columns. Verify its source-defined status in isolation.
        LoadValidationRoom(4, 0x9f);
        StepGameplayUpdates(1, Vector2.Zero);
        var rawActor = _entities.Entities<WhispCharacter>().First();
        var rawAdapter = _entities.EntityAdapters<WhispRoomEntity>().First();
        seeds.TryGet(ItemId.MysterySeed, out var mystery);
        var result = rawAdapter.ApplySeedCollision(rawActor.CollisionBounds, rawActor.Position, mystery, ItemCollisionType.MysterySeed, []);
        Vector2 rawBefore = rawActor.Position;
        StepGameplayUpdates(2, Vector2.Zero);
        FailIf(!result.Contact || result.DisableCollision || rawActor.Health != 0 || rawActor.CollisionEnabled ||
            rawActor.IsDead || !rawActor.Visible || rawActor.Position == rawBefore,
            "Whisp raw Mystery effect$35 must clear health/collision without deleting or freezing its non-boomerang state.");
        LoadValidationRoom(0, 0x60);
    }
}
