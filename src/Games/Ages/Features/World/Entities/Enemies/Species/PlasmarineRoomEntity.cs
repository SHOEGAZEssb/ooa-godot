using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class PlasmarineRoomEntity : CombatEnemyRoomEntityAdapter<PlasmarineCharacter>,IFixedRoomEntity,
    INativeEnemySlotRoomEntity,IScreenTransitionPreloadRoomEntity,IPlayerForcedMovement,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze,ILinkSwordStateAwareRoomEntity,
    IPostObjectMeleeCollisionRoomEntity,IItemCollisionHittableRoomEntity,ISeedCollisionTarget,
    IExpertPunchHittableRoomEntity,IBoomerangCollisionRoomEntity,ISomariaBlockCollisionRoomEntity
{
    private readonly PlasmarineEnvironment _world;
    private readonly PlasmarineProfile _data=PlasmarineProfile.Shared;
    private Player? _player;
    private int _sword=ItemCollisionType.L1Sword;
    public bool MeleeReportsContact {get;private set;}
    public override int DimitriCollisionMode=>Entity.CollisionMode;
    public override int DimitriCollisionType=>AgesEnemyId.Plasmarine;
    public bool UpdatesDuringDialogue=>Entity.InitializationPending;
    public bool UpdatesDuringRoomEntityFreeze=>Entity.InitializationPending;
    internal PlasmarineRoomEntity(PlasmarineCharacter actor,PlasmarineEnvironment world)
        :base(actor,actor.SetTransitionDrawOffset,EnemyCombatDescriptor.Special(
            EnemyCombatComponent.WithContactDamage(()=>actor.IsDead,()=>actor.CollisionBounds,(_,_)=>false,_=>false,
                actor.OverlapsLink,()=>actor.Position,actor.Record.DamageQuarters,()=>null),true,0,
            completedOutcome:()=>RoomEnemyOutcome.BossTeardown(0),soundRequested:world.Sound),collisionZ:()=>actor.ZFixed>>8)=>_world=world;
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve)=>Entity.Slot=slot;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        _player=frame.Player;
        // plasmarine_stateA captures hEnemyTargetY/X, sampled before ENEMY;
        // the independent Scent target and later Link writes do not replace it.
        Entity.UpdateFrame(frame.LinkTarget??frame.Player.EnemyContactPosition,frame.Counter);
    }
    public void UpdatePlayerForcedMovement(Player player)=>_world.Entry.Update(player);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {Entity.InitializeNative();return ScreenTransitionPresentation.Visible;}
    public void SetLinkSwordState(SwordActionState state,int level,int? itemCollisionType=null)=>_sword=itemCollisionType??SwordCollision.Type(state,level);
    private bool Eligible(int item,Rect2 bounds)=>Entity.CollisionEnabled && !Entity.NativeHitPending && Entity.InvincibilityCounter==0 &&
        _data.Active(item) && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    private bool Hit(int item,Rect2 bounds)
    {
        MeleeReportsContact=false;if(!Eligible(item,bounds))return false;
        int effect=_data.Effect(Entity.CollisionMode,item);MeleeReportsContact=effect!=0;
        switch(effect)
        {
            case CollisionEffect.None:return true;
            case CollisionEffect.Effect26:Entity.PublishCollision(item,unchecked((sbyte)0xe4));return true;
            case CollisionEffect.Effect1c:case CollisionEffect.Effect20:Entity.PublishCollision(item);return true;
            case CollisionEffect.ElectricShock:
                Entity.PublishShock();(_player??throw new InvalidOperationException("plasmarine.s: shock requires live Link.")).ApplyElectricShock(Entity.Position);return true;
            default:throw new NotSupportedException($"plasmarine.s mode${Entity.CollisionMode:x2}: item${item:x2}, effect${effect:x2} unsupported.");
        }
    }
    public override bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Hit(_sword,bounds);
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit((int)item,bounds);
    public bool ApplyExpertPunch(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit(ItemCollisionType.ExpertPunch,bounds);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit(ItemCollisionType.BiggoronSword,bounds);
    public override BombchuTarget BombchuTarget=>new(AgesEnemyId.Plasmarine,Entity.Visible,Entity.Position,Entity.CollisionBounds,Entity.Health);
    public override BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns)=>
        item.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,item.ZHigh,7) && Hit(ItemCollisionType.L1Boomerang,item.CollisionBounds)
            ? new(true,_data.Effect(Entity.CollisionMode,ItemCollisionType.L1Boomerang)!=0):default;
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        if(!block.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,block.ZHigh,7) ||
            !Eligible(ItemCollisionType.SomariaBlock,block.CollisionBounds))return false;
        if(_data.Effect(Entity.CollisionMode,ItemCollisionType.SomariaBlock)!=CollisionEffect.Effect2d)
            throw new NotSupportedException("plasmarine.s: Somaria requires effect$2d.");
        block.Flags|=0x20;return true;
    }
    protected override bool TryApplySwitchHookEffect(int effect,SwitchHookItem hook,Vector2 position)
    {if(effect!=CollisionEffect.ElectricShock)return false;return Hit(ItemCollisionType.SwitchHook,hook.CollisionBounds);}
    public override SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)=>
        throw new InvalidOperationException("Plasmarine requires the live seed collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int item,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Hit(item,bounds))return default;
        int effect=_data.Effect(Entity.CollisionMode,item);
        return effect==0?new(true,SeedHitResult.None,false):
            new(true,seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,effect==CollisionEffect.Effect20);
    }
    public override void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled || Entity.NativeHitPending || !player.EnemyContactHeightOverlaps(CollisionZ))return;
        _player=player;
        int shield=player.Inventory.ShieldLevel;
        if(player.IsUsingShield && shield is >=1 and <=3 && player.CanAcceptShieldCollision &&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,player.ShieldCollisionBounds))
        {
            int effect=_data.Effect(Entity.CollisionMode,shield);
            if(effect==CollisionEffect.ElectricShock) {Hit(shield,player.ShieldCollisionBounds);return;}
            var (inv,recoil)=effect switch {CollisionEffect.Effect05=>(8,11),CollisionEffect.Effect06=>(15,19),
                _=>throw new NotSupportedException($"plasmarine.s: shield effect${effect:x2} unsupported.")};
            player.ApplyShieldCollisionRecoil(Entity.Position,inv,recoil);Entity.PublishCollision(shield);return;
        }
        if(!player.NativeObjectVulnerable || !player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ))return;
        if(Entity.CollisionMode==0x68) {Entity.PublishShock();player.ApplyElectricShock(Entity.Position);}
        else if(player.ApplyEnemyContactDamage(Entity.Position,Entity.ContactDamageQuarters))Entity.PublishCollision(ItemCollisionType.Link);
    }
}
