using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PolsVoiceRoomEntity
    : CombatEnemyRoomEntityAdapter<PolsVoiceCharacter>, IFixedRoomEntity,
        IInstrumentReactiveRoomEntity, IScreenTransitionPreloadRoomEntity,
        IItemCollisionHittableRoomEntity, IExpertPunchHittableRoomEntity
{
    private readonly IReadOnlyList<EnemyBehaviorValue> _collisionEffects =
        EnemyBehaviorTables.Shared.PolsVoiceCollisionEffects;
    private bool _instrumentDeathPuffQueued;

    internal PolsVoiceRoomEntity(
        PolsVoiceCharacter polsVoice,
        EnemyCombatSourceDescriptor combatSource,
        Action<int> soundRequested)
        : base(
            polsVoice,
            polsVoice.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(
                combatSource,
                polsVoice,
                polsVoice.Record.DamageQuarters,
                polsVoice.TakeBumpHit,
                polsVoice.TakeBurnHit,
                polsVoice.ApplyPolsVoiceBump,
                soundRequested,
                EnemySwordResponse.Bump,
                acceptedHitSound: 0),
            collisionZ: () => polsVoice.ZHigh)
    { }

    public override bool ApplySwordHit(
        Godot.Rect2 hitbox,
        Godot.Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        int expectedEffect = knockbackStrength switch
        {
            EnemyKnockbackStrength.Low => 0x0c,
            EnemyKnockbackStrength.Normal => 0x0d,
            EnemyKnockbackStrength.High => 0x0e,
            _ => throw new ArgumentOutOfRangeException(
                nameof(knockbackStrength), knockbackStrength,
                "Pols Voice received an unknown sword collision strength.")
        };
        RequireCollisionEffect(
            knockbackStrength switch
            {
                EnemyKnockbackStrength.Low => 0x04,
                EnemyKnockbackStrength.Normal => 0x05,
                _ => 0x08
            },
            expectedEffect);
        return base.ApplySwordHit(
            hitbox, sourcePosition, damage, knockbackStrength, spawns);
    }

    public bool ApplyExpertPunch(
        Godot.Rect2 hitbox,
        Godot.Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns) =>
        ApplyDamagingCollision(
            RoomEntityItemCollision.ExpertPunch,
            hitbox,
            sourcePosition,
            damage,
            EnemyKnockbackStrength.Normal,
            spawns);

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Godot.Rect2 hitbox,
        Godot.Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns) => collision switch
    {
        RoomEntityItemCollision.ExpertPunch => ApplyExpertPunch(
            hitbox, sourcePosition, damage, spawns),
        RoomEntityItemCollision.ThrownObject => ApplyDamagingCollision(
            collision, hitbox, sourcePosition, damage,
            EnemyKnockbackStrength.Normal, spawns),
        RoomEntityItemCollision.Bomb => ApplyDamagingCollision(
            collision, hitbox, sourcePosition, damage,
            EnemyKnockbackStrength.High, spawns),
        RoomEntityItemCollision.SwordBeam => ApplyBumpCollision(
            collision, hitbox, sourcePosition,
            EnemyKnockbackStrength.Normal, spawns),
        _ => false
    };

    public override SeedHitResult ApplySeedHit(
        Godot.Rect2 hitbox,
        Godot.Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            !CombatDescriptor.Combat.Intersects(hitbox))
        {
            return SeedHitResult.None;
        }
        switch (seedItem)
        {
            case 0x20: // ITEM_EMBER_SEED -> COLLISIONEFFECT_20, no health damage.
                RequireCollisionEffect(0x1b, 0x20);
                return SeedHitResult.Activate;
            case 0x21: // ITEM_SCENT_SEED -> COLLISIONEFFECT_BUMP.
                RequireCollisionEffect(0x1c, 0x0d);
                return base.ApplySwordHit(
                    hitbox,
                    sourcePosition,
                    damage: 0,
                    EnemyKnockbackStrength.Normal,
                    spawns)
                        ? SeedHitResult.Activate
                        : SeedHitResult.None;
            case 0x24: // ITEM_MYSTERY_SEED -> COLLISIONEFFECT_20 special path.
                RequireCollisionEffect(0x1a, 0x20);
                return SeedHitResult.Activate;
            default:
                return SeedHitResult.None;
        }
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.Position, instrumentPlaying: false);

    public void UpdateDuringInstrument(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player.Position, instrumentPlaying: true);
        if (!Entity.DiedFromInstrument || _instrumentDeathPuffQueued ||
            CombatDescriptor.Combat.CreateDeathPuff() is not { } deathPuff)
        {
            return;
        }
        _instrumentDeathPuffQueued = true;
        spawns.Add(deathPuff with
        {
            DecrementsRoomCount = CombatDescriptor.CountsAsEnemy,
            UpdateThisFrame = true
        });
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.PrepareForScreenTransition();

    private bool ApplyDamagingCollision(
        RoomEntityItemCollision collision,
        Godot.Rect2 hitbox,
        Godot.Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        int collisionType = (int)collision;
        RequireCollisionEffect(
            collisionType,
            collision == RoomEntityItemCollision.Bomb ? 0x0a : 0x09);
        if (!CombatDescriptor.Combat.Intersects(hitbox) ||
            !Entity.TakeDamagingHit(sourcePosition, damage))
        {
            return false;
        }
        Entity.ApplySwordKnockback(sourcePosition, knockbackStrength);
        CombatDescriptor.RequestSound(OracleSoundEngine.SndDamageEnemy);
        return true;
    }

    private bool ApplyBumpCollision(
        RoomEntityItemCollision collision,
        Godot.Rect2 hitbox,
        Godot.Vector2 sourcePosition,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        RequireCollisionEffect((int)collision, 0x0d);
        return base.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage: 0,
            knockbackStrength,
            spawns);
    }

    private void RequireCollisionEffect(int collisionType, int expectedEffect)
    {
        EnemyBehaviorValue effect = _collisionEffects[collisionType];
        if (effect.Value != expectedEffect)
        {
            throw new InvalidOperationException(
                $"{effect.Source} maps ENEMYCOLLISION_POLS_VOICE $21 item " +
                $"collision ${collisionType:x2} to effect ${effect.Value:x2}; " +
                $"runtime support requires ${expectedEffect:x2}.");
        }
    }
}
