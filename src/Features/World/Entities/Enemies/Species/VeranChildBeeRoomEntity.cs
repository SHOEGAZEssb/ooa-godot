using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class VeranChildBeeRoomEntity : CombatEnemyRoomEntityAdapter<VeranChildBeeCharacter>,IFixedRoomEntity,
    IPostObjectLinkContactRoomEntity,IPostObjectMeleeCollisionRoomEntity,IPostObjectItemCollisionRoomEntity,
    ILinkSwordStateAwareRoomEntity,ISomariaBlockCollisionRoomEntity,IExpertPunchHittableRoomEntity,
    ISeedCollisionTarget,IBurningEnemyTarget,IBoomerangCollisionRoomEntity,IShovelHittableRoomEntity
{
    private readonly VeranFinalDatabase _data;
    private readonly RoomEntityManager _owner;
    protected override bool Stunned=>Entity.StunCounter!=0;
    public bool MeleeReportsContact=>true;
    internal VeranChildBeeCharacter Actor=>Entity;
    internal VeranChildBeeRoomEntity(VeranChildBeeCharacter actor,RoomEntityManager owner,VeranFinalDatabase data)
        :base(actor,actor.SetTransitionDrawOffset,EnemyCombatDescriptor.WithContactDamage(
            new(0x1f,actor.SubId,0x9f,0,0,EnemyHandlerKind.Keese,"veranFinalForm.s:veranFinal_beeForm_stateA",0x10,0x0f,0x0f),
            actor,data.Actor(0x1f).DamageQuarters,actor.TakeSwordHit,actor.TakeBurnHit,actor.ApplySwordKnockback,
            owner.OnSoundRequested,EnemySwordResponse.Knockback,deathPuffAllowed:()=>!actor.FlewOut,
            completedOutcome:()=>actor.FlewOut?RoomEnemyOutcome.RoomCountDecrement():RoomEnemyOutcome.EnemyDie(0),
            deathPuffZHigh:()=>actor.GaleCollisionDisabled?actor.GaleDrawZ:actor.BurnZFixed>>8),collisionZ:()=>actor.BurnZFixed>>8)
    {_data=data;_owner=owner;}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame);
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null)=>_sword=collision??SwordCollision.Type(state,level);
    private int _sword=4;
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit((int)item,bounds,origin,damage,spawns);
    public override bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Hit(_sword,bounds,origin,damage,spawns);
    public override bool ApplyBiggoronSwordCollision(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit(7,bounds,origin,damage,spawns);
    public bool ApplyExpertPunch(Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit(11,bounds,origin,damage,spawns);
    public bool ApplyShovelHit(Rect2 bounds,Vector2 origin)=>RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,0,7)&&
        Hit(ItemCollisionType.Shovel,bounds,origin,0,System.Array.Empty<RoomEntitySpawn>());
    private bool Hit(int item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Entity.CollisionEnabled||Entity.NativeHitPending||Entity.InvincibilityCounter!=0||!_data.Active(false,0x1f,item)||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds))return false;
        int effect=_data.Effect(0x1f,item);
        if(effect==0)return true;
        if(effect==CollisionEffect.Bump){Entity.ApplyItemBump(origin);return true;}
        var strength=effect switch {8=>EnemyKnockbackStrength.Low,9=>EnemyKnockbackStrength.Normal,10=>EnemyKnockbackStrength.High,
            _=>throw new System.NotSupportedException($"veranChildBee.s: ENEMY $1f, item ${item:x2}, effect ${effect:x2}.")};
        return CombatDescriptor.Combat.ApplyDamageAfterCollision(origin,damage,strength,spawns,true);
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)=>
        ApplySomariaBlockCollision(block,-_data.Actor(0x1f).DamageQuarters*2,Entity.NativeHitPending,spawns);
    protected override bool TryApplySwitchHookEffect(int effect,SwitchHookItem hook,Vector2 linkPosition)
    {
        if(effect!=CollisionEffect.SwordLowKnockback||!Entity.TakeSwordHit(hook.Position,hook.HitDamage))return false;
        Entity.ApplySwordKnockback(hook.Position,EnemyKnockbackStrength.Low);hook.NotifyObjectCollision();
        CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)=>
        throw new System.InvalidOperationException("ENEMY $1f requires the seed's live collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        if(GaleCaught||!Entity.CollisionEnabled||Entity.NativeHitPending||Entity.InvincibilityCounter!=0||!_data.Active(false,0x1f,collision)||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds))return default;
        int effect=_data.Effect(0x1f,collision);
        switch(effect)
        {
            case CollisionEffect.None:return new(true,SeedHitResult.None,false);
            case CollisionEffect.Effect20:Entity.DeferNativeHitStatus();break;
            case CollisionEffect.Burn:
                Entity.BeginEmberHit(-(sbyte)seed.Damage);
                if(_owner.PartSlotAvailable)spawns.Add(new BurningEnemySpawn(this));break;
            case CollisionEffect.PegasusSeed:
                Entity.BeginPegasusHit();CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);break;
            case CollisionEffect.GaleSeed:
                Entity.BeginGaleHit();BeginNativeGale(origin,()=>_owner.Random.Next().Value);break;
            case CollisionEffect.SwordLowKnockback:case CollisionEffect.Sword:
                Hit(collision,bounds,origin,-(sbyte)seed.Damage,spawns);break;
            default:throw new System.NotSupportedException($"veranChildBee.s: ENEMY $1f seed column ${collision:x2}, effect ${effect:x2}.");
        }
        return new(true,seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,
            effect is not (CollisionEffect.SwordLowKnockback or CollisionEffect.Sword));
    }
    public int BurnTargetId=>0x1f;
    public int BurnZFixed {get=>Entity.BurnZFixed;set=>Entity.BurnZFixed=value;}
    public override void ReleaseBurn()=>Entity.ReleaseBurn();
    public override void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled||Entity.NativeHitPending||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.IsUsingShield)
        {
            int previous=Entity.InvincibilityCounter;base.HandleLinkContact(player);
            if(previous!=Entity.InvincibilityCounter)Entity.DeferNativeHitStatus();
        }
        else if(!Stunned&&player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ)&&
            player.ApplyEnemyContactDamage(Entity.Position,_data.Actor(0x1f).DamageQuarters))Entity.DeferNativeHitStatus();
    }
    public override void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if(Entity.FlewOut)return; // enemyDelete after explicit decNumEnemies, no puff/drop.
        if(Entity.NativeDeath&&CombatDescriptor.Combat.CreateDeathPuff() is {} puff)
            spawns.Add(puff with {DecrementsRoomCount=CombatDescriptor.CountsAsEnemy});
        base.OnFinished(spawns);
    }
}
