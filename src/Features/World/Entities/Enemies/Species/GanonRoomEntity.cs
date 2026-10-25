using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class GanonRoomEntity : CombatEnemyRoomEntityAdapter<GanonCharacter>,IFixedRoomEntity,
    INativeEnemySlotRoomEntity,IPostObjectLinkContactRoomEntity,IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity,ILinkSwordStateAwareRoomEntity,IExpertPunchHittableRoomEntity,
    ISeedCollisionTarget,IBoomerangCollisionRoomEntity,ISomariaBlockCollisionRoomEntity,ISwordAttackerKnockbackRoomEntity
{
    private int _sword=4,_attackerRecoil;
    internal GanonCharacter Actor=>Entity;
    internal int Slot {get;private set;}
    public bool MeleeReportsContact {get;private set;}
    public override int DimitriCollisionType=>Entity.CollisionType&127;
    public override int DimitriCollisionMode=>0x0b;
    public override BombchuTarget BombchuTarget=>new(4,Entity.Visible,Entity.Position,Entity.CollisionBounds,Entity.Health);
    internal GanonRoomEntity(GanonCharacter actor):base(actor,actor.SetTransitionDrawOffset,
        EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(()=>actor.IsDead,()=>actor.CollisionBounds,
            (_,_)=>false,_=>false,actor.OverlapsLink,()=>actor.Position,actor.World.Data.Actor(4).DamageQuarters,()=>null),true,0,
            ()=>RoomEnemyOutcome.SilentDeletion(false),actor.World.Entities.OnSoundRequested),collisionZ:()=>actor.ZFixed>>8) {}
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve){Slot=slot;Entity.Slot=slot;}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame);
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null)=>_sword=collision??SwordCollision.Type(state,level);
    private bool Eligible(int item,Rect2 bounds)=>Entity.CollisionEnabled&&!Entity.JustHit&&Entity.InvincibilityCounter==0&&
        Entity.World.Data.Active(false,DimitriCollisionType,item)&&RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    private bool Hit(int item,Rect2 bounds,Vector2 origin,int damage)
    {
        _attackerRecoil=0;MeleeReportsContact=false;if(!Eligible(item,bounds))return false;
        int effect=Entity.World.Data.Effect(0x0b,item);MeleeReportsContact=effect!=0;
        switch(effect)
        {
            case 0:return true;
            case 0x1c:case 0x20:Entity.PublishHit(item);return true;
            case 0x15:case 0x16:
                _attackerRecoil=effect==0x15?EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback.LowFrames:EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback.NormalFrames;
                Entity.PublishHit(item,0,-28);Entity.World.Entities.TryCreateBoomerangClink(CollisionMidpoint(Entity.Position,origin),0);
                Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);return true;
            case 0x1b:Entity.PublishHit(item,0,-20);Entity.World.Entities.TryCreateBoomerangClink(CollisionMidpoint(Entity.Position,origin),0);return true;
            case 0x2c:
                _attackerRecoil=EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback.NormalFrames;
                Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);goto case 0x21;
            case 0x21:Entity.PublishHit(item,damage,32);Entity.World.Entities.OnSoundRequested(SoundId.SndBossDamage);return true;
            default:throw new NotSupportedException($"ganon.s: ENEMY $04 form ${Entity.State:x2}, mode ${0x0b:x2}, item ${item:x2}, effect ${effect:x2}.");
        }
    }
    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength,out SwordAttackerKnockback response)
    {response=new(Entity.Position,_attackerRecoil);bool hit=_attackerRecoil!=0;_attackerRecoil=0;return hit;}
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
        if(Entity.World.Data.Effect(0x0b,0x15)!=0x2d)throw new NotSupportedException("ganon.s: Somaria requires effect $2d.");
        block.Flags|=0x20;return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)=>
        throw new InvalidOperationException("ENEMY $04 requires the seed's live collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int item,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Hit(item,bounds,origin,-(sbyte)seed.Damage))return default;
        int effect=Entity.World.Data.Effect(0x0b,item);
        return effect==0?new(true,SeedHitResult.None,false):new(true,
            seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,effect==0x20);
    }
    public override void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled||Entity.JustHit||Entity.InvincibilityCounter!=0||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.CanAcceptShieldCollision&&Entity.World.Data.Active(false,DimitriCollisionType,player.Inventory.ShieldLevel)&&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,player.ShieldCollisionBounds))
        {
            int effect=Entity.World.Data.Effect(0x0b,player.Inventory.ShieldLevel);
            if(effect==CollisionEffect.None)return;
            var recoil=effect switch
            {
                CollisionEffect.Effect05=>(Invincibility:8,Knockback:11),
                CollisionEffect.Effect06=>(Invincibility:15,Knockback:19),
                CollisionEffect.Effect07=>(Invincibility:22,Knockback:25),
                _=>throw new NotSupportedException($"ganon.s: shield column ${player.Inventory.ShieldLevel:x2}, mode ${0x0b:x2}, effect ${effect:x2}.")
            };
            player.ApplyShieldCollisionRecoil(Entity.Position,recoil.Invincibility,recoil.Knockback);Entity.PublishHit(player.Inventory.ShieldLevel);
            Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);return;
        }
        if(player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ)&&player.ApplyEnemyContactDamage(Entity.Position,Entity.World.Data.Actor(4).DamageQuarters,
            RingDamageSource.Generic,34,15))Entity.PublishHit(0);
    }
}
