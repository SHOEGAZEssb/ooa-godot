using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// PART $2d and $2f retain their distinct native state machines and share the
// original PART collision/presentation boundary. Baby Link belongs to Player.
internal sealed partial class VeranFairyProjectileRoomEntity : EnemyCharacter,IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IPostObjectLinkContactRoomEntity,IPostObjectMeleeCollisionRoomEntity,IPostObjectItemCollisionRoomEntity,
    ILinkSwordStateAwareRoomEntity,ISwordHittableRoomEntity,ISeedCollisionTarget,IBoomerangCollisionRoomEntity,
    IBombCollisionRoomEntity,IObjectCollisionHeightRoomEntity,INativePartHealthRoomEntity
{
    private readonly RoomEntityManager _owner;
    private readonly VeranFairyDatabase _data;
    private readonly VeranFairyCharacter _parent;
    private int _sword=4;
    private bool _hit,_healthCleared;
    internal int Id {get;}
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Speed {get;private set;}
    internal int Angle {get;private set;}
    internal int ZFixed {get;}
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead&&!_hit&&!_healthCleared;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(ZFixed>>8);
    public Node2D Node=>this;
    public bool Finished=>IsDead;
    public bool MeleeReportsContact {get;private set;}
    public bool HasBombCollisionIdentity=>true;
    public int CollisionZ=>ZFixed>>8;
    internal VeranFairyProjectileRoomEntity(RoomEntityManager owner,VeranFairyDatabase data,VeranFairyCharacter parent,
        int id,Vector2 position,int z,int angle)
    {
        _owner=owner;_data=data;_parent=parent;Id=id;ZFixed=(z>>8)<<8;Angle=angle;
        InitializeEnemy(position.Floor(),EnemyCharacterConfiguration.FromImported(data.Actor(id)),positionedOam:true);
        Visible=false;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public void ClearHealthAndCollision()=>_healthCleared=true;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(_hit||_healthCleared){Finish();return;}
        if(_parent.IsDead||_parent.Health==0)
        {
            // objectCreatePuff uses INTERAC_PUFF:$00, including SND_POOF.
            _owner.TryCreateVirePuff(Position,ZFixed>>8,0);Finish();return;
        }
        Vector2 target=frame.EnemyTarget??frame.Player.EnemyContactPosition;
        if(State==0)
        {
            State=1;Visible=true;ZIndex=ObjectDrawPriority.BehindLinkZIndex;
            if(Id==0x2d){Speed=0x3c;Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),target.Floor());_owner.OnSoundRequested(SoundId.SndVeranFairyAttack);}
            else {Speed=0x14;Counter=30;_owner.OnSoundRequested(SoundId.SndBlueStalfosCharge);}
            return;
        }
        if(Id==0x2f&&State==1)
        {
            if(Counter!=0)Counter--;
            if(Counter==0){State=2;Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),target.Floor());_owner.OnSoundRequested(SoundId.SndBeam2);}
            AdvanceAnimation();return;
        }
        if(Id==0x2f&&_parent.State==3&&_parent.Attack==2&&(frame.Counter&15)==0)
        {
            int toward=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),target.Floor());int difference=(Angle-toward)&31;
            if(difference!=0)Angle=(Angle+(difference<16?-1:1))&31;
        }
        if(!NativePartBoundary.IsInside(_owner.ActiveRoom,Position.Floor(),Angle,_data.Bytes("part-boundaries"))){Finish();return;}
        Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;AdvanceAnimation();
    }
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null)=>_sword=collision??SwordCollision.Type(state,level);
    private int Mode=>_data.Bytes($"part-{Id:x2}")[1]&0x7f;
    private bool Hit(int item,Rect2 bounds)
    {
        MeleeReportsContact=false;
        if(!CollisionEnabled||!_data.Active(true,Id,item)||!RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds,bounds))return false;
        int effect=_data.Effect(Mode,item);MeleeReportsContact=effect!=0;
        switch(effect)
        {
            case 0:return true;
            case 0x1c:case 0x20:_hit=true;return true;
            case 0x1e:case 0x1f:_hit=true;InvincibilityCounter=-28;_owner.OnSoundRequested(SoundId.SndClink2);return true;
            default:throw new NotSupportedException($"PART ${Id:x2}, item ${item:x2}, effect ${effect:x2}.");
        }
    }
    public bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Hit(_sword,bounds);
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit((int)item,bounds);
    public bool ApplyBombCollision(IBombExplosionRoomEntity bomb,ICollection<RoomEntitySpawn> spawns)=>bomb.CollisionEnabled&&
        RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,bomb.CollisionZ,bomb.CollisionZRadius)&&Hit(0x18,bomb.CollisionBounds);
    public BoomerangCollisionResponse ApplyBoomerangCollision(BoomerangItem item,ICollection<RoomEntitySpawn> spawns)=>item.CollisionEnabled&&
        RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,item.ZHigh,7)&&Hit(0x17,item.CollisionBounds)?new(true,MeleeReportsContact):default;
    public SeedHitResult ApplySeedHit(Rect2 bounds,Vector2 origin,int item,ICollection<RoomEntitySpawn> spawns)=>throw new InvalidOperationException($"PART ${Id:x2} requires live seed collision identity.");
    public SeedCollisionResponse ApplySeedCollision(Rect2 bounds,Vector2 origin,SeedRecord seed,int item,ICollection<RoomEntitySpawn> spawns)
    {
        if(!Hit(item,bounds))return default;int effect=_data.Effect(Mode,item);
        return effect==0?new(true,SeedHitResult.None,false):new(true,seed.SeedItem==ItemId.MysterySeed?SeedHitResult.ActivateRandomSeed:SeedHitResult.Activate,effect==0x20);
    }
    public void HandleLinkContact(Player player)
    {
        if(!CollisionEnabled||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.IsUsingShield&&player.CanAcceptShieldCollision&&Hit(player.Inventory.ShieldLevel,player.ShieldCollisionBounds))
        {
            if(_data.Effect(Mode,player.Inventory.ShieldLevel)==0x1e)player.ApplyShieldCollisionRecoil(Position,0,16);
            return;
        }
        if(!player.NativeObjectVulnerable||!player.OverlapsEnemyCollision(CollisionBounds,CollisionZ))return;
        if(Id==0x2f){player.QueueBabyTransformation();_hit=true;}
        else if(player.ApplyEnemyContactDamage(Position,_data.Actor(Id).DamageQuarters,RingDamageSource.Generic,34,15))_hit=true;
    }
}
