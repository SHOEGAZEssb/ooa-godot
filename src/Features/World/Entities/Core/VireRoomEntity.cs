using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class VireRoomEntity(VireCharacter actor,VireRoomEnvironment world)
    : RoomEntityAdapter<VireCharacter>(actor,actor.SetTransitionDrawOffset), IFixedRoomEntity,
        IRoomEntityLifetime, IRoomEnemyCounterEntity, IRoomEnemyOutcomeSource, INativeEnemySlotRoomEntity,
        ISwordHittableRoomEntity, IPostObjectMeleeCollisionRoomEntity, ILinkSwordStateAwareRoomEntity,
        IPostObjectItemCollisionRoomEntity, IObjectCollisionHeightRoomEntity, IPostObjectLinkContactRoomEntity,
        ISeedCollisionTarget, IBoomerangCollisionRoomEntity, ISwitchHookHittableRoomEntity,
        ISomariaBlockCollisionRoomEntity, IExpertPunchHittableRoomEntity, IPlayerRestriction, IPlayerForcedMovement,
        IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly VireBehaviorProfile _data = EnemyBehaviorTables.Shared.Vire;
    private bool _outcomeTaken;
    private int _swordCollision = ItemCollisionType.L1Sword;
    public bool Finished => Entity.IsDead;
    public bool CountsAsEnemy => Entity.MainForm && !Finished;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public bool FreezesPlayerUpdates => Entity.FreezesLink;
    public bool DisablesSword => false;
    public int CollisionZ => Entity.ZFixed >> 8;
    public bool MeleeReportsContact { get; private set; }
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve) => Entity.Slot = slot;
    public void UpdatePlayerForcedMovement(Player player) { if (Entity.MainForm && !Finished) world.Entry.Update(player); }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Entity.InitializeNative(true); return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden; }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame);
    public bool TryTakeEnemyOutcome(out RoomEnemyOutcome outcome)
    {
        outcome = default;
        if (!Finished || _outcomeTaken) return false;
        _outcomeTaken = true;
        outcome = Entity.MainForm ? RoomEnemyOutcome.BossTeardown(0) : RoomEnemyOutcome.SilentDeletion(false);
        return true;
    }
    public void SetLinkSwordState(SwordActionState state,int level,int? itemCollisionType = null) =>
        _swordCollision = itemCollisionType ?? SwordCollision.Type(state,level);
    private bool Overlaps(int collision,Rect2 bounds,bool checkInvincibility = true) => Entity.CollisionEnabled &&
        !Entity.NativeHitPending && (!checkInvincibility || Entity.InvincibilityCounter == 0) &&
        _data.ActiveCollisions[collision].Value != 0 && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    private bool Hit(int collision,Rect2 bounds,Vector2 origin,int damage)
    {
        MeleeReportsContact = false;
        if (!Overlaps(collision,bounds)) return false;
        switch (_data.Effects[collision].Value)
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect21:
                if (!Entity.Damage(origin,damage)) return false;
                MeleeReportsContact = true; world.Sound(SoundId.SndBossDamage); return true;
            case CollisionEffect.Effect1c:
                Entity.PublishCollision(); MeleeReportsContact = true; return true;
            case CollisionEffect.Effect20:
                Entity.SeedCollision(origin); return true;
            default: throw new NotSupportedException($"vire.s: ENEMY$75 mode$46 collision${collision:x2} effect${_data.Effects[collision].Value:x2}.");
        }
    }
    public bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns) =>
        Hit(_swordCollision,bounds,origin,damage);
    public bool ApplyItemCollision(RoomEntityItemCollision collision,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns) =>
        Hit((int)collision,bounds,origin,damage);
    public bool ApplyExpertPunch(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns) =>
        Hit(ItemCollisionType.ExpertPunch,bounds,origin,damage);
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns) =>
        item.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,item.ZHigh,7) &&
        Hit(ItemCollisionType.L1Boomerang,item.CollisionBounds,item.Position,0) ? new(true,_data.Effects[ItemCollisionType.L1Boomerang].Value != 0) : default;
    public bool ApplySwitchHookHit(SwitchHookItem hook,Vector2 linkPosition)
    {
        if (!hook.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,hook.ZHigh,7) ||
            !Hit(ItemCollisionType.SwitchHook,hook.CollisionBounds,linkPosition,hook.HitDamage)) return false;
        if (_data.Effects[ItemCollisionType.SwitchHook].Value != 0) hook.NotifyObjectCollision();
        return true;
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        if (!block.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,block.ZHigh,7) ||
            !Overlaps(ItemCollisionType.SomariaBlock,block.CollisionBounds)) return false;
        switch (_data.Effects[ItemCollisionType.SomariaBlock].Value)
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect2d: block.Flags |= 0x20; return true;
            default: throw new NotSupportedException("vire.s: mode$46 Somaria response changed.");
        }
    }
    public void HandleLinkContact(Player player)
    {
        if (player.IsUsingShield && player.EnemyContactHeightOverlaps(CollisionZ) &&
            Overlaps(Math.Clamp(player.Inventory.ShieldLevel,1,3),player.ShieldCollisionBounds,checkInvincibility:false))
        {
            if (player.CanAcceptShieldCollision)
            {
                // Only Mirror Shield is enabled: effect$07 publishes enemy
                // JUST_HIT and LINKDMG_18's negative invincibility/recoil.
                if (_data.Effects[3].Value != CollisionEffect.Effect07)
                    throw new NotSupportedException("vire.s: mode$46 Mirror Shield response changed.");
                player.ApplyShieldCollisionRecoil(Entity.Position,0x16,0x19);
                Entity.PublishCollision(); world.Sound(SoundId.SndBombLand);
            }
            return;
        }
        if (!player.NativeObjectVulnerable || !player.EnemyContactHeightOverlaps(CollisionZ) ||
            !Overlaps(ItemCollisionType.Link,new(player.EnemyContactPosition - new Vector2(6,6),new(12,12)),checkInvincibility:false)) return;
        // Mode$46 Link effect$03 has no enemy-side response. Its ENEMYDMG_08
        // damages Link with the ordinary raw damage/inv25/recoil7 profile.
        player.ApplyEnemyContactDamage(Entity.Position,Entity.Record.DamageQuarters);
    }
    public SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int seedItem,ICollection<RoomEntitySpawn> spawns)
    {
        if (seedItem == ItemId.MysterySeed) throw new InvalidOperationException("Vire requires Mystery's live collision type.");
        if (!new SeedSatchelDatabase().TryGet(seedItem,out var seed)) return SeedHitResult.None;
        return ApplySeedCollision(bounds,origin,seed,seed.Collision & ObjectCollisionFlags.TypeMask,spawns).Effect;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        if (!Hit(collision,bounds,origin,0)) return default;
        if (_data.Effects[collision].Value == 0) return new(true,SeedHitResult.None,false);
        return new(true,seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,true);
    }
}
