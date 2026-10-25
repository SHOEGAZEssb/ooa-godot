using System.Collections.Generic;

namespace Oracles;

internal sealed class VeranSpiderRoomEntity : CombatEnemyRoomEntityAdapter<VeranSpiderCharacter>, IFixedRoomEntity,
    IScreenTransitionPreloadRoomEntity, IPostObjectLinkContactRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    IBoomerangCollisionRoomEntity, ISeedCollisionTarget, IBurningEnemyTarget,
    IPostObjectItemCollisionRoomEntity, IExpertPunchHittableRoomEntity, ISomariaBlockCollisionRoomEntity,
    ILinkSwordStateAwareRoomEntity
{
    private readonly int _damage;
    private readonly PalacePossessionDatabase _data;
    private readonly RoomEntityManager _owner;
    private int _swordCollision=ItemCollisionType.L1Sword;
    internal VeranSpiderCharacter Actor => Entity;
    public bool MeleeReportsContact => true;
    protected override bool Stunned=>Entity.StunCounter!=0;
    internal VeranSpiderRoomEntity(VeranSpiderCharacter actor,PalacePossessionDatabase data,RoomEntityManager owner)
        : base(actor,actor.SetTransitionDrawOffset,EnemyCombatDescriptor.WithContactDamage(
            new(0x0f,1,0x10,0,0,EnemyHandlerKind.None,"veranPossessionBoss.s:veranPossessionBoss_ambi_stateD",0x10,0x0f,0x0f),
            actor,data.Spider.DamageQuarters,actor.TakeSwordHit,actor.TakeBurnHit,actor.ApplySwordKnockback,
            owner.OnSoundRequested,EnemySwordResponse.Knockback,
            deathPuffZHigh:()=>actor.GaleCollisionDisabled?actor.GaleDrawZ:actor.ZFixed>>8),collisionZ:()=>actor.ZFixed>>8)
    { _damage=data.Spider.DamageQuarters; _data=data; _owner=owner; }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame);
    }
    // enemyCode0f overrides every status, including state5, when cfd0
    // becomes nonzero. Dispatch through its normal enemy slot so enemyDie's
    // PART allocation precedes the same-update PART walk.
    public override bool GaleCaught=>base.GaleCaught&&!Entity.CutsceneTriggered;
    public override void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if(Entity.NativeDeath&&CombatDescriptor.Combat.CreateDeathPuff() is {} puff)
            spawns.Add(puff with {DecrementsRoomCount=CombatDescriptor.CountsAsEnemy});
        base.OnFinished(spawns);
    }
    public override SeedHitResult ApplySeedHit(Godot.Rect2 bounds,Godot.Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)=>
        throw new System.InvalidOperationException("ENEMY $0f requires the seed's live collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Godot.Rect2 bounds,Godot.Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        if(GaleCaught||!Entity.CollisionEnabled||Entity.NativeHitPending||Entity.InvincibilityCounter!=0||
            !_data.SpiderActiveCollision(collision)||!RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds))return default;
        int effect=_data.Collision(0x10,collision);
        switch(effect)
        {
            case CollisionEffect.None:return new(true,SeedHitResult.None,false);
            case CollisionEffect.Effect20:Entity.DeferNativeHitStatus();break;
            case CollisionEffect.Burn:
                Entity.BeginEmberHit(-(sbyte)seed.Damage);
                if(_owner.PartSlotAvailable)spawns.Add(new BurningEnemySpawn(this));
                break;
            case CollisionEffect.PegasusSeed:
                Entity.BeginPegasusHit();CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);break;
            case CollisionEffect.GaleSeed:
                Entity.BeginGaleHit();BeginNativeGale(origin,()=>_owner.Random.Next().Value);break;
            case CollisionEffect.SwordLowKnockback:
                if(CombatDescriptor.Combat.ApplyDamageAfterCollision(origin,-(sbyte)seed.Damage,EnemyKnockbackStrength.Low,spawns,CombatDescriptor.CountsAsEnemy))
                    Entity.DeferNativeHitStatus();
                break;
            default:throw new System.NotSupportedException($"veranSpider.s: ENEMY $0f seed collision ${collision:x2}, effect ${effect:x2}.");
        }
        return new(true,seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,
            effect!=CollisionEffect.SwordLowKnockback);
    }
    public int BurnTargetId=>0x0f;
    public int BurnZFixed {get=>Entity.BurnZFixed;set=>Entity.BurnZFixed=value;}
    public override void ReleaseBurn()=>Entity.ReleaseBurn();
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null)=>
        _swordCollision=collision??SwordCollision.Type(state,level);
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)=>
        ApplySomariaBlockCollision(block,-_damage*2,Entity.NativeHitPending,spawns);
    public bool ApplyExpertPunch(Godot.Rect2 bounds,Godot.Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>
        Hit(ItemCollisionType.ExpertPunch,bounds,origin,damage,spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision item,Godot.Rect2 bounds,Godot.Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>
        Hit((int)item,bounds,origin,damage,spawns);
    private bool Hit(int collision,Godot.Rect2 bounds,Godot.Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Entity.CollisionEnabled||Entity.NativeHitPending||Entity.InvincibilityCounter!=0||!_data.SpiderActiveCollision(collision)||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds))return false;
        int effect=_data.Collision(0x10,collision);
        if(effect==CollisionEffect.None)return true;
        var strength=effect switch
        {
            CollisionEffect.SwordLowKnockback=>EnemyKnockbackStrength.Low,
            CollisionEffect.Sword=>EnemyKnockbackStrength.Normal,
            CollisionEffect.SwordHighKnockback=>EnemyKnockbackStrength.High,
            _=>throw new System.NotSupportedException($"veranSpider.s: ENEMY $0f item ${collision:x2}, effect ${effect:x2}.")
        };
        bool accepted=CombatDescriptor.Combat.ApplyDamageAfterCollision(origin,damage,strength,spawns,CombatDescriptor.CountsAsEnemy);
        if(accepted)Entity.DeferNativeHitStatus();
        return accepted;
    }
    public override void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.NativeHitPending || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        if (player.IsUsingShield)
        {
            int invincibility=Entity.InvincibilityCounter;
            base.HandleLinkContact(player);
            if (invincibility!=Entity.InvincibilityCounter) Entity.DeferNativeHitStatus();
        }
        else if (!Stunned && player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ) &&
            player.ApplyEnemyContactDamage(Entity.Position,_damage)) Entity.DeferNativeHitStatus();
    }
    public override bool ApplySwordHit(Godot.Rect2 bounds,Godot.Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)
        =>Hit(_swordCollision,bounds,origin,damage,spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) => ScreenTransitionPresentation.Hidden;
}
