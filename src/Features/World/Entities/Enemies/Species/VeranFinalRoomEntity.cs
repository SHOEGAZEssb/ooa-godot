
namespace Oracles;

internal sealed class VeranFinalRoomEntity : NativeHitEnemyRoomEntity<VeranFinalCharacter>,IFixedRoomEntity,
    INativeEnemySlotRoomEntity,IPostObjectLinkContactRoomEntity,IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity,ILinkSwordStateAwareRoomEntity,IExpertPunchHittableRoomEntity,
    ISeedCollisionTarget,IBoomerangCollisionRoomEntity,ISomariaBlockCollisionRoomEntity
{
    internal VeranFinalCharacter Actor=>Entity;
    internal int Slot {get;private set;}
    public override int DimitriCollisionType=>Entity.CollisionType;
    public override int DimitriCollisionMode=>Entity.CollisionMode;
    internal VeranFinalRoomEntity(VeranFinalCharacter actor):base(actor,actor.SetTransitionDrawOffset,
        EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(()=>actor.IsDead,()=>actor.CollisionBounds,
            (_,_)=>false,_=>false,actor.OverlapsLink,()=>actor.Position,actor.Data.Actor(2).DamageQuarters,()=>null),true,0,
            ()=>RoomEnemyOutcome.SilentDeletion(false),actor.World.Entities.OnSoundRequested),collisionZ:()=>actor.ZFixed>>8) {}
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve){Slot=slot;Entity.Slot=slot;}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame);
    private bool Eligible(int item,Rect2 bounds)=>Entity.CollisionEnabled&&!Entity.JustHit&&Entity.InvincibilityCounter==0&&
        Entity.Data.Active(false,Entity.CollisionType,item)&&RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    protected override int ItemEffect(int item) => Entity.Data.Effect(DimitriCollisionMode,item);
    protected override bool Hit(int item,Rect2 bounds,Vector2 origin,int damage)
    {
        MeleeReportsContact=false;if(!Eligible(item,bounds))return false;
        int effect=Entity.Data.Effect(Entity.CollisionMode,item);MeleeReportsContact=effect!=0;
        switch(effect)
        {
            case 0:return true;
            case 0x1c:case 0x20:Entity.PublishHit(origin,0);return true;
            case 0x21:Entity.PublishHit(origin,damage,32);Entity.World.Entities.OnSoundRequested(SoundId.SndBossDamage);return true;
            case 0x1b:
                Entity.World.Entities.TryCreateItemCollisionClink(CollisionMidpoint(Entity.Position,origin),CollisionZ);
                Entity.PublishBump(origin,20,0);return true;
            case 0x14:
                Entity.World.Entities.TryCreateItemCollisionClink(CollisionMidpoint(Entity.Position,origin),CollisionZ);goto case 0x0e;
            case 0x0e:Entity.PublishBump(origin,EnemyKnockbackStrength.High);return true;
            default:throw new NotSupportedException($"veranFinalForm.s: ENEMY $02 form ${Entity.SubId:x2}, mode ${Entity.CollisionMode:x2}, item ${item:x2}, effect ${effect:x2}.");
        }
    }
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        if(!block.CollisionEnabled||!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,block.ZHigh,7)||!Eligible(0x15,block.CollisionBounds))return false;
        if(Entity.Data.Effect(Entity.CollisionMode,0x15)!=0x2d)throw new NotSupportedException("veranFinalForm.s: Somaria requires effect $2d.");
        block.Flags|=0x20;return true;
    }
    public override void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled||Entity.JustHit||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.CanAcceptShieldCollision&&Entity.Data.Active(false,Entity.CollisionType,player.Inventory.ShieldLevel)&&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,player.ShieldCollisionBounds))
        {
            int effect=Entity.Data.Effect(Entity.CollisionMode,player.Inventory.ShieldLevel);
            if(effect==CollisionEffect.None)return;
            var recoil=ShieldRecoil(effect,player.Inventory.ShieldLevel,"veranFinalForm.s");
            player.ApplyShieldCollisionRecoil(Entity.Position,recoil.Invincibility,recoil.Knockback);Entity.PublishHit(Entity.Position,0);
            Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);return;
        }
        if(player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ)&&player.ApplyEnemyContactDamage(Entity.Position,Entity.Data.Actor(2).DamageQuarters,
            RingDamageSource.Generic,34,15))Entity.PublishHit(Entity.Position,0);
    }
}
