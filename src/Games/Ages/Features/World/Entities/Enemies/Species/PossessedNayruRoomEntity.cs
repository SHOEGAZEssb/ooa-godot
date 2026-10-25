using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class PossessedNayruRoomEntity : CombatEnemyRoomEntityAdapter<PossessedNayruCharacter>, IFixedRoomEntity,
    INativeEnemySlotRoomEntity, ILinkSwordStateAwareRoomEntity, IItemCollisionHittableRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity, IPostObjectLinkContactRoomEntity, ISeedCollisionTarget, IBoomerangCollisionRoomEntity,
    IScreenTransitionPreloadRoomEntity
{
    private int _swordCollision=4;
    internal PossessedNayruCharacter Actor => Entity;
    internal int Slot { get; private set; }
    public bool MeleeReportsContact { get; private set; }
    public override int DimitriCollisionType => 0x61;
    public override int DimitriCollisionMode => Entity.CollisionMode;
    public override BombchuTarget BombchuTarget => new(0x61,Entity.Visible,Entity.Position,Entity.CollisionBounds,Entity.Health);
    internal PossessedNayruRoomEntity(PossessedNayruCharacter actor) : base(actor,actor.SetTransitionDrawOffset,
        EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(()=>actor.IsDead,()=>actor.CollisionBounds,
            (_,_)=>false,_=>false,actor.OverlapsLink,()=>actor.Position,4,()=>null),actor.SubId<=1,0,
            ()=>RoomEnemyOutcome.SilentDeletion(false),actor.RequestSound),collisionZ:()=>actor.ZFixed>>8) { }
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve) => Slot=slot;
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null) => _swordCollision=collision??SwordCollision.Type(state,level);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame,spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeStateZero();
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public override bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns) =>
        Hit(_swordCollision,bounds,origin,damage);
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns) =>
        Hit((int)item,bounds,origin,damage);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns) => Hit(7,bounds,origin,damage);
    private bool Eligible(int item,Rect2 bounds) => Entity.CollisionEnabled && !Entity.JustHit && Entity.InvincibilityCounter==0 &&
        Entity.Data.ActiveCollision(item) && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    private bool Hit(int item,Rect2 bounds,Vector2 origin,int damage)
    {
        if (!Eligible(item,bounds)) return false;
        int effect=Entity.Data.Collision(Entity.CollisionMode,item); MeleeReportsContact=effect!=0;
        switch (effect)
        {
            case 0: return true;
            case 0x1c: Entity.MarkContact(item,origin); return true;
            case 0x30: Entity.MarkContact(item,origin,knockback:true); return true;
            case 0x21: Entity.MarkContact(item,origin,32,knockback:true,damage:damage); Entity.RequestSound(SoundId.SndBossDamage); return true;
            case 0x20: Entity.MarkContact(item,origin); return true;
            case 0x2d: return true;
            default: throw new NotSupportedException($"veranPossessionBoss.s: mode${Entity.CollisionMode:x2}/item${item:x2}/effect${effect:x2}.");
        }
    }
    public override bool ApplySwitchHookHit(SwitchHookItem hook,Vector2 link)
    {
        if (!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,0,7) || !Eligible(13,hook.CollisionBounds)) return false;
        if (Entity.Data.Collision(Entity.CollisionMode,13)==0x2e)
        { Entity.BeginSwitchHook(link); hook.LatchEnemy(Entity); return true; }
        Hit(13,hook.CollisionBounds,link,hook.HitDamage); hook.NotifyObjectCollision(); return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("ENEMY $61 requires the live imported seed collision type.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        if (!Eligible(collision,bounds)) return default;
        int effect=Entity.Data.Collision(Entity.CollisionMode,collision);
        if (effect==0) return new(true,SeedHitResult.None,false);
        int actual=seed.SeedItem==ItemId.MysterySeed && Entity.SubId<=1 ? 0x18 : collision;
        // func_07_47b7 rewrites Mystery's identity only after table dispatch.
        Entity.MarkContact(actual,origin,effect==0x21?32:0,knockback:effect is 0x21 or 0x20,
            damage:effect==0x21?-unchecked((sbyte)seed.Damage):0);
        if (effect==0x21) Entity.RequestSound(SoundId.SndBossDamage);
        if (effect is not (0x20 or 0x21 or 0x1c))
            throw new NotSupportedException($"ENEMY $61 seed effect${effect:x2}.");
        return new(true,seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,effect==0x20);
    }
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns)
    {
        if (!item.CollisionEnabled || !Hit(0x17,item.CollisionBounds,item.Position,0)) return default;
        return new(true,Entity.Data.Collision(Entity.CollisionMode,0x17)!=0);
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.JustHit || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        if (player.IsUsingShield && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,player.ShieldCollisionBounds))
        {
            if (!player.CanAcceptShieldCollision) return;
            int item=player.Inventory.ShieldLevel, effect=Entity.Data.Collision(Entity.CollisionMode,item);
            if (effect==0) return;
            var (inv,recoil)=effect switch { 5=>(8,11),6=>(15,19),7=>(22,25),_=>throw new NotSupportedException($"ENEMY$61 shield effect${effect:x2}.") };
            player.ApplyShieldCollisionRecoil(Entity.Position,inv,recoil); Entity.MarkContact(item,player.Position); return;
        }
        if (player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ))
        { player.ApplyEnemyContactDamage(Entity.Position,4); Entity.MarkContact(0,player.Position); }
    }
}
