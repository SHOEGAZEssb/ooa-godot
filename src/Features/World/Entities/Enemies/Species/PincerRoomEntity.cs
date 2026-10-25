using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class PincerRoomEntity : CombatEnemyRoomEntityAdapter<PincerCharacter>,IFixedRoomEntity,
    IScreenTransitionPreloadRoomEntity,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze,
    INativeEnemySlotRoomEntity,INativeEnemyCounter1RoomEntity,ISeedCollisionTarget,IItemCollisionHittableRoomEntity,IBurningEnemyTarget
{
    public override bool CountsAsEnemy=>!Entity.Spawned && base.CountsAsEnemy;
    public override int DimitriCollisionMode=>Entity.CollisionMode;
    protected override bool Stunned=>Entity.StunCounter!=0;
    public bool UpdatesDuringDialogue=>Entity.State==0;
    public bool UpdatesDuringRoomEntityFreeze=>Entity.State==0;
    public int Counter1 {get=>Entity.Counter1;set=>Entity.Counter1=value;}
    public bool RetainsCounter1AfterDeletion=>false;
    internal EnemyCombatSourceDescriptor Source {get;}
    internal PincerRoomEntity(PincerCharacter actor,EnemyCombatSourceDescriptor source,Action<int> sound)
        :base(actor,actor.SetTransitionDrawOffset,EnemyCombatDescriptor.WithContactDamage(source,actor,actor.Record.DamageQuarters,
            actor.Hit,_=>false,(_,_)=>{},sound,EnemySwordResponse.NoKnockback,
            completedOutcome:()=>actor.Spawned || actor.BodyDeleted ? RoomEnemyOutcome.SilentDeletion(false) : RoomEnemyOutcome.EnemyDie(source.KillableEnemyIndex)),
            collisionZ:()=>actor.ZFixed>>8) => Source=source;
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve)=>Entity.NativeSlot=slot;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame.Player.Position,frame.Counter,spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {if(Entity.State==0)Entity.UpdateFrame(Vector2.Zero,0,spawns);return ScreenTransitionPresentation.Hidden;}
    public override void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled || Entity.NativeHitPending || Entity.InvincibilityCounter!=0 ||
            !player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.IsUsingShield && RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,player.ShieldCollisionBounds))
        {
            if(!player.CanAcceptShieldCollision)return;
            int effect=EnemyBehaviorTables.Shared.PincerEffects[player.Inventory.ShieldLevel].Value;
            var (invincibility,recoil)=effect switch
            {
                CollisionEffect.Effect06=>(15,19),CollisionEffect.Effect07=>(22,25),
                _=>throw new NotSupportedException($"pincer.s $45: shield effect${effect:x2} unsupported.")
            };
            player.ApplyShieldCollisionRecoil(Entity.Position,invincibility,recoil);
            Entity.DeferNativeHitStatus();return;
        }
        if(!Stunned && player.OverlapsEnemyCollision(Entity.CollisionBounds))
        {player.ApplyEnemyContactDamage(Entity.Position,Entity.Record.DamageQuarters);Entity.DeferNativeHitStatus();}
    }
    protected override bool TryApplySwitchHookEffect(int effect,SwitchHookItem hook,Vector2 linkPosition)
    {
        if(effect!=CollisionEffect.SwordNoKnockback || !Entity.Hit(hook.Position,hook.HitDamage))return false;
        hook.NotifyObjectCollision();CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);return true;
    }
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)
    {
        var data=EnemyBehaviorTables.Shared;
        if(!Entity.CollisionEnabled || Entity.InvincibilityCounter!=0 || Entity.NativeHitPending || data.PincerCollisionMask[(int)item].Value==0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds))return false;
        int effect=data.PincerEffects[(int)item].Value;
        if(effect==CollisionEffect.None)return true;
        if(effect!=CollisionEffect.SwordNoKnockback)throw new NotSupportedException($"pincer.s $45: item${(int)item:x2} effect${effect:x2} unsupported.");
        return CombatDescriptor.Combat.ApplyDamageAfterCollision(origin,damage,EnemyKnockbackStrength.Normal,spawns,CountsAsEnemy);
    }
    public override SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)=>throw new InvalidOperationException("Pincer requires its seed's live collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        var data=EnemyBehaviorTables.Shared;
        if(!Entity.CollisionEnabled || Entity.InvincibilityCounter!=0 || Entity.NativeHitPending || data.PincerCollisionMask[collision].Value==0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds))return default;
        int effect=data.PincerEffects[collision].Value;
        if(effect==CollisionEffect.None)return new(true,SeedHitResult.None,false);
        if(effect==CollisionEffect.SwordNoKnockback)Entity.Hit(origin,-(sbyte)seed.Damage);
        else if(effect==CollisionEffect.Burn) {Entity.BeginEmberHit();if(PartSlotAvailable())spawns.Add(new BurningEnemySpawn(this));}
        else if(effect==CollisionEffect.PegasusSeed) {Entity.ApplyBoomerangStun(240);Entity.InvincibilityCounter=-16;}
        else if(effect!=CollisionEffect.Effect20)throw new NotSupportedException($"pincer.s $45: seed${collision:x2} effect${effect:x2} unsupported.");
        if(effect!=CollisionEffect.Effect20)CombatDescriptor.RequestSound(SoundId.SndDamageEnemy);
        return new(true,seed.SeedItem==ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,effect!=CollisionEffect.SwordNoKnockback);
    }
    internal Func<bool> PartSlotAvailable {private get;set;}=null!;
    public int BurnTargetId=>0x45;
    public int BurnZFixed {get=>Entity.ZFixed;set=>Entity.ZFixed=value;}
}
