using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// PART $4c/$4e: source native charge, flight and collision publication.
internal sealed partial class TwinrovaAttackPart : EnemyCharacter,IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IPostObjectLinkContactRoomEntity,IPostObjectMeleeCollisionRoomEntity,ILinkSwordStateAwareRoomEntity,
    ISwordHittableRoomEntity,IObjectCollisionHeightRoomEntity,INativePartHealthRoomEntity
{
    private readonly TwinrovaEnvironment _world;
    internal int Id {get;}
    internal int Subid {get;}
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal int ZFixed {get;}
    private int _pending=-1,_sword=4;
    private bool _collision;
    private readonly int _palette;
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead&&State!=0&&_collision;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(ZFixed>>8);
    internal override Texture2D CurrentDrawTexture=>Animation.CurrentTextureForPalette(_palette);
    public Node2D Node=>this;
    public bool Finished=>IsDead;
    public bool MeleeReportsContact {get;private set;}
    public int CollisionZ=>ZFixed>>8;
    internal TwinrovaAttackPart(TwinrovaEnvironment world,int id,int subid,Vector2 position,int z,int angle=0)
    {
        if(id is not(0x4c or 0x4e)||subid>1||id==0x4e&&subid!=0)throw new NotSupportedException($"Twinrova PART${id:x2}:${subid:x2}.");
        _world=world;Id=id;Subid=subid;Angle=angle;ZFixed=(z>>8)<<8;_collision=id==0x4c;
        _palette=id==0x4c&&subid==1?5:world.Data.Actor(id).Palette;
        InitializeEnemy(position.Floor(),EnemyCharacterConfiguration.FromImported(world.Data.Actor(id)),positionedOam:true,paletteVariants:[_palette]);
        if(subid==1)SetNativeCollisionRadii(2,2);
        Visible=false;ZIndex=ObjectDrawPriority.FromVisible(0x82);
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public void ClearHealthAndCollision()=>throw new NotSupportedException($"PART${Id:x2} has no health-clear collision column.");
    private void Move()=>Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
    private void Aim(RoomEntityFrame frame)=>Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),(frame.EnemyTarget??frame.Player.Position).Floor());
    private void DestroySnow()
    {
        if(_world.Entities.InteractionSlotAvailable)_world.Entities.AddEntity(new SnowDebrisRoomEntity(_world.Data.SnowDebris,Position));
        Finish();
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        try
        {
            if(_pending>=0)
            {
                int hit=_pending;_pending=-1;
                if(Id==0x4c&&hit!=0){Finish();return;}
                if(Id==0x4e&&(hit==3||hit is >=5 and <=8)){DestroySnow();return;}
            }
            if(State==0)
            {
                State=1;Visible=true;
                if(Id==0x4c&&Subid==1){Speed=0x50;RestartAnimation(1);_world.Entities.OnSoundRequested(SoundId.SndBeam2);return;}
                Counter=30;Speed=Id==0x4c?0x46:0x5a;
                if(Id==0x4e)_world.Entities.OnSoundRequested(SoundId.SndTeleport);
                return;
            }
            if(Subid==1)
            {
                AdvanceAnimation();Move();
                if(NativePartBoundary.IsScreenBoundaryAtPoint(_world.Entities.ActiveRoom,Position))Finish();
                return;
            }
            if(State==1)
            {
                if(Counter!=0)Counter--;
                if(Counter!=0)
                {
                    if(Id==0x4e&&(AnimationParameter&1)!=0){Animation.SetParameter(0);_collision=true;}
                    AdvanceAnimation();return;
                }
                State=2;Aim(frame);
            }
            if(Id==0x4c)AdvanceAnimation();
            Move();
            if(!NativePartBoundary.HasTileCollisionOrBoundary(_world.Entities.ActiveRoom,Position))return;
            if(Id==0x4e){DestroySnow();return;}
            Aim(frame);int angle=(Angle-2)&31;
            // Parent remains occupied during all three checked allocations.
            for(int i=0;i<3;i++,angle=(angle+2)&31)
                if(_world.Entities.PartSlotAvailable)_world.Entities.AddEntity(new TwinrovaAttackPart(_world,0x4c,1,Position,ZFixed,angle));
            _world.Entities.TryCreateVirePuff(Position,CollisionZ,0);Finish();
        }
        finally{AdvanceInvincibilityCounter();}
    }
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null)=>_sword=collision??SwordCollision.Type(state,level);
    public bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Hit(_sword,bounds);
    private bool Hit(int item,Rect2 bounds)
    {
        MeleeReportsContact=false;
        if(!CollisionEnabled||_pending>=0||InvincibilityCounter!=0||!_world.Data.Active(true,Id,item)||!RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds,bounds))return false;
        int effect=_world.Data.Effect(6,item);if(effect==0)return true;
        if(effect is not(0x1c or 0x1e or 0x1f))throw new NotSupportedException($"Twinrova PART${Id:x2}: item${item:x2}, effect${effect:x2}.");
        _pending=item;MeleeReportsContact=true;
        if(effect is 0x1e or 0x1f)
        {if(effect==0x1e&&item<=3)_world.Entities.OnSoundRequested(SoundId.SndBombLand);InvincibilityCounter=-28;_world.Entities.OnSoundRequested(SoundId.SndClink2);}
        return true;
    }
    public void HandleLinkContact(Player player)
    {
        if(!CollisionEnabled||_pending>=0||InvincibilityCounter!=0||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(player.CanAcceptShieldCollision&&player.IsUsingShield&&Hit(player.Inventory.ShieldLevel,player.ShieldCollisionBounds))
        {if(_world.Data.Effect(6,player.Inventory.ShieldLevel)==0x1e)player.ApplyShieldCollisionRecoil(Position,0,16);return;}
        if(player.OverlapsEnemyCollision(CollisionBounds,CollisionZ)&&player.ApplyEnemyContactDamage(Position,_world.Data.Actor(Id).DamageQuarters,RingDamageSource.Generic,34,15))_pending=0;
    }
}
