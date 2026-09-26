using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Native slot/lifecycle adapter for the linked miniboss and its ball.
internal sealed class SmasherRoomEntity(SmasherCharacter actor, SmasherRoomEnvironment world, bool placed)
    : RoomEntityAdapter<SmasherCharacter>(actor, actor.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity,
        IRoomEnemyOutcomeSource, INativeEnemySlotRoomEntity,
        ISwordHittableRoomEntity, IPostObjectMeleeCollisionRoomEntity, ILinkSwordStateAwareRoomEntity,
        IPostObjectItemCollisionRoomEntity, IExpertPunchHittableRoomEntity, IObjectCollisionHeightRoomEntity,
        ISeedCollisionTarget, IPostObjectLinkContactRoomEntity, ISwitchHookHittableRoomEntity,
        ISomariaBlockCollisionRoomEntity, IBoomerangCollisionRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        INativeBraceletRoomEntity, IBraceletChildRoomEntity, IReservedBraceletCollisionRoomEntity,
        INativeEnemyCounter1RoomEntity, IScreenTransitionPreloadRoomEntity, IPlayerForcedMovement,
        IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        // _updateEnemiesIfStateIsZero dispatches uninitialized enemies on
        // every scroll update, including allocation retries. State8 freezes.
        if (Entity.State == 0)
            Entity.UpdateInitializationFrame(frame.Counter, InitializeBossRoom, () => world.SpawnParent(this), world.InteractionSlotAvailable(), world.CreateInitializationPuff);
    }
    public int Counter1 { get => Entity.Counter1; set => Entity.Counter1 = value; }
    public bool RetainsCounter1AfterDeletion => false;
    public void UpdatePlayerForcedMovement(Player player)
    { if (placed && !Finished) world.Entry?.Update(player); }
    private void InitializeBossRoom()
    {
        world.InitializeBossRoom();
        world.Entry?.Arm();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.State == 0)
            Entity.UpdateInitializationFrame(world.FrameCounter?.Invoke() ?? 0, InitializeBossRoom, () => world.SpawnParent(this), world.InteractionSlotAvailable(), world.CreateInitializationPuff);
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    private Action<INativeBraceletRoomEntity>? _publishGrabbable;
    private Player? _holder;
    private SmasherBraceletThrow? _throw;
    private static readonly Lazy<BraceletGrabGeometry> GrabGeometry = new(() => new());
    private static readonly Lazy<BraceletWeightDatabase> Weights = new(() => new());
    private static readonly Lazy<BraceletDatabaseRecord> Bracelet = new(() => new BraceletDatabase().Data);
    public bool TryGetReservedBraceletCollision(out ReservedBraceletCollision collision)
    {
        collision = default;
        if (_throw is not { Active: true }) return false;
        // bracelet.s @notTile copies the enemy's radii, enables item collision
        // $16, and keeps the item's own coordinates throughout the throw.
        Vector2 size = Entity.CollisionBounds.Size;
        collision = new(new(_throw.Position.Floor() - size / 2, size),
            _throw.ZFixed >> 8, Bracelet.Value.CollisionZRadius, Bracelet.Value.Damage);
        return true;
    }
    public bool ReservedBraceletChildActive => _throw?.Active == true;
    internal SmasherBraceletThrow? ReservedThrow => _throw;
    public void BindGrabbablePublisher(Action<INativeBraceletRoomEntity> publish)
    {
        _publishGrabbable = publish;
        Entity.TreeExiting += DropHeldObject;
    }
    private void DropHeldObject()
    {
        if (_holder is null) return;
        Entity.DropGrab();
        _holder.EndCarriedObjectPose();
        _holder = null;
    }
    public bool TryUseBracelet(Player player, Vector2I releaseDirection)
    {
        if (_holder == player)
        {
            if (_throw is null) throw new InvalidOperationException("$74 release precedes the grabbed enemy's weight initialization.");
            int direction = CarriedObjectMotion.DirectionIndex(player.FacingVector);
            _throw.Begin(direction, releaseDirection == Vector2I.Zero ? 0xff :
                CarriedObjectMotion.DirectionIndex(releaseDirection) * 8, RingEffects.UsesStrongThrow(player.Inventory));
            player.EndCarriedObjectPose();
            _holder = null;
            return true;
        }
        if (!Entity.IsBall || Entity.IsDead || Entity.State is not (9 or 10) ||
            player.IsCarryingObject || !player.IsOnGroundForBracelet) return false;
        if (!GrabGeometry.Value.Overlaps(new(player.EnemyContactPosition - new Vector2(6,6), new(12,12)),
            player.EnemyContactZ, CarriedObjectMotion.DirectionIndex(player.FacingVector),
            Entity.CollisionBounds, CollisionZ, Entity.PendingCollision)) return false;
        Entity.BeginGrab();
        _holder = player;
        player.BeginCarriedObjectPose();
        return true;
    }
    public void UpdateBraceletChild(Player player)
    {
        if (_holder is not null && !_holder.IsCarryingObject) DropHeldObject();
        _throw?.Update(world.Sound);
    }
    public void UpdateHeldPosition(Player player)
    {
        if (_holder != player) return;
        if (!player.IsCarryingObject || Entity.IsDead || Entity.State != 2 || Entity.GrabSubstate >= 2)
        { DropHeldObject(); return; }
        if (player.BraceletEntityOffset is Vector2I offset)
            Entity.CopyCarriedPosition(new((byte)((int)player.Position.X + offset.X), (byte)(int)player.Position.Y),
                unchecked((sbyte)(player.EnemyContactZ + offset.Y)));
        else
            _throw?.Hold(player.Position, player.EnemyContactZ, player.CarriedObjectAnimationFrame == 0 ? 2 : 3,
                CarriedObjectMotion.DirectionIndex(player.FacingVector));
    }
    private bool _marked;
    private bool _outcomeTaken;
    private int _swordCollision = ItemCollisionType.L1Sword;
    private readonly SmasherBehaviorProfile _data = EnemyBehaviorTables.Shared.Smasher;
    public int CollisionZ => Entity.ZFixed >> 8;
    public bool MeleeReportsContact { get; private set; }
    internal SmasherCharacter Character => Entity;
    public bool Finished => Entity.IsDead;
    public bool CountsAsEnemy => world.Counted && !Finished &&
        (Entity.NativeSubId == 1 || placed && Entity.State == 0 && Entity.NativeSubId == 0);
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve) => Entity.BindNativeSlot(slot);

    public void SetLinkSwordState(SwordActionState state, int level) =>
        _swordCollision = SwordCollision.Type(state, level);
    private bool Overlaps(int collision, Rect2 bounds) => Entity.CollisionEnabled && !Entity.PendingCollision &&
        Entity.InvincibilityCounter == 0 && _data.ActiveCollisions[collision].Value != 0 &&
        RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    private int Effect(int collision) => (Entity.CollisionMode == EnemyCollisionMode.SmasherBall ? _data.BallEffects : _data.ParentEffects)[collision].Value;
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns)
    {
        if (!item.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, item.ZHigh, 7) ||
            !Overlaps(ItemCollisionType.L1Boomerang, item.CollisionBounds)) return default;
        int effect = Effect(ItemCollisionType.L1Boomerang);
        if (effect == CollisionEffect.None) return new(true, false);
        if (effect != CollisionEffect.Effect1c) throw new NotSupportedException($"ENEMY_SMASHER $74 mode${Entity.CollisionMode:x2}: boomerang effect${effect:x2}.");
        Entity.PublishCollision();
        return new(true, true);
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!block.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, block.ZHigh, 7) ||
            !Overlaps(ItemCollisionType.SomariaBlock, block.CollisionBounds)) return false;
        int effect = Effect(ItemCollisionType.SomariaBlock);
        switch (effect)
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect2d:
                // Both Smasher modes ($45/$63) destroy Somaria via var2f.
                // This does not hit/retract the boss or reset its ball timer.
                block.Flags |= 0x20;
                return true;
            default: throw new NotSupportedException($"ENEMY_SMASHER $74 mode ${Entity.CollisionMode:x2}: Somaria effect ${effect:x2} is not represented.");
        }
    }
    public bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition)
    {
        if (!hook.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,hook.ZHigh,7) ||
            !Overlaps(ItemCollisionType.SwitchHook,hook.CollisionBounds)) return false;
        int effect = Effect(ItemCollisionType.SwitchHook);
        if (effect == CollisionEffect.None) return true;
        if (effect != CollisionEffect.Effect1c)
            throw new NotSupportedException($"ENEMY_SMASHER $74 mode ${Entity.CollisionMode:x2}: Switch Hook effect ${effect:x2} is not represented.");
        // collisionEffect1c writes JUST_HIT to both objects. The hook's next
        // item update retracts; Smasher receives no damage, stun or recoil.
        Entity.PublishCollision();
        hook.NotifyObjectCollision();
        return true;
    }
    private bool ApplyWeapon(int collision, Rect2 bounds)
    {
        MeleeReportsContact = false;
        if (!Overlaps(collision, bounds)) return false;
        switch (Effect(collision))
        {
            case CollisionEffect.None: return true; // Ends this enemy's item scan without a contact flag.
            case CollisionEffect.Effect1c:
                Entity.PublishCollision();
                MeleeReportsContact = true;
                return true; // ENEMYDMG_1c only writes var2a; no HP/recoil/invincibility.
            case CollisionEffect.Effect20: return true; // Consumed projectile; no enemy-side status.
            default: throw new NotSupportedException($"ENEMY_SMASHER $74 mode ${Entity.CollisionMode:x2}, weapon ${collision:x2}: effect ${Effect(collision):x2} is not represented.");
        }
    }
    public bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength,
        ICollection<RoomEntitySpawn> spawns) => ApplyWeapon(_swordCollision, bounds);
    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 bounds, Vector2 origin, int damage,
        ICollection<RoomEntitySpawn> spawns) => ApplyWeapon((int)collision, bounds);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => ApplyWeapon(ItemCollisionType.ExpertPunch, bounds);
    public SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int seedItem, ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("ENEMY_SMASHER $74 requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem, out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds, origin, seed, seed.Collision & ObjectCollisionFlags.TypeMask, spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int collision,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Overlaps(collision, bounds)) return default;
        int effect = Effect(collision);
        if (effect == CollisionEffect.None) return new(true, SeedHitResult.None, false);
        if (effect != CollisionEffect.Effect20) throw new NotSupportedException($"ENEMY_SMASHER $74 seed ${collision:x2}: effect ${effect:x2} is not represented.");
        return new(true, seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.State == 0)
        {
            Entity.UpdateInitializationFrame(frame.Counter, InitializeBossRoom, () => world.SpawnParent(this), world.InteractionSlotAvailable(), world.CreateInitializationPuff);
            return;
        }
        bool Puff(Vector2 position)
        {
            if (!world.InteractionSlotAvailable()) return false;
            spawns.Add(new PuzzlePuffSpawn(position.Floor(), SoundId.SndPoof, ZHigh: Entity.ZFixed >> 8));
            return true;
        }
        bool Explosion(Vector2 position)
        {
            if (!world.PartSlotAvailable()) return false;
            spawns.Add(new BossDeathExplosionSpawn(position.Floor(), EnemyId.Smasher, Entity.ZFixed >> 8));
            return true;
        }
        void Drop()
        {
            Entity.DropGrab();
            frame.Player.EndCarriedObjectPose();
            _holder = null;
        }
        Entity.UpdateNormalFrame(frame.Player.EnemyContactPosition, frame.Counter, Puff, world.BeginMiniboss, world.Sound,
            setReservedItemAngle: angle =>
            {
                if (_throw is null) throw new InvalidOperationException("$74 thrown enemy has no reserved bracelet item.");
                _throw.SetAngle(angle);
            },
            setLinkGrabState: weight =>
            {
                if (weight != 0x20 || _holder != frame.Player)
                    throw new InvalidOperationException("$74 grabbed state requires Link and weight $20.");
                _throw = new(Entity, Entity.Room, Weights.Value, new BombDatabase().Data);
            },
            forceDrop: Drop,
            handleDeath: () => Entity.UpdateDeathFrame(Puff, Explosion, world.DisableLinkCollisionsAndMenu,
                () => _marked = true, world.RestoreRoomMusic, world.Sound, Drop),
            groundBallContact: () =>
            {
                _publishGrabbable?.Invoke(this);
                // objectPushLinkAwayOnCollision checks XY only, independently
                // of collision enable, height and Link's post-object masks.
                if (Player.EnemyCollisionOverlaps(frame.Player.EnemyContactPosition, Entity.CollisionBounds))
                    frame.Player.AdvanceInteractionVelocity(0x28,
                        OracleObjectMovement.Shared.RelativeAngle(Entity.Position, frame.Player.EnemyContactPosition));
            });
    }

    public void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.PendingCollision ||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, player.EnemyContactZ, 7)) return;
        int shield = Math.Clamp(player.Inventory.ShieldLevel, 1, 3);
        if (player.IsUsingShield && _data.ActiveCollisions[shield].Value != 0 &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            if (!player.CanAcceptShieldCollision) return;
            if (Effect(shield) != CollisionEffect.Effect05) throw new NotSupportedException($"ENEMY_SMASHER $74 shield effect ${Effect(shield):x2} is not represented.");
            player.ApplyShieldCollisionRecoil(Entity.Position, 8, 11); // LINKDMG_10, ENEMYDMG_1c.
            Entity.PublishCollision();
            world.Sound(SoundId.SndBombLand);
            return;
        }
        if (Player.EnemyCollisionOverlaps(player.EnemyContactPosition, Entity.CollisionBounds) &&
            player.ApplyEnemyContactDamage(Entity.Position, Entity.DamageQuarters, RingDamageSource.Generic, 34, 15))
            Entity.PublishCollision(); // Effect$02: LINKDMG_04 and ENEMYDMG_1c.
    }

    public bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        outcome = default;
        if (!_marked || _outcomeTaken) return false;
        _outcomeTaken = true;
        outcome = RoomEnemyOutcome.BossTeardown(world.KillableEnemyIndex);
        return true;
    }
}
