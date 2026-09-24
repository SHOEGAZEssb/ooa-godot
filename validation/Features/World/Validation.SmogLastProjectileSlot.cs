using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSmogLastProjectileSlot()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var setAnimation = typeof(EnemyCharacter).GetMethod("SetAnimation", flags)!;
        foreach (bool batched in new[] { false, true })
        foreach (bool large in new[] { false, true })
        foreach (bool full in new[] { false, true })
        {
            LoadValidationRoom(0, 0x60);
            _entities.Clear();
            _player.WarpTo(new(24, 24));
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0x0c, 0, 0);
            var cloud = _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72, 72), large ? 3 : 2));
            _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(120, 104), 3));
            StepGameplayUpdates(large ? 5 : 1, Vector2.Zero, batched: batched);
            setAnimation.Invoke(cloud, [large ? 5 : 1]);
            // Both source firing animations reach their shot parameter after
            // 30+15 updates. Fill the pool just before that boundary.
            StepGameplayUpdates(44, Vector2.Zero, batched: batched);
            FailIf(_entities.Entities<SmogProjectilePart>().Count != 0,
                "Smog must not emit before the firing animation reaches update45.");
            var fillers = Enumerable.Range(0, full ? 16 : 15).Select(_ =>
                _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(40, 104), 1))).ToArray();
            FailIf(_entities.PartSlotAvailable == full, "Native PART availability must match the occupied slots.");
            Vector2 shotPosition = cloud.Position + (large ? new Vector2(0, 8) : Vector2.Zero);
            _runtimeState.SetWramByte(0xc0c2, 0xff);
            _runtimeState.SetWramByte(0xc0cb, 0x66);
            _runtimeState.SetWramByte(0xc0cd, 0x66);
            _runtimeState.SetWramByte(0xc0cf, 0x66);
            StepGameplayUpdates(1, Vector2.Zero);
            var projectiles = _entities.Entities<SmogProjectilePart>();
            if (full)
            {
                FailIf(projectiles.Count != 16 || projectiles.Except(fillers).Any() ||
                    _runtimeState.ReadWramByte(0xc0c2) != (large ? 0 : 0xff) ||
                    _runtimeState.ReadWramByte(0xc0cb) != (byte)(int)shotPosition.Y ||
                    _runtimeState.ReadWramByte(0xc0cd) != (byte)(int)shotPosition.X ||
                    _runtimeState.ReadWramByte(0xc0cf) != 0 || _entities.RoomEnemyCount != 2,
                    "Full PART pool must preserve all16 slots and enemy count while applying Smog's unchecked echo-RAM writes.");
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(_entities.Entities<SmogProjectilePart>().Count != 16 ||
                    _runtimeState.ReadWramByte(0xc0c2) != (large ? 0 : 0xff),
                    "Full-pool projectile failure must not queue a retry or duplicate the subid increment.");
                continue;
            }
            var shot = projectiles.Except(fillers).Single();
            FailIf(projectiles.Count != 16 || _entities.PartSlotAvailable || shot.State != 1 ||
                shot.SubId != (large ? 1 : 0) || _entities.RoomEnemyCount != 2,
                "Smog must use the final PART slot and initialize it in the same update without changing ENEMY count.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_entities.Entities<SmogProjectilePart>().Count != 16,
                "The next firing-animation update must not duplicate the last-slot projectile.");
        }
        LoadValidationRoom(0, 0x60);
    }
}
