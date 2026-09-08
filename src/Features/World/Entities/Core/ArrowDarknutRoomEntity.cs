using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ArrowDarknutRoomEntity(ArrowDarknutCharacter enemy,
    EnemyCombatSourceDescriptor source, Action<int> soundRequested)
    : CombatEnemyRoomEntityAdapter<ArrowDarknutCharacter>(enemy, enemy.SetTransitionDrawOffset,
        EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
            enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordKnockback,
            soundRequested, EnemySwordResponse.Knockback)),
        IFixedRoomEntity, ILinkSwordStateAwareRoomEntity, IScreenTransitionPreloadRoomEntity,
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
        int effect = EnemyBehaviorTables.Shared.ArrowDarknutCollisionEffects[collision].Value;
        EnemyKnockbackStrength strength = effect switch
        {
            0x08 => EnemyKnockbackStrength.Low,
            0x09 => EnemyKnockbackStrength.Normal,
            0x0a => EnemyKnockbackStrength.High,
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
            return Hit(0x1c, hitbox, origin, 2, spawns) ? SeedHitResult.Activate : SeedHitResult.None;
        // Collision row $20 ignores fire; mystery seeds retain the shared effect.
        return seed == 0x24 ? SeedHitResult.Activate : SeedHitResult.None;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int angle = Entity.UpdateFrame(frame.Player.Position, frame.ScentSeedTarget);
        if (angle >= 0) spawns.Add(new EnemyArrowSpawn(Entity.Position, angle));
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.State == ArrowMoblinState.Uninitialized) Entity.UpdateFrame(Vector2.Zero);
        return ScreenTransitionPresentation.Visible;
    }
}
