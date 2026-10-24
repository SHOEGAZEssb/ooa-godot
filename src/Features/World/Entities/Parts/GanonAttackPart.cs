using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Native PART $50/$52; each child retains its own live PART slot and fractions.
internal sealed partial class GanonAttackPart : EnemyCharacter,IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IPostObjectLinkContactRoomEntity,IPostObjectMeleeCollisionRoomEntity,IPostObjectItemCollisionRoomEntity,
    ILinkSwordStateAwareRoomEntity,ISwordHittableRoomEntity,IObjectCollisionHeightRoomEntity,INativePartHealthRoomEntity,
    ISwordAttackerKnockbackRoomEntity,ISomariaBlockCollisionRoomEntity
{
    internal GanonEnvironment World {get;}
    internal int Id {get;}
    internal int Subid {get;}
    internal int ParentSlot {get;}
    internal int Var03 {get;}
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal int ZFixed {get;private set;}
    private int _palette;
    internal override Texture2D CurrentDrawTexture=>Animation.CurrentTextureForPalette(_palette);
    internal Vector2I Center {get;private set;}
    private int _sword=4,_pending=-1,_attackerRecoil;
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead&&State!=0;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(ZFixed>>8);
    public Node2D Node=>this;
    public bool Finished=>IsDead;
    public int CollisionZ=>ZFixed>>8;
    public bool MeleeReportsContact {get;private set;}
    internal GanonAttackPart(GanonEnvironment world,int id,int subid,int parent,Vector2 position,int z,int angle=0,int speed=0,int var03=0)
    {
        if(id is not(0x50 or 0x51 or 0x52))throw new NotSupportedException($"Ganon PART${id:x2} is not implemented.");
        World=world;Id=id;Subid=subid;ParentSlot=parent;ZFixed=(z>>8)<<8;Angle=angle;Speed=speed;Var03=var03;
        var visual=EnemyCharacterConfiguration.FromImported(world.Data.Actor(id));
        if(id==0x51&&subid==1)visual=visual with {TileBase=visual.TileBase+14};
        _palette=visual.Palette;
        InitializeEnemy(position.Floor(),visual,positionedOam:true,paletteVariants:[_palette,5]);
        Visible=false;ZIndex=ObjectDrawPriority.FromVisible(0x82);
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public void ClearHealthAndCollision()=>throw new NotSupportedException($"PART${Id:x2} has no represented health-clear collision.");
    private void HighPosition(Vector2 position)=>Position=position.Floor()+Position-Position.Floor();
    private int Dec(){if(Counter!=0)Counter--;return Counter;}
    private void Move()=>Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        try
        {
            // Split children have a zero related pointer in the source. Its
            // state read aliases the clean ROM's JR NC opcode at $0004.
            var parent=ParentSlot<0?null:World.Entities.ResolveEnemySlot(ParentSlot) as GanonRoomEntity;
            int parentState=ParentSlot<0?World.Data.Bytes("null-related-state")[0]:parent?.Actor.State??-1;
            if(parentState==0x0e){Finish();return;}
            if(ParentSlot>=0&&parent is null)throw new NotSupportedException($"PART${Id:x2}: related ENEMY slot${ParentSlot:x2} has no Ganon state owner.");
            if(Id==0x50){Trident(parent!.Actor);return;}
            if(Id==0x51){LargeBallPart(frame);return;}
            switch(Subid)
            {
                case 0:
                    if(State==0){State=1;Counter=10;Visible=true;return;}
                    if(State==1)
                    {
                        if(Dec()!=0){AdvanceAnimation();return;}
                        State=2;World.Entities.OnSoundRequested(SoundId.SndBeam);RestartAnimation(2);
                    }
                    Flight();return;
                case 1:SplittingShot();return;
                case 2:
                    switch(State)
                    {
                        case 0:State=1;Counter=15;Visible=true;return;
                        case 1:
                            if(Dec()!=0){AdvanceAnimation();return;}
                            Counter=15;State=2;World.Entities.OnSoundRequested(SoundId.SndVeranFairyAttack);RestartAnimation(1);return;
                        case 2:
                            if(Dec()!=0){AdvanceAnimation();return;}
                            State=3;Speed=0x5a;Angle=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),frame.Player.Position.Floor());RestartAnimation(2);return;
                        case 3:Flight();return;
                    }
                    break;
            }
            throw new NotSupportedException($"PART$52:${Subid:x2} state${State:x2}.");
        }
        finally{_pending=-1;AdvanceInvincibilityCounter();QueueRedraw();}
    }
    private void Flight()
    {
        if(!NativePartBoundary.IsInside(World.Entities.ActiveRoom,Position,Angle,World.Data.Bytes("part-boundaries"))){Finish();return;}
        Move();AdvanceAnimation();
    }
    private void SplittingShot()
    {
        if(State==0)
        {
            Speed=0x50;Visible=true;
            if(Var03!=0){State=3;SetNativeCollisionRadii(2,2);return;}
            State=1;Counter=40;SetNativeCollisionRadii(4,4);Center=new((int)Position.X,((int)Position.Y+32)&255);RestartAnimation(1);
        }
        switch(State)
        {
            case 1:
                if(Dec()!=0)
                {
                    if((Counter&1)==0)Angle=(Angle+1)&31;
                    Visible=!Visible;var delta=MovementCircleArcOffset(8,Angle);
                    HighPosition(new((Center.X+delta.X)&255,(Center.Y+delta.Y)&255));AdvanceAnimation();return;
                }
                Counter=10;State=2;World.Entities.OnSoundRequested(SoundId.SndVeranProjectile);Visible=true;AdvanceAnimation();return;
            case 2:
                if(Dec()!=0){Move();AdvanceAnimation();return;}
                State=3;SetNativeCollisionRadii(2,2);RestartAnimation(0);World.Entities.TryCreateVirePuff(Position,CollisionZ,0);
                foreach(int delta in new[]{-3,3})
                    if(World.Entities.PartSlotAvailable)World.Entities.AddEntity(new GanonAttackPart(World,0x52,1,-1,Position,ZFixed,(Angle+delta)&31,var03:1));
                return;
            case 3:Flight();return;
        }
        throw new NotSupportedException($"PART$52:01 state${State:x2}.");
    }
    private void Trident(GanonCharacter parent)
    {
        if(State==0)
        {State=1;HighPosition(parent.Position);ZFixed=(parent.ZFixed>>8)<<8;RestartAnimation(parent.SlashSide!=0?1:0);return;}
        if(State==1)
        {
            AdvanceAnimation();
            if(AnimationParameter==255){State=2;SetNativeCollisionRadii(7,7);Visible=false;}
            else
            {
                if((AnimationParameter&128)!=0){Animation.SetParameter(AnimationParameter&127);Visible=true;World.Entities.OnSoundRequested(SoundId.SndBigSword);}
                int at=AnimationParameter;var table=World.Data.Bytes("trident");
                if(at+3>=table.Length)throw new NotSupportedException($"ganonTrident.s: radius/offset index${at:x2}.");
                SetNativeCollisionRadii(table[at+1],table[at]);
                int x=unchecked((sbyte)table[at+3]);if(parent.SlashSide!=0)x=-x;
                HighPosition(new(((int)parent.Position.X+x)&255,((int)parent.Position.Y+unchecked((sbyte)table[at+2]))&255));
                ZFixed=(parent.ZFixed>>8)<<8;return;
            }
        }
        if(parent.Substate==0){Finish();return;}
        HighPosition(new((int)parent.Position.X,((int)parent.Position.Y+32)&255));ZFixed=(parent.ZFixed>>8)<<8;
    }
    public void SetLinkSwordState(SwordActionState state,int level,int? collision=null)=>_sword=collision??SwordCollision.Type(state,level);
    public bool ApplySwordHit(Rect2 bounds,Vector2 origin,int damage,EnemyKnockbackStrength strength,ICollection<RoomEntitySpawn> spawns)=>Hit(_sword,bounds,origin);
    public bool ApplyItemCollision(RoomEntityItemCollision item,Rect2 bounds,Vector2 origin,int damage,ICollection<RoomEntitySpawn> spawns)=>Hit((int)item,bounds,origin);
    private bool Hit(int item,Rect2 bounds,Vector2 origin)
    {
        MeleeReportsContact=false;_attackerRecoil=0;
        if(!CollisionEnabled||_pending>=0||InvincibilityCounter!=0||!World.Data.Active(true,Id,item)||!RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds,bounds))return false;
        int effect=World.Data.Effect(Id==0x50?0x7b:6,item);if(effect==0)return true;
        if(effect!=0x16)throw new NotSupportedException($"PART${Id:x2}: item${item:x2}, effect${effect:x2}.");
        _pending=item;InvincibilityCounter=-28;MeleeReportsContact=true;
        _attackerRecoil=EnemyBehaviorTables.Shared.ArmoredSwordAttackerKnockback.NormalFrames;
        World.Entities.TryCreateBoomerangClink(BoomerangCollisionResponse.Midpoint(Position,origin),0);
        World.Entities.OnSoundRequested(SoundId.SndBombLand);return true;
    }
    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength,out SwordAttackerKnockback response)
    {response=new(Position,_attackerRecoil);bool hit=_attackerRecoil!=0;_attackerRecoil=0;return hit;}
    public bool ApplySomariaBlockCollision(SomariaBlock block,ICollection<RoomEntitySpawn> spawns)
    {
        if(Id!=0x50||!CollisionEnabled||_pending>=0||InvincibilityCounter!=0||!block.CollisionEnabled||
            !RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ,block.ZHigh,7)||!RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds,block.CollisionBounds))return false;
        block.Flags|=0x20;return true;
    }
    public void HandleLinkContact(Player player)
    {
        if(!CollisionEnabled||_pending>=0||InvincibilityCounter!=0||!player.EnemyContactHeightOverlaps(CollisionZ))return;
        if(Id==0x50&&player.CanAcceptShieldCollision&&World.Data.Active(true,Id,player.Inventory.ShieldLevel)&&
            RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds,player.ShieldCollisionBounds))
        {player.ApplyShieldCollisionRecoil(Position,15,19);_pending=player.Inventory.ShieldLevel;World.Entities.OnSoundRequested(SoundId.SndBombLand);return;}
        if(player.OverlapsEnemyCollision(CollisionBounds,CollisionZ)&&player.ApplyEnemyContactDamage(Position,World.Data.Actor(Id).DamageQuarters,
            RingDamageSource.Generic,Id==0x50?42:34,Id==0x50?21:15))_pending=0;
    }
}
