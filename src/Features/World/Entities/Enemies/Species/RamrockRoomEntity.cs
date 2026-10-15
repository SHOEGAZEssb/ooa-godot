using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RamrockRoomEntity : CombatEnemyRoomEntityAdapter<RamrockCharacter>,
    IFixedRoomEntity, INativeEnemySlotRoomEntity, IPlayerForcedMovement,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    ILinkSwordStateAwareRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity, IExpertPunchHittableRoomEntity,
    ISeedCollisionTarget, ISomariaBlockCollisionRoomEntity, IPostObjectLinkContactRoomEntity
{
    private readonly RamrockEnvironment _world;
    private readonly RamrockProfile _data = RamrockProfile.Shared;
    private int _sword = ItemCollisionType.L1Sword;
    public bool MeleeReportsContact { get; private set; }
    public override int DimitriCollisionType => Entity.Record.Id;
    public override int DimitriCollisionMode => Entity.CollisionMode;
    public override bool CountsAsEnemy => !Finished;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    internal RamrockRoomEntity(RamrockCharacter actor, RamrockEnvironment world, int killableIndex)
        : base(actor, actor.SetTransitionDrawOffset, EnemyCombatDescriptor.Special(
            EnemyCombatComponent.WithContactDamage(() => actor.IsDead, () => actor.CollisionBounds,
                (_, _) => false, _ => false, actor.OverlapsLink, () => actor.Position, actor.Record.DamageQuarters, () => null),
            true, killableIndex, completedOutcome: () => actor.BossExploded
                ? RoomEnemyOutcome.BossTeardown(killableIndex) : RoomEnemyOutcome.SilentDeletion(true), soundRequested: world.Sound),
            collisionZ: () => actor.ZFixed >> 8) { _world = world; }
    public void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve) => Entity.Slot = slot;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame.Player.EnemyContactPosition, frame.Counter);
    public void UpdatePlayerForcedMovement(Player player) { if (Entity.Record.Id == EnemyId.Ramrock && !Finished) _world.Entry.Update(player); }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Entity.InitializeNative(); return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden; }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) { if (Entity.State == 0) Entity.InitializeNative(); }
    public void SetLinkSwordState(SwordActionState state, int level, int? itemCollisionType = null) => _sword = itemCollisionType ?? SwordCollision.Type(state, level);
    private bool Eligible(int item, Rect2 bounds) => Entity.CollisionEnabled && !Entity.HitPending && Entity.InvincibilityCounter == 0 &&
        _data.Active(Entity.Record.Id, item) && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, bounds);
    private bool Hit(int item, Rect2 bounds, Vector2 origin, ICollection<RoomEntitySpawn> spawns)
    {
        MeleeReportsContact = false; if (!Eligible(item, bounds)) return false;
        int effect = _data.Effect(Entity.CollisionMode, item); MeleeReportsContact = effect != 0;
        switch (effect)
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect1b:
                Entity.PublishCollision(item, -20); spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position, origin))); return true;
            case CollisionEffect.Effect1c:
            case CollisionEffect.Effect20: Entity.PublishCollision(item); return true;
            default: throw new NotSupportedException($"ramrock.s/ramrockArms.s: collision mode${Entity.CollisionMode:x2}/item${item:x2}/effect${effect:x2}.");
        }
    }
    public override bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) => Hit(_sword, bounds, origin, spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision item, Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit((int)item, bounds, origin, spawns);
    public bool ApplyExpertPunch(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit(ItemCollisionType.ExpertPunch, bounds, origin, spawns);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds, Vector2 origin, int damage, ICollection<RoomEntitySpawn> spawns) => Hit(ItemCollisionType.BiggoronSword, bounds, origin, spawns);
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item, ICollection<RoomEntitySpawn> spawns)
    {
        if (!item.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, item.ZHigh, 7) ||
            !Hit(ItemCollisionType.L1Boomerang, item.CollisionBounds, item.Position, spawns)) return default;
        return new(true, _data.Effect(Entity.CollisionMode, ItemCollisionType.L1Boomerang) != 0);
    }
    public override BombchuTarget BombchuTarget => new(Entity.Record.Id, Entity.Visible, Entity.Position, Entity.CollisionBounds, Entity.Health);
    public override bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition)
    {
        if (!hook.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, hook.ZHigh, 7) || !Eligible(ItemCollisionType.SwitchHook, hook.CollisionBounds)) return false;
        Entity.PublishCollision(ItemCollisionType.SwitchHook, -20); hook.NotifyObjectCollision(CollisionMidpoint(Entity.Position, hook.Position)); return true;
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block, ICollection<RoomEntitySpawn> spawns)
    {
        if (!block.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, block.ZHigh, 7) || !Eligible(ItemCollisionType.SomariaBlock, block.CollisionBounds)) return false;
        if (_data.Effect(Entity.CollisionMode, ItemCollisionType.SomariaBlock) != CollisionEffect.Effect2d)
            throw new NotSupportedException("Ramrock Somaria collision requires effect$2d.");
        Entity.PublishCollision(ItemCollisionType.SomariaBlock); block.Flags |= 0x20; return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds, Vector2 origin, int item, ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("Ramrock requires the live seed collision type.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds, Vector2 origin, SeedRecord seed, int item, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Hit(item, bounds, origin, spawns)) return default;
        int effect = _data.Effect(Entity.CollisionMode, item);
        return effect == 0 ? new(true, SeedHitResult.None, false) : new(true,
            seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate, true);
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.HitPending || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        int shield = player.Inventory.ShieldLevel;
        if (player.IsUsingShield && shield is >= 1 and <= 3 && player.CanAcceptShieldCollision &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds, player.ShieldCollisionBounds))
        {
            int effect = _data.Effect(Entity.CollisionMode, shield);
            var (inv, recoil) = effect switch {
                CollisionEffect.Effect05 => (8,11), CollisionEffect.Effect06 => (15,19), CollisionEffect.Effect07 => (22,25),
                _ => throw new NotSupportedException($"Ramrock shield effect${effect:x2}.") };
            player.ApplyShieldCollisionRecoil(Entity.Position, inv, recoil); Entity.PublishCollision(shield); _world.Sound(SoundId.SndBombLand); return;
        }
        if (player.NativeObjectVulnerable && player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ) &&
            player.ApplyEnemyContactDamage(Entity.Position, Entity.Record.DamageQuarters, RingDamageSource.Generic, 34, 15))
            Entity.PublishCollision(ItemCollisionType.Link);
    }
}
