using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KingMoblinRoomEntity : CombatEnemyRoomEntityAdapter<KingMoblinBoss>, IFixedRoomEntity, IPlayerRestriction, ISeedCollisionTarget
{
    internal KingMoblinRoomEntity(KingMoblinBoss boss)
        : base(boss,boss.SetTransitionDrawOffset,EnemyCombatDescriptor.Special(
            EnemyCombatComponent.WithContactDamage(()=>boss.IsDead,()=>boss.CollisionBounds,(_,_)=>false,_=>false,
                _=>false,()=>boss.Position,0,()=>null),countsAsEnemy:true,killableEnemyIndex:0,
            completedOutcome:()=>RoomEnemyOutcome.SilentDeletion(false))) { }
    public bool DisablesSword => Entity.ControlsDisabled;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => DisablesSword;
    public bool DisablesPlayerContact => DisablesSword;
    public bool FreezesPlayerUpdates => DisablesSword;
    public override int DimitriCollisionType => 0x7f;
    public override int DimitriCollisionMode => 0x50;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(frame,spawns);
    protected override bool TryApplySwitchHookEffect(int effect,SwitchHookItem hook,Vector2 linkPosition)
    {
        if(effect!=0x1c) return false;
        hook.NotifyObjectCollision(); return true;
    }
    public override SeedHitResult ApplySeedHit(Rect2 hitbox,Vector2 origin,int seedItem,ICollection<RoomEntitySpawn> spawns)
        => throw new System.InvalidOperationException("ENEMY_KING_MOBLIN $7f seed collision requires the projectile's native collision type.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 hitbox,Vector2 origin,SeedRecord seed,int collisionType,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Entity.CollisionEnabled || Entity.InvincibilityCounter!=0 ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(Entity.CollisionBounds,hitbox)) return default;
        int effect=Entity.Data.Bytes("collision")[collisionType];
        if(effect==0) return new(true,SeedHitResult.None,false);
        if(effect!=0x20) throw new System.NotSupportedException($"ENEMY_KING_MOBLIN $7f: seed collision ${collisionType:x2}, effect ${effect:x2}.");
        return new(true,seed.SeedItem==0x24?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,true);
    }
}
