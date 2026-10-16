using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class VeranFairyRoomEntity : CombatEnemyRoomEntityAdapter<VeranFairyCharacter>,IFixedRoomEntity,
    INativeEnemySlotRoomEntity,IPostObjectLinkContactRoomEntity,IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity,ILinkSwordStateAwareRoomEntity,IExpertPunchHittableRoomEntity,
    ISeedCollisionTarget,IBoomerangCollisionRoomEntity,ISomariaBlockCollisionRoomEntity
{
    private int _sword=4;
    internal VeranFairyCharacter Actor=>Entity;
    internal int Slot {get;private set;}
    public bool MeleeReportsContact {get;private set;}
    public override int DimitriCollisionType=>6;
    public override int DimitriCollisionMode=>0x0d;
    internal VeranFairyRoomEntity(VeranFairyCharacter actor):base(actor,actor.SetTransitionDrawOffset,
        EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(()=>actor.IsDead,()=>actor.CollisionBounds,
            (_,_)=>false,_=>false,actor.OverlapsLink,()=>actor.Position,actor.Data.Actor(6).DamageQuarters,()=>null),true,0,
            ()=>RoomEnemyOutcome.SilentDeletion(false),actor.World.Entities.OnSoundRequested),collisionZ:()=>actor.ZFixed>>8) {}
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve)=>Slot=slot;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame);
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null)=>_sword=collision??SwordCollision.Type(state,level);
    private bool Eligible(int item,Rect2 bounds)=>Entity.CollisionEnabled&&!Entity.JustHit&&Entity.InvincibilityCounter==0&&
        Entity.Data.Active(false,6,item)&&RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    private bool Hit(int item,Rect2 bounds,int damage)
    {
        if(!Eligible(item,bounds))return false;
        int effect=Entity.Data.Effect(0x0d,item);MeleeReportsContact=effect!=0;
        switch(effect)
        {
            case 0:return true;
            case 0x1c:case 0x20:Entity.PublishHit();return true;
            case 0x21:Entity.PublishHit(damage,32);Entity.World.Entities.OnSoundRequested(SoundId.SndBossDamage);return true;
            default:throw new NotSupportedException($"veranFairy.s: ENEMY $06 item ${item:x2}, effect ${effect:x2}.");
        }
    }
    public override bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Hit(_sword,bounds,damage);
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit((int)item,bounds,damage);
    public bool ApplyExpertPunch(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit(11,bounds,damage);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit(7,bounds,damage);
    public override bool ApplySwitchHookHit(SwitchHookItem hook,Vector2 link)
    {
        if(!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,hook.ZHigh,7)||!Hit(13,hook.CollisionBounds,hook.HitDamage))return false;
        hook.NotifyObjectCollision();return true;
    }
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns)=>
        item.CollisionEnabled&&RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,item.ZHigh,7)&&Hit(0x17,item.CollisionBounds,item.Damage)?new(true,true):default;
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        if(!block.CollisionEnabled||!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,block.ZHigh,7)||!Eligible(0x15,block.CollisionBounds))return false;
        if(Entity.Data.Effect(0x0d,0x15)!=0x2d)throw new NotSupportedException("veranFairy.s: Somaria requires effect $2d.");
        block.Flags|=0x20;return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)=>
        throw new InvalidOperationException("ENEMY $06 requires the seed's live collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int item,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Hit(item,bounds,-(sbyte)seed.Damage))return default;
        int effect=Entity.Data.Effect(0x0d,item);
        return effect==0?new(true,SeedHitResult.None,false):new(true,
            seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,effect==0x20);
    }
    public override void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled||Entity.JustHit||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.IsUsingShield&&player.CanAcceptShieldCollision&&RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,player.ShieldCollisionBounds))
        {
            if(Entity.Data.Effect(0x0d,player.Inventory.ShieldLevel)!=5)throw new NotSupportedException("veranFairy.s: shield requires effect $05.");
            player.ApplyShieldCollisionRecoil(Entity.Position,8,11);Entity.PublishHit();Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);return;
        }
        if(player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ)&&player.ApplyEnemyContactDamage(Entity.Position,Entity.Data.Actor(6).DamageQuarters,
            RingDamageSource.Generic,34,15))Entity.PublishHit();
    }
}
