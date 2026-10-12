using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class AnglerFishRoomEntity : CombatEnemyRoomEntityAdapter<AnglerFishCharacter>,IFixedRoomEntity,
    INativeEnemySlotRoomEntity,INativeEnemyCounter1RoomEntity,IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze,IPlayerForcedMovement,
    ILinkSwordStateAwareRoomEntity,IPostObjectMeleeCollisionRoomEntity,IItemCollisionHittableRoomEntity,
    ISeedCollisionTarget,IExpertPunchHittableRoomEntity,ISomariaBlockCollisionRoomEntity,IBoomerangCollisionRoomEntity
{
    private readonly AnglerFishEnvironment _world;
    private readonly AnglerFishProfile _data=AnglerFishProfile.Shared;
    private int _sword=ItemCollisionType.L1Sword;
    public bool MeleeReportsContact {get;private set;}
    public override bool CountsAsEnemy=>Entity.Counted;
    public override int DimitriCollisionMode=>Entity.CollisionMode;
    public override int DimitriCollisionType=>Entity.Record.Id;
    public bool UpdatesDuringDialogue=>Entity.InitializationPending;
    public bool UpdatesDuringRoomEntityFreeze=>Entity.InitializationPending;
    public int Counter1 {get=>Entity.Counter1;set=>Entity.Counter1=value;}
    public bool RetainsCounter1AfterDeletion=>false;
    internal AnglerFishRoomEntity(AnglerFishCharacter actor,AnglerFishEnvironment world)
        :base(actor,actor.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(
                ()=>actor.IsDead,()=>actor.CollisionBounds,(_,_)=>false,_=>false,actor.OverlapsLink,
                ()=>actor.Position,actor.Record.DamageQuarters,()=>null),actor.Counted,0,
                completedOutcome:()=>actor.BossExploded ? RoomEnemyOutcome.BossTeardown(0) : RoomEnemyOutcome.SilentDeletion(actor.Counted),
                soundRequested:world.Sound))=>_world=world;
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve)=>Entity.Slot=slot;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>
        Entity.UpdateFrame(frame.Player,frame.ScentSeedTarget??frame.Player.Position,frame.Counter);
    public void UpdatePlayerForcedMovement(Player player)
    {if(!Entity.IsBubble && !Entity.IsAntenna && !Finished)_world.Entry.Update(player);}
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {Entity.InitializeNative(true);return ScreenTransitionPresentation.Hidden;}
    public void SetLinkSwordState(SwordActionState state,int level,int? itemCollisionType=null)=>_sword=itemCollisionType??SwordCollision.Type(state,level);
    private bool Eligible(int item,Rect2 bounds)=>Entity.CollisionEnabled && !Entity.NativeHitPending && Entity.InvincibilityCounter==0 &&
        _data.Active(Entity.IsBubble,item) && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    private bool Hit(int item,Rect2 bounds,Vector2 origin,int damage)
    {
        MeleeReportsContact=false;if(!Eligible(item,bounds))return false;
        int effect=_data.Effect(Entity.CollisionMode,item);MeleeReportsContact=effect!=0;
        switch(effect)
        {
            case CollisionEffect.None:return true;
            case CollisionEffect.Effect21:Entity.PublishCollision(item,damage,32);_world.Sound(SoundId.SndBossDamage);return true;
            case CollisionEffect.Effect1c:Entity.PublishCollision(item);return true;
            case CollisionEffect.Effect20:Entity.PublishCollision(item);return true;
            default:throw new NotSupportedException($"anglerFish.s mode${Entity.CollisionMode:x2}: item${item:x2} effect${effect:x2} unsupported.");
        }
    }
    public override bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Hit(_sword,bounds,origin,damage);
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit((int)item,bounds,origin,damage);
    public bool ApplyExpertPunch(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit(ItemCollisionType.ExpertPunch,bounds,origin,damage);
    public override BombchuTarget BombchuTarget=>new(Entity.Record.Id,Entity.Visible,Entity.Position,Entity.CollisionBounds,Entity.Health);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>
        Hit(ItemCollisionType.BiggoronSword,bounds,origin,damage);
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns)=>
        item.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(0,item.ZHigh,7) &&
        Hit(ItemCollisionType.L1Boomerang,item.CollisionBounds,item.Position,item.Damage)
            ? new(true,_data.Effect(Entity.CollisionMode,ItemCollisionType.L1Boomerang)!=0) : default;
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        if(!block.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(0,block.ZHigh,7) ||
            !Eligible(ItemCollisionType.SomariaBlock,block.CollisionBounds))return false;
        int effect=_data.Effect(Entity.CollisionMode,ItemCollisionType.SomariaBlock);
        if(effect==CollisionEffect.Effect2d)block.Flags|=0x20;
        else if(effect==CollisionEffect.Effect2f)return ApplySomariaBlockCollision(block,unchecked((byte)(-2*Entity.Record.DamageQuarters)),Entity.NativeHitPending,spawns);
        else if(effect!=0)throw new NotSupportedException($"anglerFish.s: Somaria effect${effect:x2} unsupported.");
        return true;
    }
    protected override void ApplySomariaEnemyDamage(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)=>
        Entity.PublishSomariaHit(block.Position,-block.Damage);
    public override bool ApplySwitchHookHit(SwitchHookItem hook,Vector2 linkPosition)
    {
        if(!hook.CollisionEnabled || !Hit(ItemCollisionType.SwitchHook,hook.CollisionBounds,hook.Position,hook.HitDamage))return false;
        if(_data.Effect(Entity.CollisionMode,ItemCollisionType.SwitchHook)!=0)hook.NotifyObjectCollision();return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)=>
        throw new InvalidOperationException("Angler Fish requires the live seed collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int item,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Hit(item,bounds,origin,-(sbyte)seed.Damage))return default;
        int effect=_data.Effect(Entity.CollisionMode,item);
        return effect==0 ? new(true,SeedHitResult.None,false) :
            new(true,seed.SeedItem==ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,effect==CollisionEffect.Effect20);
    }
    public override void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled || Entity.NativeHitPending || !player.EnemyContactHeightOverlaps(0))return;
        int shield=Math.Clamp(player.Inventory.ShieldLevel,1,3);
        if(player.IsUsingShield && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,player.ShieldCollisionBounds))
        {
            if(!player.CanAcceptShieldCollision)return;
            var (inv,recoil)=_data.Effect(Entity.CollisionMode,shield) switch
            {
                CollisionEffect.Effect05=>(8,11),CollisionEffect.Effect06=>(15,19),
                _=>throw new NotSupportedException($"anglerFish.s: shield mode${Entity.CollisionMode:x2} unsupported.")
            };
            player.ApplyShieldCollisionRecoil(Entity.Position,inv,recoil);Entity.PublishCollision(shield);return;
        }
        if(player.NativeObjectVulnerable && player.OverlapsEnemyCollision(Entity.CollisionBounds) &&
            player.ApplyEnemyContactDamage(Entity.Position,Entity.Record.DamageQuarters))Entity.PublishCollision(ItemCollisionType.Link);
    }
}
