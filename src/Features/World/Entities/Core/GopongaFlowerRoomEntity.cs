using System;
using System.Collections.Generic;
using Godot;

namespace oracleofages;

internal sealed class GopongaFlowerRoomEntity(GopongaFlowerCharacter enemy,
    EnemyCombatSourceDescriptor source, Action<int> soundRequested)
    : CombatEnemyRoomEntityAdapter<GopongaFlowerCharacter>(enemy, enemy.SetTransitionDrawOffset,
        EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
            enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordNoKnockback,
            soundRequested, EnemySwordResponse.NoKnockback)),
        IFixedRoomEntity, IScreenTransitionPreloadRoomEntity, ILinkSwordStateAwareRoomEntity,
        IItemCollisionHittableRoomEntity, IExpertPunchHittableRoomEntity
{
    private int _swordCollision = 4;
    public void SetLinkSwordState(SwordActionState state, int level) => _swordCollision = state switch
    {
        SwordActionState.Spin => 8,
        SwordActionState.Held or SwordActionState.Charged => 9,
        _ => level + 3
    };
    public override bool ApplySwordHit(Rect2 hitbox, Vector2 origin, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        Hit(_swordCollision, hitbox, origin, damage, spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns) => Hit((int)collision, hitbox, origin, damage, spawns);
    public bool ApplyExpertPunch(Rect2 hitbox, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        Hit(0x0b, hitbox, origin, damage, spawns);

    private bool Hit(int collision, Rect2 hitbox, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 || !hitbox.Intersects(Entity.CollisionBounds)) return false;
        int effect = EnemyBehaviorTables.Shared.GopongaFlowerCollisionEffects[collision].Value;
        if (effect == 0x0b) return base.ApplySwordHit(hitbox, origin, damage, EnemyKnockbackStrength.Low, spawns);
        if (effect == 0x1c)
        {
            spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position, hitbox.GetCenter())));
            return true;
        }
        if (effect is 0 or 0x20) return false;
        throw new InvalidOperationException($"gopongaFlower.s collision ${collision:x2}: unsupported effect ${effect:x2}.");
    }

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 origin, int seed,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 || !hitbox.Intersects(Entity.CollisionBounds)) return SeedHitResult.None;
        // $23's satchel entries are no-op ($20); only a fire projectile uses
        // effect $34. Shooter flames use the same free-flame owner as Zoras.
        if (seed != 0x20) return SeedHitResult.None;
        base.ApplySwordHit(hitbox, origin, 0x7f, EnemyKnockbackStrength.None, spawns);
        return SeedHitResult.Activate;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareForScreenTransition();
        return ScreenTransitionPresentation.Visible;
    }
}
