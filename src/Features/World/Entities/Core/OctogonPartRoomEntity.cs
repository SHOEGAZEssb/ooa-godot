using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class OctogonPartRoomEntity(OctogonPart part,Action<int> sound)
    : RoomEntityAdapter<OctogonPart>(part,part.SetTransitionDrawOffset), IFixedRoomEntity,
        IRoomEntityLifetime, INativePartHealthRoomEntity, IObjectCollisionHeightRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        ISwordHittableRoomEntity, IPostObjectMeleeCollisionRoomEntity, ILinkSwordStateAwareRoomEntity,
        IPostObjectLinkContactRoomEntity, IPostObjectItemCollisionRoomEntity, IBombCollisionRoomEntity,
        IBoomerangCollisionRoomEntity, ISeedCollisionTarget, ISomariaBlockCollisionRoomEntity
{
    private readonly OctogonEffectsDatabase _data = OctogonEffectsDatabase.Shared;
    private int _sword = ItemCollisionType.L1Sword;
    public bool Finished => Entity.Finished;
    public int CollisionZ => Entity.ZFixed>>8;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public bool MeleeReportsContact => true;
    public bool HasBombCollisionIdentity => true;
    public void ClearHealthAndCollision() => Entity.ClearHealthAndCollision();
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame);
    public void SetLinkSwordState(SwordActionState state,int level,int? itemCollisionType = null) =>
        _sword = itemCollisionType ?? SwordCollision.Type(state,level);
    private bool Eligible(int collision,Rect2 bounds) => Entity.CollisionEnabled && !Entity.PendingCollision &&
        Entity.InvincibilityCounter == 0 && _data.Collision(Entity.Id,collision).Enabled &&
        RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,bounds);
    private bool Hit(int collision,Rect2 bounds)
    {
        if (!Eligible(collision,bounds)) return false;
        switch (_data.Collision(Entity.Id,collision).Effect)
        {
            case CollisionEffect.None: return true;
            case CollisionEffect.Effect1c: case CollisionEffect.Effect20:
                Entity.PublishCollision(collision); return true;
            case CollisionEffect.Effect1f:
                Entity.InvincibilityCounter = -28; Entity.PublishCollision(collision); sound(SoundId.SndClink2); return true;
            default: throw new NotSupportedException($"Octogon PART${Entity.Id:x2} collision${collision:x2} has unrepresented effect${_data.Collision(Entity.Id,collision).Effect:x2}.");
        }
    }
    public bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns) => Hit(_sword,bounds);
    public bool ApplyItemCollision(RoomEntityItemCollision collision,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns) => Hit((int)collision,bounds);
    public bool ApplyBombCollision(IBombExplosionRoomEntity bomb,ICollection<RoomEntitySpawn> spawns) =>
        bomb.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,bomb.CollisionZ,bomb.CollisionZRadius) && Hit(ItemCollisionType.Bomb,bomb.CollisionBounds);
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns) =>
        item.CollisionEnabled && RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,item.ZHigh,7) && Hit(ItemCollisionType.L1Boomerang,item.CollisionBounds)
            ? new(true,_data.Collision(Entity.Id,ItemCollisionType.L1Boomerang).Effect != 0) : default;
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        if (!block.CollisionEnabled || !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,block.ZHigh,7) || !Eligible(ItemCollisionType.SomariaBlock,block.CollisionBounds)) return false;
        int effect = _data.Collision(Entity.Id,ItemCollisionType.SomariaBlock).Effect;
        if (effect == CollisionEffect.Effect2f)
        {
            // collisionEffect2f: LINKDMG_30 then ENEMYDMG_04.
            block.QueueEnemyDamage(unchecked((sbyte)_data.Properties(Entity.Id)[3]),1,Entity.Position);
            Entity.ApplySomariaDamage(block.Damage);
        }
        else if (effect != 0) Entity.PublishCollision(ItemCollisionType.SomariaBlock);
        return true;
    }
    public void HandleLinkContact(Player player)
    {
        if (!Entity.CollisionEnabled || Entity.PendingCollision || Entity.InvincibilityCounter != 0 || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        int shield = player.Inventory.ShieldLevel;
        if (player.IsUsingShield && shield is >= 1 and <= 3 && player.CanAcceptShieldCollision && Hit(shield,player.ShieldCollisionBounds)) return;
        if (!player.NativeObjectVulnerable || !player.OverlapsEnemyCollision(Entity.CollisionBounds,CollisionZ) || !_data.Collision(Entity.Id,ItemCollisionType.Link).Enabled) return;
        int effect = _data.Collision(Entity.Id,ItemCollisionType.Link).Effect;
        if (effect == CollisionEffect.Effect1c) Entity.PublishCollision(ItemCollisionType.Link);
        else if (effect == CollisionEffect.DamageLink)
        {
            int raw = _data.Properties(Entity.Id)[3];
            if (player.ApplyEnemyContactDamage(Entity.Position,(0x100-raw)/2,RingDamageSource.Generic,34,15)) Entity.PublishCollision(ItemCollisionType.Link);
        }
        else if (effect != 0) throw new NotSupportedException($"Octogon PART${Entity.Id:x2} Link effect${effect:x2}.");
    }
    public SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)
    {
        if (item == ItemId.MysterySeed) throw new InvalidOperationException("Octogon parts require Mystery's live collision type.");
        return new SeedSatchelDatabase().TryGet(item,out var seed) ? ApplySeedCollision(bounds,origin,seed,seed.Collision&31,spawns).Effect : SeedHitResult.None;
    }
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        if (!Hit(collision,bounds)) return default;
        int effect = _data.Collision(Entity.Id,collision).Effect;
        return effect == 0 ? new(true,SeedHitResult.None,false) : new(true,seed.SeedItem == ItemId.MysterySeed ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate,effect == CollisionEffect.Effect20);
    }
}
