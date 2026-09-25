using Godot;
using System;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogLive()
    {
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            void Reset()
            {
                LoadValidationRoom(0, 0x60); _entities.Clear(); _player.WarpTo(new(8,8));
                for (int y = 8; y < 128; y += 16)
                    for (int x = 8; x < 160; x += 16) _currentRoom.SetPositionTileAndCollision(new(x,y), 0x0c, 0, 0);
            }
            Reset();
            var cloud = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72,72),2,3));
            _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(120,104),2));
            cloud.InvincibilityCounter = -3;
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                Step();
                FailIf(cloud.State != 8 || cloud.InvincibilityCounter != -3 || cloud.ProjectileCounter is not (50 or 60 or 80),
                    "Frozen Smog state0 must initialize with phase3's source timer and retain its signed invincibility.");
                Vector2 position = cloud.Position;
                int timer = cloud.ProjectileCounter;
                Step(3);
                FailIf(cloud.Position != position || cloud.ProjectileCounter != timer || cloud.InvincibilityCounter != -3,
                    "Initialized Smog must hold movement, timer and invincibility during text.");
            }
            finally { _entities.TextActiveSource = text; }
            int initialTimer = cloud.ProjectileCounter;
            Step(initialTimer + 44);
            FailIf(cloud.AnimationIndex != 1 || _entities.EntityAdapters<SmogProjectileRoomEntity>().Any(),
                "Smog must wait for the 30+15 update shooting animation after its timer reaches zero.");
            Step();
            var projectile = (SmogProjectilePart)_entities.EntityAdapters<SmogProjectileRoomEntity>().Single().Node;
            FailIf(projectile.State != 1 || cloud.AnimationIndex != 0 || _entities.RoomEnemyCount != 2,
                "Cloud emission must allocate PART$4a, initialize it in the same gameplay update and preserve enemy count.");

            Reset();
            cloud = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72,72),3));
            var other = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(120,104),3));
            Step(4);
            FailIf(cloud.State != 0 || cloud.SubId != 3, "Merged Smog must remain uninitialized for four updates.");
            Step();
            FailIf(cloud.State != 8 || cloud.SubId != 4 || other.SubId != 4 || _entities.RoomEnemyCount != 2,
                "Merged Smog must select its large form on initialization update5 with native count exactly2.");
            other.DisableCollision();
            var adapter = _entities.EntityAdapters<SmogRoomEntity>().Single(entity => entity.Node == cloud);
            adapter.SetLinkSwordState(SwordActionState.Swing,1);
            FailIf(!adapter.ApplySwordHit(cloud.CollisionBounds,Vector2.Zero,6,default,[]), "Large Smog must accept lethal sword damage.");
            Step();
            FailIf(cloud.IsDead || cloud.Counter1 != 19, "Pending lethal contact must execute the normal handler before boss death begins.");
            Step();
            FailIf(cloud.Counter1 != 119 || cloud.IsDead, "First eligible death update must start the 120-update timer.");
            Step(118);
            FailIf(cloud.Counter1 != 1 || cloud.IsDead || _entities.EntityAdapters<BossDeathExplosionRoomEntity>().Any(),
                "Smog must retain its enemy count and wait through death update119.");
            Step();
            var explosion = (BossDeathExplosionEffect)_entities.EntityAdapters<BossDeathExplosionRoomEntity>().Single().Node;
            FailIf(!cloud.IsDead || _entities.RoomEnemyCount != 2, "Death update120 must transfer the retained count to PART$04.");
            Step(explosion.AnimationDuration + 1);
            FailIf(_entities.RoomEnemyCount != 1 || _entities.EntityAdapters<BossDeathExplosionRoomEntity>().Any(),
                "Completed PART$04 must release exactly one Smog count while the other cloud survives.");
        }
        _entities.Clear();
        GD.Print("Validated Smog frozen initialization, animation-driven projectile allocation, merged initialization and boss death handoff with single and batched gameplay updates.");
    }
}
