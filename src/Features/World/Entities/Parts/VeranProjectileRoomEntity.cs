using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// PART_VERAN_PROJECTILE $37. The core is also a collidable PART; each ray
// inherits its XY/Z bytes and advances at its newly allocated live slot.
internal sealed partial class VeranProjectileRoomEntity : EnemyCharacter, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IPostObjectLinkContactRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    ISwordHittableRoomEntity, ILinkSwordStateAwareRoomEntity, IPostObjectItemCollisionRoomEntity, IItemCollisionHittableRoomEntity,
    IBombCollisionRoomEntity, IBoomerangCollisionRoomEntity, ISeedCollisionTarget,
    IObjectCollisionHeightRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEntityManager _owner;
    private readonly PossessedNayruCharacter _parent;
    private readonly PalacePossessionDatabase _data;
    private int _z, _angle, _swordCollision=4;
    private bool _hit;
    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int ZFixed => _z;
    internal override bool InitializationPending => State==0;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset+Vector2.Down*(_z>>8);
    public Node2D Node => this;
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public bool Finished => IsDead;
    public bool MeleeReportsContact => true;
    public int CollisionZ => _z>>8;
    public bool HasBombCollisionIdentity => true;
    public override Rect2 CollisionBounds => new(Position-Vector2.One*(SubId==0?6:4),Vector2.One*(SubId==0?12:8));
    public bool UpdatesDuringDialogue => State==0;
    public bool UpdatesDuringRoomEntityFreeze => State==0;
    internal VeranProjectileRoomEntity(RoomEntityManager owner,PalacePossessionDatabase data,PossessedNayruCharacter parent,
        int subid,Vector2 position,int z,int angle)
    {
        _owner=owner; _data=data; _parent=parent; SubId=subid; _z=z; _angle=angle;
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(data.Projectile),positionedOam:true);
        Visible=false;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (_hit) { Finish(); return; }
        if (SubId==0 && (!_parent.CollisionEnabled || _parent.IsDead)) { Puff(); return; }
        if (State==0)
        {
            State=1; Visible=true;
            if (SubId==0) _z=(_z&255)|-0x400;
            else { SetAnimation(1); _owner.OnSoundRequested(SoundId.SndVeranProjectile); }
            return;
        }
        if (SubId==0)
        {
            if (State==1)
            {
                _z=unchecked((short)(_z-0x100));
                if ((_z>>8)==-16) { State=2; Counter=129; }
            }
            else if (--Counter==0) { Puff(); return; }
            else if ((Counter&7)==0) _owner.TryCreateVeranProjectile(_parent,1,Position,_z,(Counter>>2)&31);
            AdvanceAnimation(); return;
        }
        if (State==1) { _z=unchecked((short)(_z+0x100)); if ((_z>>8)==0) State=2; }
        Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),0x64,_angle).PrecisePosition;
        // partCommon_checkTileCollisionOrOutOfBounds returns NZ for a wall:
        // RET C preserves the nonzero flags from the collision calculation.
        // Only the $ff screen-boundary sentinel returns Z to this caller.
        if (_owner.ActiveRoom.GetTerrainInfo(Position.Floor()).Collision==0xff) Finish();
    }
    private void Puff()
    {
        if (_owner.InteractionSlotAvailable) _owner.TryCreateVirePuff(Position,_z>>8,0x80);
        Finish();
    }
    public void HandleLinkContact(Player player)
    {
        if (State==0 || _hit || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        if (player.IsUsingShield && player.CanAcceptShieldCollision && Hit(player.Inventory.ShieldLevel,player.ShieldCollisionBounds)) return;
        if (player.OverlapsEnemyCollision(CollisionBounds,CollisionZ) && player.ApplyEnemyContactDamage(Position,_data.Projectile.DamageQuarters,
            RingDamageSource.Generic,34,15)) _hit=true;
    }
    public bool ApplySwordHit(Rect2 bounds,Vector2 source,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)
    {
        return Hit(_swordCollision,bounds);
    }
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null) => _swordCollision=collision??SwordCollision.Type(state,level);
    private bool Hit(int collision,Rect2 bounds)
    {
        if (State==0 || _hit || !_data.ProjectileActiveCollision(collision) || !RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds,bounds)) return false;
        int effect=_data.Collision(6,collision);
        switch(effect)
        {
            case 0: return true;
            case 0x1c: case 0x20: _hit=true; return true;
            case 0x1f: _hit=true; InvincibilityCounter=-28; _owner.OnSoundRequested(SoundId.SndClink2); return true;
            default: throw new NotSupportedException($"veranProjectile.s: PART$37 mode$06 collision${collision:x2}, effect${effect:x2}.");
        }
    }
    public bool ApplyItemCollision(RoomEntityItemCollision collision,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns) => Hit((int)collision,bounds);
    public bool ApplyBombCollision(IBombExplosionRoomEntity bomb,ICollection<RoomEntitySpawn> spawns) => bomb.CollisionEnabled &&
        RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,bomb.CollisionZ,bomb.CollisionZRadius) && Hit(0x18,bomb.CollisionBounds);
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns) => item.CollisionEnabled &&
        RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,item.ZHigh,7) && Hit(0x17,item.CollisionBounds) ? new(true,_data.Collision(6,0x17)!=0) : default;
    public SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("PART$37 requires the live imported seed collision type.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int collision,ICollection<RoomEntitySpawn> spawns)
    {
        if (!Hit(collision,bounds)) return default;
        int effect=_data.Collision(6,collision);
        return effect==0 ? new(true,SeedHitResult.None,false) :
            new(true,seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,effect==0x20);
    }
}
