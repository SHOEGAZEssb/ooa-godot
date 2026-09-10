using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SwordEnemyRoomEntity : CombatEnemyRoomEntityAdapter<SwordEnemyCharacter>,
    IFixedRoomEntity, IScreenTransitionPreloadRoomEntity, ILinkSwordStateAwareRoomEntity,
    IItemCollisionHittableRoomEntity
{
    private readonly Func<bool> _freePartSlot;
    private SwordActionState _swordState;
    private int _swordLevel;

    internal SwordEnemyRoomEntity(SwordEnemyCharacter enemy, EnemyCombatSourceDescriptor source,
        Action<int> soundRequested, Func<bool> freePartSlot)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
                enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordKnockback,
                soundRequested, EnemySwordResponse.Knockback)) => _freePartSlot = freePartSlot;

    protected override int GaleCollisionMode => Entity.SwordBlocking ? 0x55 : base.GaleCollisionMode;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        bool initializing = Entity.State == SwordEnemyState.Uninitialized;
        Entity.UpdateFrame(frame.Player.EnemyContactPosition, frame.ScentSeedTarget,
            swordSlotAvailable: !initializing || _freePartSlot());
        if (initializing && Entity.State != SwordEnemyState.Uninitialized)
            spawns.Add(new EnemySwordSpawn(Entity, CombatDescriptor.RequestSound, () => !IsSeedBurning));
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        bool initializing = Entity.State == SwordEnemyState.Uninitialized;
        ScreenTransitionPresentation result = Entity.PrepareForScreenTransition(!initializing || _freePartSlot());
        if (initializing && Entity.State != SwordEnemyState.Uninitialized)
            spawns.Add(new EnemySwordSpawn(Entity, CombatDescriptor.RequestSound, () => !IsSeedBurning));
        return result;
    }

    public void SetLinkSwordState(SwordActionState state, int swordLevel)
    { _swordState = state; _swordLevel = swordLevel; }

    public override bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) =>
        // The blocked body still accepts ITEMCOLLISION_L2_SPIN_SWORD $08.
        (!Entity.SwordBlocking || _swordState == SwordActionState.Spin && _swordLevel >= 2) &&
        base.ApplySwordHit(hitbox, sourcePosition, damage, strength, spawns);

    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox,
        Vector2 sourcePosition, int damage, ICollection<RoomEntitySpawn> spawns) =>
        (collision != RoomEntityItemCollision.ExpertPunch || !Entity.SwordBlocking) &&
        base.ApplySwordHit(hitbox, sourcePosition, damage,
            collision is RoomEntityItemCollision.Bomb or RoomEntityItemCollision.ExpertPunch
                ? EnemyKnockbackStrength.High : EnemyKnockbackStrength.Normal, spawns);
}
