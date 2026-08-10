using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SwordEnemyRoomEntity
    : CombatEnemyRoomEntityAdapter<SwordEnemyCharacter>, IFixedRoomEntity,
        ISwordAttackerKnockbackRoomEntity,
        ILinkSwordStateAwareRoomEntity
{
    private int _swordPartInvincibilityCounter;
    private int _pendingAttackerKnockbackFrames;
    private SwordActionState _linkSwordState = SwordActionState.Swing;
    private int _linkSwordLevel = 1;

    internal SwordEnemyRoomEntity(
        SwordEnemyCharacter enemy,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            enemy,
            enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                enemy,
                enemy.Record.DamageQuarters,
                enemy.TakeSwordHit,
                enemy.TakeBurnHit,
                enemy.ApplySwordKnockback,
                soundRequested,
                EnemySwordResponse.Knockback))
    { }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_swordPartInvincibilityCounter > 0)
            _swordPartInvincibilityCounter--;
        Entity.UpdateFrame(frame.Player.Position, frame.ScentSeedTarget);
    }

    public void SetLinkSwordState(SwordActionState state, int swordLevel)
    {
        _linkSwordState = state;
        _linkSwordLevel = swordLevel;
    }

    public override bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.BlocksSwordFrom(sourcePosition))
        {
            return base.ApplySwordHit(
                hitbox,
                sourcePosition,
                damage,
                knockbackStrength,
                spawns);
        }
        // ENEMYCOLLISION_STALFOS_BLOCKED_WITH_SWORD maps Link's sword rows to
        // effect $00: the enemy body silently ignores the hit. The separate,
        // invisible PART_ENEMY_SWORD $1d owns the clink and Link recoil.
        if (!Entity.CollisionEnabled ||
            _linkSwordState == SwordActionState.Spin ||
            _swordPartInvincibilityCounter != 0 ||
            !Entity.EnemySwordCollisionBounds.Intersects(hitbox))
        {
            return false;
        }

        int attackerKnockbackFrames =
            _linkSwordState is SwordActionState.Held or
                SwordActionState.Charged ||
            _linkSwordLevel >= 3
                ? 6
                : 8;
        // COLLISIONEFFECT_$32/$33 applies LINKDMG_$34/$38 to ITEM_SWORD
        // (6/8 knockback updates), but ENEMYDMG_$48/$4c to PART_ENEMY_SWORD
        // starts its signed invincibility counter at $f7/$f5. The part's
        // standard update increments that counter through zero, keeping the
        // blade unavailable for 9/11 updates. Reusing Link's shorter recoil
        // counter lets one uninterrupted swing clink a second time.
        _swordPartInvincibilityCounter = attackerKnockbackFrames + 3;
        _pendingAttackerKnockbackFrames = attackerKnockbackFrames;
        spawns.Add(new EnemyClinkSpawn(
            CollisionMidpoint(
                Entity.EnemySwordPosition,
                hitbox.GetCenter())));
        return true;
    }

    public bool TryGetSwordAttackerKnockback(
        EnemyKnockbackStrength strength,
        out SwordAttackerKnockback response)
    {
        int frames = _pendingAttackerKnockbackFrames;
        _pendingAttackerKnockbackFrames = 0;
        response = new SwordAttackerKnockback(
            Entity.EnemySwordPosition,
            frames);
        return frames != 0;
    }
}
