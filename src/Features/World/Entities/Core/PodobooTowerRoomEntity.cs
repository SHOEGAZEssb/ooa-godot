using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PodobooTowerRoomEntity(PodobooTowerCharacter enemy,
    EnemyCombatSourceDescriptor source, Action<int> soundRequested)
    : CombatEnemyRoomEntityAdapter<PodobooTowerCharacter>(enemy, enemy.SetTransitionDrawOffset,
        EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
            enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordNoKnockback,
            soundRequested, EnemySwordResponse.NoKnockback,
            completedOutcome: () => enemy.MysterySeedDeath
                ? RoomEnemyOutcome.EnemyDieUncounted()
                : RoomEnemyOutcome.EnemyDie(source.KillableEnemyIndex), dropsItem: false)),
        IFixedRoomEntity, IItemCollisionHittableRoomEntity, IExpertPunchHittableRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns)
    {
        int effect = EnemyBehaviorTables.Shared.PodobooTowerCollisionEffects[(int)collision].Value;
        if (effect != 0x0b)
            throw new InvalidOperationException($"podobooTower.s: collision ${(int)collision:x2} effect ${effect:x2} is unsupported.");
        return base.ApplySwordHit(hitbox, origin, damage, EnemyKnockbackStrength.Low, spawns);
    }
    public bool ApplyExpertPunch(Rect2 hitbox, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        ApplyItemCollision(RoomEntityItemCollision.ExpertPunch, hitbox, origin, damage, spawns);

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 origin, int seed,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 || !hitbox.Intersects(Entity.CollisionBounds))
            return SeedHitResult.None;
        if (seed == 0x24)
        {
            Entity.KillWithMysterySeed();
            spawns.Add(new EnemyDeathPuffSpawn(Entity.Position, EnemyId: 0x2d,
                DecrementsRoomCount: false, DropsItem: false));
            return SeedHitResult.Activate;
        }
        if (seed is not (0x20 or 0x21)) return SeedHitResult.None;
        return base.ApplySwordHit(hitbox, origin, seed == 0x20 ? 0x7f : 2,
            EnemyKnockbackStrength.Low, spawns) ? SeedHitResult.Activate : SeedHitResult.None;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter, spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Hidden;
    }
}
