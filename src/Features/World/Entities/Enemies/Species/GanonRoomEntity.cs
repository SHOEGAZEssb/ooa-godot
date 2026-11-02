
namespace Oracles;

internal sealed class GanonRoomEntity : NativeHitEnemyRoomEntity<GanonCharacter>,IFixedRoomEntity,
    INativeEnemySlotRoomEntity,IPostObjectLinkContactRoomEntity,IPostObjectMeleeCollisionRoomEntity,
    IPostObjectItemCollisionRoomEntity,ILinkSwordStateAwareRoomEntity,IExpertPunchHittableRoomEntity,
    ISeedCollisionTarget,IBoomerangCollisionRoomEntity,ISomariaBlockCollisionRoomEntity,ISwordAttackerKnockbackRoomEntity
{
    private int _attackerRecoil;
    internal GanonCharacter Actor=>Entity;
    internal int Slot {get;private set;}
    public override int DimitriCollisionType=>Entity.CollisionType&127;
    public override int DimitriCollisionMode=>0x0b;
    public override BombchuTarget BombchuTarget=>new(4,Entity.Visible,Entity.Position,Entity.CollisionBounds,Entity.Health);
    internal GanonRoomEntity(GanonCharacter actor):base(actor,actor.SetTransitionDrawOffset,
        EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(()=>actor.IsDead,()=>actor.CollisionBounds,
            (_,_)=>false,_=>false,actor.OverlapsLink,()=>actor.Position,actor.World.Data.Actor(4).DamageQuarters,()=>null),true,0,
            ()=>RoomEnemyOutcome.SilentDeletion(false),actor.World.Entities.OnSoundRequested),collisionZ:()=>actor.ZFixed>>8) {}
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve){Slot=slot;Entity.Slot=slot;}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame);
    private bool Eligible(int item,Rect2 bounds)=>Entity.CollisionEnabled&&!Entity.JustHit&&Entity.InvincibilityCounter==0&&
        Entity.World.Data.Active(false,DimitriCollisionType,item)&&RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    protected override int ItemEffect(int item) => Entity.World.Data.Effect(DimitriCollisionMode,item);
    protected override bool Hit(int item,Rect2 bounds,Vector2 origin,int damage)
    {
        _attackerRecoil=0;MeleeReportsContact=false;if(!Eligible(item,bounds))return false;
        int effect=Entity.World.Data.Effect(0x0b,item);MeleeReportsContact=effect!=0;
        switch(effect)
        {
            case 0:return true;
            case 0x1c:case 0x20:Entity.PublishHit(item);return true;
            case 0x15:case 0x16:
                _attackerRecoil=effect==0x15?EnemyBehaviorTables.For(AssetRoot).ArmoredSwordAttackerKnockback.LowFrames:EnemyBehaviorTables.For(AssetRoot).ArmoredSwordAttackerKnockback.NormalFrames;
                Entity.PublishHit(item,0,-28);Entity.World.Entities.TryCreateItemCollisionClink(CollisionMidpoint(Entity.Position,origin),0);
                Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);return true;
            case 0x1b:Entity.PublishHit(item,0,-20);Entity.World.Entities.TryCreateItemCollisionClink(CollisionMidpoint(Entity.Position,origin),0);return true;
            case 0x2c:
                _attackerRecoil=EnemyBehaviorTables.For(AssetRoot).ArmoredSwordAttackerKnockback.NormalFrames;
                Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);goto case 0x21;
            case 0x21:Entity.PublishHit(item,damage,32);Entity.World.Entities.OnSoundRequested(SoundId.SndBossDamage);return true;
            default:throw new NotSupportedException($"ganon.s: ENEMY $04 form ${Entity.State:x2}, mode ${0x0b:x2}, item ${item:x2}, effect ${effect:x2}.");
        }
    }
    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength,out SwordAttackerKnockback response)
    {response=new(Entity.Position,_attackerRecoil);bool hit=_attackerRecoil!=0;_attackerRecoil=0;return hit;}
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        if(!block.CollisionEnabled||!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,block.ZHigh,7)||!Eligible(0x15,block.CollisionBounds))return false;
        if(Entity.World.Data.Effect(0x0b,0x15)!=0x2d)throw new NotSupportedException("ganon.s: Somaria requires effect $2d.");
        block.Flags|=0x20;return true;
    }
    public override void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled||Entity.JustHit||Entity.InvincibilityCounter!=0||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.CanAcceptShieldCollision&&Entity.World.Data.Active(false,DimitriCollisionType,player.Inventory.ShieldLevel)&&
            RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,player.ShieldCollisionBounds))
        {
            int effect=Entity.World.Data.Effect(0x0b,player.Inventory.ShieldLevel);
            if(effect==CollisionEffect.None)return;
            var recoil=ShieldRecoil(effect,player.Inventory.ShieldLevel,"ganon.s");
            player.ApplyShieldCollisionRecoil(Entity.Position,recoil.Invincibility,recoil.Knockback);Entity.PublishHit(player.Inventory.ShieldLevel);
            Entity.World.Entities.OnSoundRequested(SoundId.SndBombLand);return;
        }
        if(player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ)&&player.ApplyEnemyContactDamage(Entity.Position,Entity.World.Data.Actor(4).DamageQuarters,
            RingDamageSource.Generic,34,15))Entity.PublishHit(0);
    }
}
