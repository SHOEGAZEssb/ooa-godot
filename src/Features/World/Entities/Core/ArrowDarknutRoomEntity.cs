using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ArrowDarknutRoomEntity(ArrowDarknutCharacter enemy,
    EnemyCombatSourceDescriptor source, Action<int> soundRequested)
    : CombatEnemyRoomEntityAdapter<ArrowDarknutCharacter>(enemy, enemy.SetTransitionDrawOffset,
        EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
            enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordKnockback,
            soundRequested, EnemySwordResponse.Knockback), collisionZ: () => enemy.ZFixed >> 8),
        IFixedRoomEntity, ILinkSwordStateAwareRoomEntity, IScreenTransitionPreloadRoomEntity,
        IItemCollisionHittableRoomEntity, IExpertPunchHittableRoomEntity, ISomariaBlockCollisionRoomEntity, IBoomerangCollisionRoomEntity
{
    private int _swordCollision = ItemCollisionType.L1Sword;
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Record.RawDamage, Entity.NativeHitPending, spawns);
    public void SetLinkSwordState(SwordActionState state, int level) => _swordCollision = SwordCollision.Type(state, level);
    public override bool ApplySwordHit(Rect2 hitbox, Vector2 origin, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        Hit(_swordCollision, hitbox, origin, damage, spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns) => Hit((int)collision, hitbox, origin, damage, spawns);
    public bool ApplyExpertPunch(Rect2 hitbox, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) =>
        Hit(ItemCollisionType.ExpertPunch, hitbox, origin, damage, spawns);

    private bool Hit(int collision, Rect2 hitbox, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns)
    {
        int effect = EnemyBehaviorTables.Shared.ArrowDarknutCollisionEffects[collision].Value;
        EnemyKnockbackStrength strength = effect switch
        {
            CollisionEffect.SwordLowKnockback => EnemyKnockbackStrength.Low,
            CollisionEffect.Sword => EnemyKnockbackStrength.Normal,
            CollisionEffect.SwordHighKnockback => EnemyKnockbackStrength.High,
            _ => throw new InvalidOperationException($"arrowDarknut.s: collision ${collision:x2} effect ${effect:x2} is unsupported.")
        };
        return base.ApplySwordHit(hitbox, origin, damage, strength, spawns);
    }

    public override SeedHitResult ApplySeedHit(Rect2 hitbox, Vector2 origin, int seed,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 || !hitbox.Intersects(Entity.CollisionBounds))
            return SeedHitResult.None;
        if (seed == 0x21)
            return Hit(ItemCollisionType.ScentSeed, hitbox, origin, 2, spawns) ? SeedHitResult.Activate : SeedHitResult.None;
        // Collision row $20 ignores fire; mystery seeds retain the shared effect.
        return seed == 0x24 ? SeedHitResult.Activate : SeedHitResult.None;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int angle = Entity.UpdateFrame(frame.Player.Position, frame.ScentSeedTarget, frame.Counter);
        if (angle >= 0) spawns.Add(new EnemyArrowSpawn(Entity.Position, angle));
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.State == ArrowMoblinState.Uninitialized) Entity.UpdateFrame(Vector2.Zero);
        return ScreenTransitionPresentation.Visible;
    }
}
