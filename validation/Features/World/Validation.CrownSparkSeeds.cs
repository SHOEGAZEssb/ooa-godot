using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownSparkSeeds()
    {
        var seeds = new SeedSatchelDatabase();
        var shooter = SeedShooterRecord.Load();
        // objectCollisionTable mode$17: all seed collision columns$1a..$1e
        // are effect$20, independently of the seed's original item ID.
        foreach (int collision in Enumerable.Range(0x1a, 5))
            FailIf(EnemyBehaviorTables.Shared.SparkCollisionEffects[collision].Value != 0x20 ||
                EnemyBehaviorTables.Shared.SparkActiveCollisions[collision].Value != 1,
                $"Spark seed column${collision:x2} must enable collisionEffect20.");
        foreach (bool batched in new[] { false, true })
        foreach (int item in Enumerable.Range(0x20, 5))
        {
            LoadValidationRoom(4, 0xa8);
            _player.WarpTo(new(24, 24));
            var spark = _entities.Entities<SparkCharacter>().First();
            for (int row = 3; row <= 7; row++)
            for (int column = 5; column <= 9; column++)
                _currentRoom.SetPositionTileAndCollision(new(column * 16 + 8, row * 16 + 8), 0xa0, 0, 0);
            // A straight north wall gives the native clockwise follower an
            // independently known rightward path throughout both shots.
            for (int column = 5; column <= 9; column++)
                _currentRoom.SetPositionTileAndCollision(new(column * 16 + 8, 72), 0x2e, 0x0f, 0);
            spark.Initialize(spark.Record, _currentRoom, new(120, 88));
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step(1);
            var adapter = _entities.EntityAdapters<SparkRoomEntity>().First();
            int health = spark.Health;
            FailIf(!seeds.TryGet(item, out var seed), $"Missing seed${item:x2}.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                var projectile = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                    spark.Position + Vector2.Down * 8 - shooter.Offsets[0], Vector2I.Up,
                    seed, 4, SeedLaunchKind.Shooter));
                Vector2 before = spark.Position;
                Step(4);
                FailIf(projectile.CollisionEnabled || adapter.IsSeedBurning || adapter.GaleCaught ||
                    spark.Health != health || !spark.CollisionEnabled || spark.Position != before + Vector2.Right * 4,
                    $"Spark must consume seed${item:x2} without burn, Gale, damage or interrupted movement (repeat{repeat}, " +
                    $"seed={projectile.State}/{projectile.CollisionEnabled}, burn={adapter.IsSeedBurning}, gale={adapter.GaleCaught}, " +
                    $"health={spark.Health}/{health}, collision={spark.CollisionEnabled}, position={before}->{spark.Position}, angle={spark.Angle}).");
            }
        }
        LoadValidationRoom(0, 0x60);
    }
}
