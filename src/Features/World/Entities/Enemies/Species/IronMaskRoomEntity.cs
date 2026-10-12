using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class IronMaskRoomEntity : CombatEnemyRoomEntityAdapter<IronMaskCharacter>, IFixedRoomEntity,
    IScreenTransitionPreloadRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze,
    IItemCollisionHittableRoomEntity,ISeedCollisionTarget,ILinkSwordStateAwareRoomEntity,
    ISwordAttackerKnockbackRoomEntity,IPostObjectMeleeCollisionRoomEntity,INativeEnemyCounter1RoomEntity,IBurningEnemyTarget
{
    private int _sword=ItemCollisionType.L1Sword;
    public int Counter1 {get=>Entity.Counter1;set=>Entity.Counter1=value;}
    public bool RetainsCounter1AfterDeletion=>false;
    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength,out SwordAttackerKnockback response)
    {
        var data=EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback;
        int frames= strength==EnemyKnockbackStrength.Low ? data.LowFrames : strength==EnemyKnockbackStrength.High ? data.HighFrames : data.NormalFrames;
        response=new(Entity.Position,frames); return Entity.CollisionMode==0x1d && MeleeReportsContact;
    }
    public bool MeleeReportsContact {get;private set;}
    public override bool CountsAsEnemy => !Entity.MaskDetached && base.CountsAsEnemy;
    public override int DimitriCollisionMode => Entity.CollisionMode;
    protected override bool Stunned=>Entity.StunCounter!=0;
    internal EnemyCombatSourceDescriptor Source {get;}
    public bool UpdatesDuringDialogue=>Entity.InitializationPending;
    public bool UpdatesDuringRoomEntityFreeze=>Entity.InitializationPending;

    internal IronMaskRoomEntity(IronMaskCharacter actor,EnemyCombatSourceDescriptor source,Action<int> sound)
        :base(actor,actor.SetTransitionDrawOffset,EnemyCombatDescriptor.WithContactDamage(source,actor,actor.Record.DamageQuarters,
            actor.TakeSwordHit,actor.TakeBurnHit,actor.ApplySwordKnockback,sound,EnemySwordResponse.Knockback,
            completedOutcome:()=>actor.MaskExpired ? RoomEnemyOutcome.SilentDeletion(false) :
                actor.DiedInHazard ? RoomEnemyOutcome.HazardDeletion(source.CountsAsEnemy) : RoomEnemyOutcome.EnemyDie(source.KillableEnemyIndex)),
            collisionZ:()=>actor.ZFixed>>8) => Source=source;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame.Player.EnemyContactPosition,frame.Counter);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {Entity.InitializeState();return ScreenTransitionPresentation.Visible;}
    public void SetLinkSwordState(SwordActionState state,int level,int? itemCollisionType=null)=>_sword=itemCollisionType??SwordCollision.Type(state,level);
    private bool Hit(int item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)
    {
        var data=EnemyBehaviorTables.Shared;
        if (!Entity.CollisionEnabled || Entity.NativeHitPending || Entity.InvincibilityCounter!=0 || data.IronMaskCollisionMask[item].Value==0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds)) return false;
        int effect=(Entity.CollisionMode==data.IronMaskState[2].Value ? data.IronMaskMaskedEffects : data.IronMaskUnmaskedEffects)[item].Value;
        MeleeReportsContact=effect!=CollisionEffect.None;
        switch(effect)
        {
            case CollisionEffect.None:return true;
            case 0x18:case 0x19:case 0x1a:
                Entity.ApplyBump(origin,effect==0x18 ? EnemyKnockbackStrength.Low : effect==0x19 ? EnemyKnockbackStrength.Normal : EnemyKnockbackStrength.High);
                spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position,origin)));return true;
            case CollisionEffect.Effect20:return true;
            case CollisionEffect.Effect1b:
                Entity.InvincibilityCounter=-20; Entity.DeferNativeHitStatus(); spawns.Add(new EnemyClinkSpawn(CollisionMidpoint(Entity.Position,origin))); return true;
            case CollisionEffect.Sword: return CombatDescriptor.Combat.ApplyDamageAfterCollision(origin,damage,EnemyKnockbackStrength.Normal,spawns,CountsAsEnemy);
            case CollisionEffect.SwordLowKnockback:return CombatDescriptor.Combat.ApplyDamageAfterCollision(origin,damage,EnemyKnockbackStrength.Low,spawns,CountsAsEnemy);
            case CollisionEffect.SwordHighKnockback:return CombatDescriptor.Combat.ApplyDamageAfterCollision(origin,damage,EnemyKnockbackStrength.High,spawns,CountsAsEnemy);
            case CollisionEffect.SwordNoKnockback:
                if (!Entity.TakeDeferredNoKnockbackHit(origin,damage)) return false;
                Entity.DeferNativeHitStatus();
                CombatDescriptor.RequestSound(SoundId.SndDamageEnemy); return true;
            default:throw new NotSupportedException($"ironMask.s $1c mode${Entity.CollisionMode:x2}: collision${item:x2} effect${effect:x2} unsupported.");
        }
    }
    public override bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Hit(_sword,bounds,origin,damage,spawns);
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit((int)item,bounds,origin,damage,spawns);
    public override SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int seedItem,ICollection<RoomEntitySpawn> spawns)=>throw new InvalidOperationException("Iron Mask requires the seed's live collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        var data=EnemyBehaviorTables.Shared;
        if(!Entity.CollisionEnabled || Entity.InvincibilityCounter!=0 || Entity.NativeHitPending || data.IronMaskCollisionMask[collision].Value==0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds))return default;
        int effect=(Entity.CollisionMode==data.IronMaskState[2].Value ? data.IronMaskMaskedEffects : data.IronMaskUnmaskedEffects)[collision].Value;
        if(effect==CollisionEffect.None)return new(true,SeedHitResult.None,false);
        if(effect==CollisionEffect.Burn)
        {
            Entity.BeginEmberHit();if(PartSlotAvailable())spawns.Add(new BurningEnemySpawn(this));return new(true,SeedHitResult.Activate,true);
        }
        if(effect==CollisionEffect.GaleSeed) {Entity.ClearSeedStun();BeginNativeGale(origin,()=>RandomSource());return new(true,SeedHitResult.Activate,true);}
        if(effect==CollisionEffect.PegasusSeed) {Entity.ApplyBoomerangStun(240);Entity.InvincibilityCounter=-16;CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);return new(true,SeedHitResult.Activate,true);}
        if(!Hit(collision,bounds,origin,-(sbyte)seed.Damage,spawns))return default;
        return new(true,seed.SeedItem==ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,false);
    }
    internal Func<byte> RandomSource {private get;set;}=null!;
    internal Func<bool> PartSlotAvailable {private get;set;}=null!;
    public bool BurnTargetAlive=>GodotObject.IsInstanceValid(Entity) && !Entity.IsDead;
    public int BurnTargetId=>0x1c;
    public Vector2 BurnPosition=>Entity.Position;
    public int BurnHealth {get=>Entity.Health;set=>Entity.Health=value;}
    public int BurnZFixed {get=>Entity.ZFixed;set=>Entity.ZFixed=value;}
    public void ReleaseBurn()=>Entity.InvincibilityCounter=0;
}
