using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class OctogonRoomEntity(OctogonCharacter actor,OctogonRoomEnvironment world)
    : RoomEntityAdapter<OctogonCharacter>(actor,actor.SetTransitionDrawOffset), IFixedRoomEntity,
        IRoomEntityLifetime, IRoomEnemyCounterEntity, IRoomEnemyOutcomeSource, INativeEnemySlotRoomEntity,
        ISwordHittableRoomEntity, IPostObjectMeleeCollisionRoomEntity, ILinkSwordStateAwareRoomEntity,
        IPostObjectItemCollisionRoomEntity, IObjectCollisionHeightRoomEntity, IPostObjectLinkContactRoomEntity,
        ISeedCollisionTarget, IBoomerangCollisionRoomEntity, ISwitchHookHittableRoomEntity,
        ISomariaBlockCollisionRoomEntity, IExpertPunchHittableRoomEntity, IBombCollisionRoomEntity, IPlayerForcedMovement,
        IScreenTransitionPreloadRoomEntity, ISwordAttackerKnockbackRoomEntity, IBossShutterState
{
    private readonly OctogonBehaviorProfile _data = OctogonBehaviorProfile.Shared;
    private bool _outcomeTaken;
    private int _sword = ItemCollisionType.L1Sword;
    private bool _armoredHit;
    public bool Finished => Entity.IsDead;
    public bool CountsAsEnemy => !Entity.ShellForm && !Finished;
    public bool BossIntroReady => Entity.State >= 8;
    public int CollisionZ => Entity.ZFixed>>8;
    public bool MeleeReportsContact { get; private set; }
    public bool HasBombCollisionIdentity => true;
    private IReadOnlyList<int> Effects => Entity.ShellForm ? _data.ShellEffects : _data.BodyEffects;
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve) => Entity.Slot = slot;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame);
    public void UpdatePlayerForcedMovement(Player player) { if (!Entity.ShellForm && !Finished) world.Entry.Update(player); }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) => PrepareForScreenTransition(null,spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeNative(true,player?.Position ?? Vector2.Zero,player?.Position ?? Vector2.Zero);
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        outcome = default; if (!Finished || _outcomeTaken) return false; _outcomeTaken = true;
        outcome = Entity.ShellForm ? RoomEnemyOutcome.SilentDeletion(false) : RoomEnemyOutcome.BossTeardown(0); return true;
    }
    public void SetLinkSwordState(SwordActionState state,int level,int? itemCollisionType = null) => _sword = itemCollisionType ?? SwordCollision.Type(state,level);
    private bool Overlaps(int collision,Rect2 bounds,bool inv = true) => Entity.CollisionEnabled && !Entity.NativeHitPending &&
        (!inv || Entity.InvincibilityCounter == 0) && _data.Active[collision] != 0 && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    private bool Hit(int collision,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)
    {
        MeleeReportsContact = false; _armoredHit = false;
        if (!Overlaps(collision,bounds)) return false;
        switch (Effects[collision])
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect21:
                if (!Entity.Damage(origin,damage)) return false; MeleeReportsContact = true; world.Sound(SoundId.SndBossDamage); return true;
            case CollisionEffect.Effect1b:
                if (!Entity.Deflect()) return false; MeleeReportsContact = true; _armoredHit = true;
                spawns.Add(new EnemyClinkSpawn(BoomerangCollisionResponse.Midpoint(Entity.Position,bounds.GetCenter()))); world.Sound(SoundId.SndBombLand); return true;
            case CollisionEffect.Effect1c: Entity.PublishCollision(); MeleeReportsContact = true; return true;
            case CollisionEffect.Effect20: Entity.SeedCollision(origin); return true;
            default: throw new NotSupportedException($"octogon.s mode${(Entity.ShellForm ? 0x67 : 0x4e):x2} collision${collision:x2} effect${Effects[collision]:x2}.");
        }
    }
    public bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns) => Hit(_sword,bounds,origin,damage,spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision collision,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns) => Hit((int)collision,bounds,origin,damage,spawns);
    public bool ApplyExpertPunch(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns) => Hit(ItemCollisionType.ExpertPunch,bounds,origin,damage,spawns);
    public bool ApplyBombCollision(IBombExplosionRoomEntity bomb,ICollection<RoomEntitySpawn> spawns) =>
        bomb.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,bomb.CollisionZ,bomb.CollisionZRadius) &&
        Hit(ItemCollisionType.Bomb,bomb.CollisionBounds,bomb.CollisionBounds.GetCenter(),bomb.Damage,spawns);
    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength,out SwordAttackerKnockback response)
    {
        var profile = EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
        int frames = !_armoredHit ? 0 : strength == EnemyKnockbackStrength.High ? profile.HighFrames : strength == EnemyKnockbackStrength.Low ? profile.LowFrames : profile.NormalFrames;
        _armoredHit = false; response = new(Entity.Position,frames); return frames != 0;
    }
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns) =>
        item.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,item.ZHigh,7) && Hit(ItemCollisionType.L1Boomerang,item.CollisionBounds,item.Position,item.Damage,spawns)
            ? new(true,Effects[ItemCollisionType.L1Boomerang] != 0) : default;
    public bool ApplySwitchHookHit(SwitchHookItem hook,Vector2 linkPosition)
    {
        if (!hook.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,hook.ZHigh,7) || !Hit(ItemCollisionType.SwitchHook,hook.CollisionBounds,linkPosition,hook.HitDamage,[])) return false;
        if (Effects[ItemCollisionType.SwitchHook] != 0) hook.NotifyObjectCollision(); return true;
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        if (!block.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,block.ZHigh,7) || !Overlaps(ItemCollisionType.SomariaBlock,block.CollisionBounds)) return false;
        if (Effects[ItemCollisionType.SomariaBlock] == CollisionEffect.Effect2d) block.Flags |= 0x20;
        else if (Effects[ItemCollisionType.SomariaBlock] != 0) throw new NotSupportedException("octogon.s: Somaria effect changed.");
        return true;
    }
    public void HandleLinkContact(Player player)
    {
        if (!player.EnemyContactHeightOverlaps(CollisionZ)) return;
        int shield = Math.Clamp(player.Inventory.ShieldLevel,1,3);
        if (player.IsUsingShield && Overlaps(shield,player.ShieldCollisionBounds,false))
        {
            if (player.CanAcceptShieldCollision)
            { player.ApplyShieldCollisionRecoil(Entity.Position,15,19); Entity.PublishCollision(); world.Sound(SoundId.SndBombLand); }
            return;
        }
        if (player.NativeObjectVulnerable && Overlaps(ItemCollisionType.Link,new(player.EnemyContactPosition-new Vector2(6,6),new(12,12)),false))
            if (player.ApplyEnemyContactDamage(Entity.Position,Entity.Record.DamageQuarters)) Entity.PublishCollision();
    }
    public SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)
    {
        if (item == ItemId.MysterySeed) throw new InvalidOperationException("Octogon requires Mystery's live collision type.");
        return new SeedSatchelDatabase().TryGet(item,out var seed) ? ApplySeedCollision(bounds,origin,seed,seed.Collision&31,spawns).Effect : SeedHitResult.None;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        int damage = seed.SeedItem is ItemId.EmberSeed or ItemId.ScentSeed ? 2 : 0;
        if (!Hit(collision,bounds,origin,damage,spawns)) return default;
        return Effects[collision] == 0 ? new(true,SeedHitResult.None,false) : new(true,seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,true);
    }
}
