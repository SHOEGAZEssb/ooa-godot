using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal abstract class CombatEnemyRoomEntityAdapter<T>(
    T entity,
    Action<Vector2> setTransitionDrawOffset,
    EnemyCombatDescriptor combatDescriptor,
    Action? finished = null,
    Func<int>? collisionZ = null)
    : RoomEntityAdapter<T>(entity, setTransitionDrawOffset),
        ILinkContactEntity, ISwordHittableRoomEntity, ISeedHittableRoomEntity,
        ISeedBurnTarget, IRoomEntityLifetime,
        IRoomEnemyCounterEntity, IRoomEnemyOutcomeSource,
        IObjectCollisionHeightRoomEntity, IDimitriMouthTarget, IGaleSeedTarget, ISwitchHookHittableRoomEntity,
        IBiggoronSwordCollisionRoomEntity, IBombchuTargetRoomEntity, IBombCollisionRoomEntity
    where T : EnemyCharacter
{
    public virtual BombchuTarget BombchuTarget => new(combatDescriptor.Source?.Id ??
        throw new NotSupportedException($"{Entity.GetType().Name}: bombchuTargets requires the original ENEMY ID."),
        Entity.Visible, Entity.Position, Entity.CollisionBounds, Entity.Health);
    private bool _seedBurning;
    public bool HasBombCollisionIdentity => DimitriCollisionType >= 0;
    public virtual bool ApplyBombCollision(IBombExplosionRoomEntity bomb, ICollection<RoomEntitySpawn> spawns)
    {
        var data = BombCollisionDatabase.Shared;
        if (!bomb.CollisionEnabled || !Entity.CollisionEnabled || Entity.NativeHitPending || Entity.InvincibilityCounter != 0 ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, bomb.CollisionZ, bomb.CollisionZRadius) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bomb.CollisionBounds)) return false;
        if (!HasBombCollisionIdentity)
            throw new NotSupportedException($"{Entity.GetType().Name}: ITEMCOLLISION_BOMB $18 requires its original ENEMY collision identity (collisionEffects.s:enemyCheckCollisions).");
        if (!data.EnemyEnabled(DimitriCollisionType)) return false;
        int effect = data.Effect(DimitriCollisionMode);
        if (effect == CollisionEffect.None) return true;
        // Explicit species item handlers already own their non-sword Bomb row.
        if (this is IItemCollisionHittableRoomEntity native)
            return native.ApplyItemCollision(RoomEntityItemCollision.Bomb, bomb.CollisionBounds,
                bomb.CollisionBounds.GetCenter(), bomb.Damage, spawns);
        if (effect == CollisionEffect.SwordNoKnockback)
            return ApplyNoKnockbackItemHit(bomb.CollisionBounds.GetCenter(), bomb.Damage);
        var strength = CollisionKnockback(ItemCollisionType.Bomb, effect);
        if (combatDescriptor.Source is null)
            throw new NotSupportedException($"ENEMY ${DimitriCollisionType:x2} mode ${DimitriCollisionMode:x2}: Bomb effect ${effect:x2} has no source-backed damage owner.");
        bool accepted = combatDescriptor.Combat.ApplyDamageAfterCollision(bomb.CollisionBounds.GetCenter(), bomb.Damage,
            strength, spawns, combatDescriptor.CountsAsEnemy);
        if (accepted) Entity.DeferNativeHitStatus();
        return accepted;
    }
    private bool _completedOutcomeTaken;
    private readonly GaleSeedEnemyMotion _gale = new(entity);
    private bool _nativeGaleHitPending;
    public virtual bool GaleCaught => _gale.Active;
    protected virtual int GaleCollisionMode => DimitriCollisionMode;

    protected virtual bool BoomerangHitPending => Entity.NativeHitPending;
    protected virtual bool Stunned => false;
    protected virtual void MarkBoomerangHit() => Entity.DeferNativeHitStatus();
    protected virtual void TransformByBoomerang() => throw new NotSupportedException(
        $"Enemy${DimitriCollisionType:x2} mode${DimitriCollisionMode:x2}: boomerang effect$35 has no transformation owner.");
    protected virtual void DamageByBoomerang(BoomerangItem item, int effect, ICollection<RoomEntitySpawn> spawns)
    {
        var strength = effect == CollisionEffect.SwordLowKnockback ? EnemyKnockbackStrength.Low : effect == CollisionEffect.SwordHighKnockback ? EnemyKnockbackStrength.High : EnemyKnockbackStrength.Normal;
        if (!combatDescriptor.Combat.ApplyDamageAfterCollision(item.Position, item.Damage, strength, spawns, combatDescriptor.CountsAsEnemy))
            throw new InvalidOperationException($"Enemy${DimitriCollisionType:x2} rejected eligible boomerang damage effect${effect:x2}.");
        MarkBoomerangHit();
    }
    public virtual BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns)
    {
        var data = BoomerangCollisionDatabase.Shared;
        if (!item.CollisionEnabled || !data.EnemyEnabled(DimitriCollisionType) || GaleCaught ||
            !Entity.CollisionEnabled || BoomerangHitPending || Entity.InvincibilityCounter != 0 ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, item.ZHigh, 7) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, item.CollisionBounds)) return default;
        int effect = data.Effect(DimitriCollisionMode);
        switch (effect)
        {
            case CollisionEffect.None: return new(true, false);
            case CollisionEffect.SwordLowKnockback: case CollisionEffect.Sword: case CollisionEffect.SwordHighKnockback: case CollisionEffect.SwordNoKnockback:
                DamageByBoomerang(item, effect, spawns);
                break;
            case CollisionEffect.Effect1b:
                Entity.InvincibilityCounter = data.DeflectionInvincibility;
                MarkBoomerangHit();
                return new(true, true, BoomerangCollisionResponse.Midpoint(Entity.Position, item.Position));
            case CollisionEffect.Effect1c:
                MarkBoomerangHit();
                break;
            case CollisionEffect.Stun:
                Entity.ApplyBoomerangStun(data.StunCounter);
                Entity.InvincibilityCounter = data.StunInvincibility;
                combatDescriptor.RequestSound(SoundId.SndDamageEnemy);
                break;
            case CollisionEffect.Effect35:
                TransformByBoomerang();
                break;
            default: throw new NotSupportedException($"Enemy${DimitriCollisionType:x2} mode${DimitriCollisionMode:x2}: " +
                $"objectCollisionTable column$17 effect${effect:x2} is not represented.");
        }
        return new(true, true);
    }

    public bool TryCatchGale(Rect2 hitbox, int seedZ, Func<byte> random)
    {
        if (GaleCaught || !Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            !combatDescriptor.Combat.Intersects(hitbox) ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, seedZ, 7)) return false;
        int effect = GaleSeedCollisionDatabase.Shared.Effect(GaleCollisionMode);
        if (effect == CollisionEffect.Effect20) return true;
        if (effect != CollisionEffect.GaleSeed) return false;
        _seedBurning = false;
        _gale.Begin(hitbox.GetCenter(), CollisionZ, random);
        return true;
    }

    protected void BeginNativeGale(Vector2 position, Func<byte> random)
    {
        _gale.Begin(position, CollisionZ, random);
        // collisionEffect29 writes var2a=$9e. Native Gibdo/Fire Keese return
        // from that JUST_HIT dispatch before their state5 handler can run.
        _nativeGaleHitPending = true;
    }
    public void UpdateGale(int cameraY)
    {
        if (_nativeGaleHitPending) { _nativeGaleHitPending = false; return; }
        _gale.Update(cameraY);
    }

    public bool Finished => combatDescriptor.Combat.Finished;
    public virtual bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition)
    {
        var collisions = SwitchHookCollisionDatabase.Shared;
        if (!collisions.EnemyEnabled(DimitriCollisionType)) return false;
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, 0, 7) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,hook.CollisionBounds)) return false;
        int effect = collisions.Effect(DimitriCollisionMode);
        // Even a no-op collision returns out of this enemy's collision scan,
        // so Link contact is skipped for this enemy while later enemies run.
        if (effect == CollisionEffect.None) return true;
        if (effect == CollisionEffect.SwitchHook && Entity.Health > 0 && Entity is ISwitchHookEnemy target)
        {
            target.BeginSwitchHook(linkPosition);
            hook.LatchEnemy(target);
            return true;
        }
        if (TryApplySwitchHookEffect(effect, hook, linkPosition)) return true;
        throw new NotSupportedException($"Switch Hook collision for enemy ${combatDescriptor.Source?.Id:x2}:${combatDescriptor.Source?.SubId:x2} " +
            $"({combatDescriptor.Source?.Source}), effect ${effect:x2}, is not implemented; " +
            "data/ages/objectCollisionTable.s:ITEMCOLLISION_SWITCH_HOOK $0d / object_code/common/items/switchHook.s:state3.");
    }
    protected virtual bool TryApplySwitchHookEffect(int effect, SwitchHookItem hook, Vector2 linkPosition) => false;
    public virtual bool CountsAsEnemy =>
        combatDescriptor.CountsAsEnemy &&
        // enemyDie transfers its count to PART_ENEMY_DESTROYED. Preserve
        // that count until OnFinished installs the replacement; interactions
        // later in this update must not observe a temporary cleared room.
        (!combatDescriptor.Combat.Finished || Entity.HasCompletedKnockbackDeath);
    public bool IsSeedBurning => _seedBurning;
    public virtual bool FreezesDuringSeedBurn => true;
    public Vector2 SeedBurnPosition => Entity.Position;
    protected EnemyCombatDescriptor CombatDescriptor => combatDescriptor;
    // These members satisfy IBurningEnemyTarget only on adapters which already
    // opt into that interface. Do not enroll legacy enemies in the native scan.
    public virtual bool BurnTargetAlive => GodotObject.IsInstanceValid(Entity) && !Entity.IsDead;
    public Vector2 BurnPosition => Entity.Position;
    public int BurnHealth { get => Entity.Health; set => Entity.Health = value; }
    public virtual void ReleaseBurn() => Entity.InvincibilityCounter = 0;

    protected bool NativeCollisionEligible(int collision, Rect2 bounds,
        IReadOnlyList<EnemyBehaviorValue> mask, bool pendingHit) =>
        !GaleCaught && !pendingHit && Entity.CollisionEnabled && Entity.InvincibilityCounter == 0 &&
        mask[collision].Value != 0 && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);

    // collisionEffects.s shares status dispatch; species retain health/enable
    // bits, state-5/reservation writes and the item's collision-bit policy.
    protected virtual void BeginSeedStatus(bool ember, int damage) =>
        throw new NotSupportedException($"{combatDescriptor.Source?.Source}: ENEMY ${DimitriCollisionType:x2} has no native seed status owner.");
    protected virtual void CatchStatusGale(Vector2 origin, Func<byte> random) =>
        throw new NotSupportedException($"{combatDescriptor.Source?.Source}: ENEMY ${DimitriCollisionType:x2} has no native Gale status owner.");
    protected virtual bool SeedStatusDisablesCollision(int effect) =>
        effect is CollisionEffect.Effect20 or CollisionEffect.GaleSeed;
    protected virtual bool SeedStatusAcceptsStrongKnockback => false;
    protected virtual int SeedStatusBurnEffect => CollisionEffect.Burn;
    protected SeedCollisionResponse ApplyStatusSeedCollision(int effect, int collision,
        Rect2 bounds, Vector2 origin, SeedRecord seed, ICollection<RoomEntitySpawn> spawns,
        Func<bool> freePart, Func<byte> random)
    {
        switch (effect)
        {
            case CollisionEffect.None: return new(true, SeedHitResult.None, false);
            case CollisionEffect.Effect20: break;
            case CollisionEffect.SwordLowKnockback:
            case CollisionEffect.Sword when SeedStatusAcceptsStrongKnockback:
            case CollisionEffect.SwordHighKnockback when SeedStatusAcceptsStrongKnockback:
                ((IItemCollisionHittableRoomEntity)this).ApplyItemCollision(
                    (RoomEntityItemCollision)collision, bounds, origin, -unchecked((sbyte)seed.Damage), spawns);
                break;
            case CollisionEffect.Burn when SeedStatusBurnEffect == CollisionEffect.Burn:
            case CollisionEffect.Effect34 when SeedStatusBurnEffect == CollisionEffect.Effect34:
                BeginSeedStatus(ember: true, -unchecked((sbyte)seed.Damage));
                if (freePart()) spawns.Add(new BurningEnemySpawn((IBurningEnemyTarget)this));
                break;
            case CollisionEffect.PegasusSeed:
                BeginSeedStatus(ember: false, 0);
                combatDescriptor.RequestSound(SoundId.SndDamageEnemy);
                break;
            case CollisionEffect.GaleSeed: CatchStatusGale(origin, random); break;
            default: throw new NotSupportedException($"{combatDescriptor.Source?.Source}: ENEMY ${DimitriCollisionType:x2}:${combatDescriptor.Source?.SubId:x2}, seed collision ${collision:x2}, effect ${effect:x2} requires its native response.");
        }
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,
            SeedStatusDisablesCollision(effect));
    }

    protected EnemyKnockbackStrength CollisionKnockback(int collision, int effect) => effect switch
    {
        // code/collisionEffects.s:collisionEffect08/09/0a select ENEMYDMG_00/04/08.
        CollisionEffect.SwordLowKnockback => EnemyKnockbackStrength.Low,
        CollisionEffect.Sword => EnemyKnockbackStrength.Normal,
        CollisionEffect.SwordHighKnockback => EnemyKnockbackStrength.High,
        _ => throw new NotSupportedException($"{combatDescriptor.Source?.Source}: ENEMY ${DimitriCollisionType:x2}:${combatDescriptor.Source?.SubId:x2}, collision ${collision:x2}, effect ${effect:x2} requires its native response.")
    };
    protected bool ApplyBoomerangTransformCollision(Rect2 bounds,
        IReadOnlyList<EnemyBehaviorValue> mask, IReadOnlyList<EnemyBehaviorValue> effects, Action hit)
    {
        const int collision = (int)RoomEntityItemCollision.Boomerang;
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 || mask[collision].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds)) return false;
        if (effects[collision].Value != 0x35)
            throw new NotSupportedException($"Enemy${DimitriCollisionType:x2} boomerang transformation requires collisionEffect35.");
        hit();
        return true;
    }
    protected bool ApplyIneffectiveWeaponCollision(int collision, Rect2 bounds,
        IReadOnlyList<EnemyBehaviorValue> mask, IReadOnlyList<EnemyBehaviorValue> effects,
        out bool reportsContact)
    {
        reportsContact = false;
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 || mask[collision].Value == 0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds)) return false;
        switch (effects[collision].Value)
        {
            case 0: return true;
            case 0x20: return true; // Projectile owner consumes the hit; no enemy status changes.
            case 0x1c:
                // Both damage rows are$1c: report weapon contact without
                // health, recoil, invincibility or sound. Species using this
                // helper must continue their AI on this non-boomerang hit.
                reportsContact = true;
                return true;
            default: throw new NotSupportedException($"Enemy${DimitriCollisionType:x2} ineffective weapon column${collision:x2}: effect${effects[collision].Value:x2} is not represented.");
        }
    }
    protected bool ApplySomariaBlockCollision(SomariaBlock block, int rawDamage,
        bool pendingHit, ICollection<RoomEntitySpawn> spawns, bool deferNativeStatus = true)
    {
        int type = DimitriCollisionType;
        if (type < 0 || !block.CollisionEnabled || !Entity.CollisionEnabled ||
            pendingHit || Entity.InvincibilityCounter != 0 ||
            !SomariaCollisionDatabase.Shared.Enemy((byte)type).Block ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, block.ZHigh, 7) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, block.CollisionBounds)) return false;
        int effect = SomariaCollisionDatabase.Shared.Effects((byte)DimitriCollisionMode).Block;
        switch (effect)
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect2d: block.Flags |= 0x20; return true;
            case CollisionEffect.Effect2f:
                // LINKDMG_30 precedes ENEMYDMG_04. Ordinary enemy initialization
                // sets var3e=$01; the block receives the enemy's raw damage,
                // without Link's ring or contact-damage policy.
                block.QueueEnemyDamage(rawDamage, 1, Entity.Position);
                ApplySomariaEnemyDamage(block, spawns);
                if (deferNativeStatus) Entity.DeferNativeHitStatus();
                return true;
            default: throw new NotSupportedException($"Enemy ${type:x2} mode ${DimitriCollisionMode:x2} Somaria effect ${effect:x2} is not represented (objectCollisionTable).");
        }
    }
    protected virtual void ApplySomariaEnemyDamage(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!combatDescriptor.Combat.ApplyDamageAfterCollision(block.Position,
            -block.Damage, EnemyKnockbackStrength.Normal, spawns, combatDescriptor.CountsAsEnemy))
            throw new InvalidOperationException($"Enemy ${DimitriCollisionType:x2} rejected an eligible Somaria effect$2f damage exchange.");
    }
    protected bool SeedBurning => _seedBurning;
    protected int KillableEnemyIndex =>
        combatDescriptor.KillableEnemyIndex;
    public int CollisionZ => collisionZ?.Invoke() ?? 0;
    public virtual int DimitriCollisionMode => combatDescriptor.Source?.CollisionMode & 0x7f ?? 0;
    // Special combat owners without a source collision identity are explicitly
    // excluded until they supply their native type, rather than borrowing $00.
    public virtual int DimitriCollisionType => combatDescriptor.Source?.Id ?? -1;
    public bool TrySwallow(Rect2 hitbox)
    {
        if (!Entity.CollisionEnabled || Entity.InvincibilityCounter != 0 ||
            CollisionZ != 0 || !combatDescriptor.Combat.Intersects(hitbox)) return false;
        Entity.Swallow();
        return true;
    }
    protected void HandleNativeLinkContact(Player player, int damageQuarters)
    {
        if (GaleCaught || Entity.NativeHitPending || !Entity.CollisionEnabled || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        if (player.IsUsingShield && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            int before = Entity.InvincibilityCounter;
            HandleLinkContactCore(player);
            if (before != Entity.InvincibilityCounter) Entity.DeferNativeHitStatus();
            return;
        }
        if (!Stunned && player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ) &&
            player.ApplyEnemyContactDamage(Entity.Position, damageQuarters)) OnNativeLinkHit();
    }
    protected virtual void OnNativeLinkHit() => Entity.DeferNativeHitStatus();

    protected bool ApplyNativeItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin,
        int damage, ICollection<RoomEntitySpawn> spawns,
        IReadOnlyList<EnemyBehaviorValue> activeCollisions, IReadOnlyList<EnemyBehaviorValue> effects)
    {
        if (!NativeCollisionEligible((int)collision, bounds, activeCollisions, Entity.NativeHitPending)) return false;
        int effect = effects[(int)collision].Value;
        if (effect is CollisionEffect.None or CollisionEffect.Effect20) return true;
        var strength = CollisionKnockback((int)collision, effect);
        return ApplySwordHit(bounds, origin, damage, strength, spawns);
    }

    public virtual void HandleLinkContact(Player player) => HandleLinkContactCore(player);

    private void HandleLinkContactCore(Player player)
    {
        if (!player.EnemyContactHeightOverlaps(CollisionZ))
            return;
        if (GaleCaught || _seedBurning && FreezesDuringSeedBurn)
            return;

        if (player.IsUsingShield &&
            combatDescriptor.Source is { } source &&
            source.ShieldBumpResponse(player.Inventory.ShieldLevel) is
                { } response &&
            combatDescriptor.Combat.Intersects(player.ShieldCollisionBounds))
        {
            if (player.CanAcceptShieldCollision &&
                Entity.TryApplyShieldBump(
                player.ShieldCollisionBounds,
                player.ShieldCollisionBounds.GetCenter(),
                response.EnemyStrength))
            {
                player.ApplyShieldCollisionRecoil(
                    Entity.Position,
                    response.LinkInvincibilityFrames,
                    response.LinkKnockbackFrames);
                combatDescriptor.RequestSound(SoundId.SndBombLand);
            }
            return;
        }

        // enemyCheckCollisions checks the shield before the stun byte, then
        // suppresses ordinary Link contact while the enemy remains stunned.
        if (!Stunned) combatDescriptor.Combat.HandleLinkContact(player);
    }
    private bool ApplyNoKnockbackItemHit(Vector2 origin, int damage)
    {
        // collisionEffects.s:collisionEffect0b shares ENEMYDMG_0c across
        // item types: damage, JUST_HIT, $20 invincibility and zero recoil.
        if (!Entity.TakeDeferredNoKnockbackHit(origin, damage)) return false;
        Entity.DeferNativeHitStatus();
        combatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return true;
    }

    public virtual bool ApplyBiggoronSwordCollision(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)
    {
        var data=BiggoronSwordCollisionDatabase.Shared;
        if(!Entity.CollisionEnabled || Entity.InvincibilityCounter!=0 || Entity.NativeHitPending ||
            !data.EnemyEnabled(DimitriCollisionType) || !combatDescriptor.Combat.Intersects(bounds)) return false;
        int effect=data.Effect(DimitriCollisionMode);
        // Even effect$00 ends this target's scan before its Link contact.
        if(effect==CollisionEffect.None) return true;
        if (effect == CollisionEffect.SwordNoKnockback)
            return ApplyNoKnockbackItemHit(origin, damage);
        var strength = CollisionKnockback(ItemCollisionType.BiggoronSword, effect);
        bool accepted = ApplySwordHit(bounds, origin, damage, strength, spawns);
        // Common damaging rows publish JUST_HIT for their status owner.
        // Species-specific responses retain their own signal lifetime.
        if (accepted) Entity.DeferNativeHitStatus();
        return accepted;
    }
    public virtual bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns) =>
        (!_seedBurning || !FreezesDuringSeedBurn) &&
        combatDescriptor.Combat.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            knockbackStrength,
            spawns,
            deathPuffDecrementsRoomCount:
                combatDescriptor.CountsAsEnemy);
    public virtual SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (this is ISeedCollisionTarget native)
        {
            // ITEM24 keeps its damage byte while changing collisionType. Only
            // non-Mystery seeds can reconstruct that identity from the item ID.
            if (seedItem == ItemId.MysterySeed)
                throw new InvalidOperationException($"{combatDescriptor.Source?.Source}: ENEMY ${DimitriCollisionType:x2}:${combatDescriptor.Source?.SubId:x2} requires Mystery Seed's live collision type.");
            return new SeedSatchelDatabase().TryGet(seedItem, out var seed)
                ? native.ApplySeedCollision(hitbox, sourcePosition, seed,
                    seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect
                : SeedHitResult.None;
        }
        if (seedItem == ItemId.ScentSeed)
        {
            if (_seedBurning || !Entity.CollisionEnabled ||
                !combatDescriptor.Combat.Intersects(hitbox))
            {
                return SeedHitResult.None;
            }
            // ITEM_SCENT_SEED's standard collision-table entry is effect $08
            // with item damage $fe: two health units, normal knockback, then
            // the seed enters its non-attracting state-$03 effect.
            return combatDescriptor.Combat.ApplySwordHit(
                    hitbox,
                    sourcePosition,
                    2,
                    EnemyKnockbackStrength.Normal,
                    spawns,
                    deathPuffDecrementsRoomCount:
                        combatDescriptor.CountsAsEnemy)
                ? SeedHitResult.Activate
                : SeedHitResult.None;
        }
        return ApplySeedHit(hitbox, seedItem);
    }

    private SeedHitResult ApplySeedHit(Rect2 hitbox, int seedItem)
    {
        if (_seedBurning || !Entity.CollisionEnabled ||
            !combatDescriptor.Combat.Intersects(hitbox))
        {
            return SeedHitResult.None;
        }
        if (seedItem == ItemId.MysterySeed)
            return SeedHitResult.Activate;
        if (seedItem != ItemId.EmberSeed)
            return SeedHitResult.None;
        _seedBurning = true;
        return SeedHitResult.Ignite;
    }
    public virtual void CompleteSeedBurn(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_seedBurning)
            return;
        _seedBurning = false;
        combatDescriptor.Combat.ApplyBurnHit(
            2,
            spawns,
            deathPuffDecrementsRoomCount:
                combatDescriptor.CountsAsEnemy);
    }

    protected void CancelSeedBurn() =>
        _seedBurning = false;

    protected static Vector2 CollisionMidpoint(
        Vector2 enemyPosition,
        Vector2 itemPosition)
    {
        // collisionEffects.s:createClinkInteraction uses SUB/SRA/ADD on
        // coordinate high bytes, including signed subtraction and wrapping.
        return BoomerangCollisionResponse.Midpoint(enemyPosition.Floor(), itemPosition.Floor());
    }

    public virtual bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        if (!combatDescriptor.Combat.Finished || _completedOutcomeTaken)
        {
            outcome = default;
            return false;
        }

        _completedOutcomeTaken = true;
        outcome = GaleCaught ? RoomEnemyOutcome.SilentDeletion(combatDescriptor.CountsAsEnemy)
            : combatDescriptor.CompletedOutcome(Entity);
        return true;
    }

    public virtual void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.TakeCompletedKnockbackDeath() &&
            combatDescriptor.Combat.CreateDeathPuff() is { } deathPuff)
        {
            spawns.Add(deathPuff with
            {
                DecrementsRoomCount =
                    combatDescriptor.CountsAsEnemy
            });
        }
        if (Entity.TakeHazardEffect() is { } hazardEffect)
        {
            spawns.Add(hazardEffect.Hazard switch
            {
                HazardType.Water or HazardType.Lava =>
                    new EnemySplashSpawn(
                        hazardEffect.Position, hazardEffect.Hazard),
                HazardType.Hole =>
                    new FallingDownHoleSpawn(hazardEffect.Position),
                _ => throw new InvalidOperationException(
                    "A completed enemy hazard must be water, hole, or lava.")
            });
        }
        finished?.Invoke();
    }
}

internal enum SeedHitResult
{
    None,
    Ignite,
    Activate,
    ActivateRandomSeed,
    Consume,
    Bounce
}

internal abstract record RoomEntitySpawn(bool UpdateThisFrame = false);
