
namespace Oracles;

internal sealed class TwinrovaMinionRoomEntity : NativeHitEnemyRoomEntity<TwinrovaMinionCharacter>,IFixedRoomEntity,
    INativeEnemySlotRoomEntity,IPostObjectLinkContactRoomEntity,IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity,ILinkSwordStateAwareRoomEntity,IExpertPunchHittableRoomEntity,
    ISeedCollisionTarget,IBoomerangCollisionRoomEntity,ISomariaBlockCollisionRoomEntity
{
    internal TwinrovaMinionCharacter Actor=>Entity;
    internal int Slot {get;private set;}
    public override int DimitriCollisionType=>Entity.Id;
    public override int DimitriCollisionMode=>Entity.Id==0x5d?0x0e:0x41;
    public override BombchuTarget BombchuTarget=>new(Entity.Id,Entity.Visible,Entity.Position,Entity.CollisionBounds,Entity.Health);
    internal TwinrovaMinionRoomEntity(TwinrovaMinionCharacter actor):base(actor,actor.SetTransitionDrawOffset,
        EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(()=>actor.IsDead,()=>actor.CollisionBounds,
            (_,_)=>false,_=>false,actor.OverlapsLink,()=>actor.Position,actor.World.Data.Actor(actor.Id).DamageQuarters,()=>null),false,0,
            ()=>actor.Killed?RoomEnemyOutcome.EnemyDieUncounted():RoomEnemyOutcome.SilentDeletion(false),actor.World.Entities.OnSoundRequested),collisionZ:()=>actor.ZFixed>>8) {}
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve){Slot=slot;Entity.Bind(resolve);}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame,spawns);
    private bool Eligible(int item,Rect2 bounds)=>Entity.CollisionEnabled&&!Entity.JustHit&&Entity.InvincibilityCounter==0&&
        Entity.World.Data.Active(false,DimitriCollisionType,item)&&RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    protected override int ItemEffect(int item) => Entity.World.Data.Effect(DimitriCollisionMode,item);
    protected override bool Hit(int item,Rect2 bounds,Vector2 origin,int damage)
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
                Entity.PublishHit(item,0,-20);Entity.World.Entities.TryCreateItemCollisionClink(CollisionMidpoint(Entity.Position,origin),0);return true;
            default:throw new NotSupportedException($"twinrovaIce.s/twinrovaBat.s: ENEMY ${Entity.Id:x2}, mode ${DimitriCollisionMode:x2}, item ${item:x2}, effect ${effect:x2}.");
        }
    }
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
    public override void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled||Entity.JustHit||Entity.InvincibilityCounter!=0||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.CanAcceptShieldCollision&&Entity.World.Data.Active(false,DimitriCollisionType,player.Inventory.ShieldLevel)&&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,player.ShieldCollisionBounds))
        {
            int effect=Entity.World.Data.Effect(DimitriCollisionMode,player.Inventory.ShieldLevel);
            if(effect==CollisionEffect.None)return;
            var recoil=ShieldRecoil(effect,player.Inventory.ShieldLevel,"twinrovaIce.s/twinrovaBat.s");
            player.ApplyShieldCollisionRecoil(Entity.Position,recoil.Invincibility,recoil.Knockback);Entity.PublishHit(player.Inventory.ShieldLevel);
            Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);return;
        }
        if(player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ)&&player.ApplyEnemyContactDamage(Entity.Position,Entity.World.Data.Actor(Entity.Id).DamageQuarters,
            RingDamageSource.Generic,34,15))Entity.PublishHit(0);
    }
}
