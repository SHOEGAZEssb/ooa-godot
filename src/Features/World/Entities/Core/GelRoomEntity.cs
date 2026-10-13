using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GelRoomEntity
    : CombatEnemyRoomEntityAdapter<GelCharacter>, IFixedRoomEntity, IPlayerRestriction, ISomariaBlockCollisionRoomEntity,
        IAfterPlayerUpdateRoomEntity, IBoomerangCollisionRoomEntity
{
    protected override void DamageByBoomerang(BoomerangItem item, int effect, ICollection<RoomEntitySpawn> spawns)
    {
        if (effect != CollisionEffect.SwordNoKnockback || !Entity.TakeBoomerangHit(item.Position, item.Damage))
            throw new InvalidOperationException("ENEMY_GEL $43 rejected eligible boomerang effect$0b.");
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
    }
    protected override bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition)
    {
        // objectCollisionTable's Gel mode $33 selects collisionEffect0b /
        // ENEMYDMG_0c: $fe damage, $20 invincibility and zero recoil, with
        // JUST_HIT left for gel.s's next dispatch. The hook retracts rather
        // than exchanging this enemy (Gel's state $03 is a stub).
        if (effect != CollisionEffect.SwordNoKnockback ||
            !Entity.TakeDeferredNoKnockbackHit(linkPosition, hook.HitDamage)) return false;
        Entity.DeferNativeHitStatus();
        hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }
    public GelRoomEntity(
        GelCharacter gel,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            gel,
            gel.SetTransitionDrawOffset,
            EnemyCombatDescriptor.FromSource(
                combatSource,
                CreateCombat(gel, soundRequested),
                EnemySwordResponse.NoKnockback, soundRequested: soundRequested),
            collisionZ: () => gel.ZFixed >> 8)
    { }

    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns) =>
        ApplySomariaBlockCollision(block, Entity.Definition.RawDamage, Entity.NativeHitPending, spawns, deferNativeStatus: false);

    protected override void ApplySomariaEnemyDamage(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.TakeSomariaHit(block.Position, -block.Damage))
            throw new InvalidOperationException("ENEMY$43 rejected an eligible Somaria effect$2f collision.");
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
    }

    private bool _disablesMovement;
    public bool DisablesSword => Entity.AttachmentRestrictionActive;
    public bool AlternatesMovementWithSwordRestriction => false;
    public bool DisablesMovement => _disablesMovement;
    public void AfterPlayerUpdate()
    {
        Entity.ClearAttachmentRestriction();
        _disablesMovement = false;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player.Position, frame.Player.FacingVector, frame.AnyButtonJustPressed);
        _disablesMovement = Entity.AttachmentRestrictionActive && (frame.Counter & 1) != 0;
    }

    private static EnemyCombatComponent CreateCombat(
        GelCharacter gel,
        Action<int> soundRequested) =>
        new(
            () => gel.IsDead,
            () => gel.CollisionBounds,
            (_, damage) => gel.TakeSwordHit(damage),
            gel.TakeSwordHit,
            player =>
            {
                if (gel.OverlapsLink(player.EnemyContactPosition))
                    gel.QueueLinkContact();
            },
            () => gel.IsDead && !gel.DiedInHazard
                ? new EnemyDeathPuffSpawn(gel.Position, EnemyId: gel.Definition.Id)
                : null,
            (sourcePosition, strength) =>
            {
                gel.ApplySwordNoKnockback(sourcePosition, strength);
                soundRequested(SoundId.SndDamageEnemy);
            });
}
