using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TwinrovaMinionRoomEntity : CombatEnemyRoomEntityAdapter<TwinrovaMinionCharacter>,IFixedRoomEntity,
    INativeEnemySlotRoomEntity,IPostObjectLinkContactRoomEntity,IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity,ILinkSwordStateAwareRoomEntity,IExpertPunchHittableRoomEntity,
    ISeedCollisionTarget,IBoomerangCollisionRoomEntity,ISomariaBlockCollisionRoomEntity
{
    private int _sword=4;
    internal TwinrovaMinionCharacter Actor=>Entity;
    internal int Slot {get;private set;}
    public bool MeleeReportsContact {get;private set;}
    public override int DimitriCollisionType=>Entity.Id;
    public override int DimitriCollisionMode=>Entity.Id==0x5d?0x0e:0x41;
    public override BombchuTarget BombchuTarget=>new(Entity.Id,Entity.Visible,Entity.Position,Entity.CollisionBounds,Entity.Health);
    internal TwinrovaMinionRoomEntity(TwinrovaMinionCharacter actor):base(actor,actor.SetTransitionDrawOffset,
        EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(()=>actor.IsDead,()=>actor.CollisionBounds,
            (_,_)=>false,_=>false,actor.OverlapsLink,()=>actor.Position,actor.World.Data.Actor(actor.Id).DamageQuarters,()=>null),false,0,
            ()=>actor.Killed?RoomEnemyOutcome.EnemyDieUncounted():RoomEnemyOutcome.SilentDeletion(false),actor.World.Entities.OnSoundRequested),collisionZ:()=>actor.ZFixed>>8) {}
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve){Slot=slot;Entity.Bind(resolve);}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame,spawns);
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null)=>_sword=collision??SwordCollision.Type(state,level);
    private bool Eligible(int item,Rect2 bounds)=>Entity.CollisionEnabled&&!Entity.JustHit&&Entity.InvincibilityCounter==0&&
        Entity.World.Data.Active(false,DimitriCollisionType,item)&&RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    private bool Hit(int item,Rect2 bounds,Vector2 origin,int damage)
    {
        MeleeReportsContact=false;if(!Eligible(item,bounds))return false;
        int effect=Entity.World.Data.Effect(DimitriCollisionMode,item);MeleeReportsContact=effect!=0;
        switch(effect)
        {
            case 0:return true;
            case 0x1c:case 0x20:Entity.PublishHit(item);return true;
            case 5:case 6:case 7:Entity.PublishHit(item);Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);return true;
            case 0x0b:Entity.PublishHit(item,damage,32);Entity.World.Entities.OnSoundRequested(SoundId.SndDamageEnemy);return true;
            case 0x2f:Entity.PublishHit(item,damage,21,11,origin);Entity.World.Entities.OnSoundRequested(SoundId.SndDamageEnemy);return true;
            case 0x1b:
                Entity.PublishHit(item,0,-20);Entity.World.Entities.TryCreateBoomerangClink(CollisionMidpoint(Entity.Position,origin),0);return true;
            default:throw new NotSupportedException($"twinrovaIce.s/twinrovaBat.s: ENEMY ${Entity.Id:x2}, mode ${DimitriCollisionMode:x2}, item ${item:x2}, effect ${effect:x2}.");
        }
    }
    public override bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Hit(_sword,bounds,origin,damage);
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit((int)item,bounds,origin,damage);
    public bool ApplyExpertPunch(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit(11,bounds,origin,damage);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit(7,bounds,origin,damage);
    public override bool ApplyBombCollision(IBombExplosionRoomEntity bomb,ICollection<RoomEntitySpawn> spawns)=>bomb.CollisionEnabled&&
        RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,bomb.CollisionZ,bomb.CollisionZRadius)&&Hit(0x18,bomb.CollisionBounds,bomb.CollisionBounds.GetCenter(),bomb.Damage);
    public override bool ApplySwitchHookHit(SwitchHookItem hook,Vector2 link)
    {
        if(!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,hook.ZHigh,7)||!Hit(13,hook.CollisionBounds,hook.Position,hook.HitDamage))return false;
        hook.NotifyObjectCollision();return true;
    }
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns)=>
        item.CollisionEnabled&&RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,item.ZHigh,7)&&Hit(0x17,item.CollisionBounds,item.Position,item.Damage)?new(true,MeleeReportsContact):default;
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        if(!block.CollisionEnabled||!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,block.ZHigh,7)||!Eligible(0x15,block.CollisionBounds))return false;
        if(Entity.Id==0x5e)
            return ApplySomariaBlockCollision(block,-2*Entity.World.Data.Actor(Entity.Id).DamageQuarters,Entity.JustHit,spawns,deferNativeStatus:false);
        if(Entity.World.Data.Effect(DimitriCollisionMode,0x15)!=0x2d)throw new NotSupportedException("twinrovaIce.s/twinrovaBat.s: Somaria requires effect $2d.");
        block.Flags|=0x20;return true;
    }
    protected override void ApplySomariaEnemyDamage(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PublishHit(0x15,-block.Damage,21,11,block.Position);
        Entity.World.Entities.OnSoundRequested(SoundId.SndDamageEnemy);
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)=>
        throw new InvalidOperationException($"ENEMY ${Entity.Id:x2} requires the seed's live collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int item,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Hit(item,bounds,origin,-(sbyte)seed.Damage))return default;
        int effect=Entity.World.Data.Effect(DimitriCollisionMode,item);
        return effect==0?new(true,SeedHitResult.None,false):new(true,
            seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,effect==0x20);
    }
    public override void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled||Entity.JustHit||Entity.InvincibilityCounter!=0||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.CanAcceptShieldCollision&&Entity.World.Data.Active(false,DimitriCollisionType,player.Inventory.ShieldLevel)&&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,player.ShieldCollisionBounds))
        {
            int effect=Entity.World.Data.Effect(DimitriCollisionMode,player.Inventory.ShieldLevel);
            if(effect==CollisionEffect.None)return;
            var recoil=effect switch
            {
                CollisionEffect.Effect05=>(Invincibility:8,Knockback:11),
                CollisionEffect.Effect06=>(Invincibility:15,Knockback:19),
                CollisionEffect.Effect07=>(Invincibility:22,Knockback:25),
                _=>throw new NotSupportedException($"twinrovaIce.s/twinrovaBat.s: shield column ${player.Inventory.ShieldLevel:x2}, mode ${DimitriCollisionMode:x2}, effect ${effect:x2}.")
            };
            player.ApplyShieldCollisionRecoil(Entity.Position,recoil.Invincibility,recoil.Knockback);Entity.PublishHit(player.Inventory.ShieldLevel);
            Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);return;
        }
        if(player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ)&&player.ApplyEnemyContactDamage(Entity.Position,Entity.World.Data.Actor(Entity.Id).DamageQuarters,
            RingDamageSource.Generic,34,15))Entity.PublishHit(0);
    }
}
