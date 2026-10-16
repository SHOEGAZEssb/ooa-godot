using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// PART $56:$00-$03. Each native relatedObj1 is resolved from its live pool;
// the hanging strand uses the enemy pointer, the shot's segments a PART pointer.
internal sealed partial class VeranSpiderwebRoomEntity : EnemyCharacter,IRoomEntity,IFixedRoomEntity,
    IRoomEntityLifetime,IPostObjectLinkContactRoomEntity,INativePartHealthRoomEntity,IObjectCollisionHeightRoomEntity
{
    private readonly RoomEntityManager _owner;
    private readonly VeranFinalDatabase _data;
    private readonly int _parentSlot;
    private int _z;
    private bool _collision,_hit,_caught;
    internal int SubId {get;}
    internal int Slot {get;set;}
    internal int State {get;private set;}
    internal int Counter {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Angle {get;private set;}
    internal int Speed {get;private set;}
    internal Vector2I Target {get;private set;}
    internal int ZFixed=>_z;
    internal bool Caught=>_caught;
    internal override bool InitializationPending=>State==0;
    internal override bool CollisionEnabled=>!IsDead&&_collision;
    protected override Vector2 AnimationDrawOffset=>base.AnimationDrawOffset+Vector2.Down*(_z>>8);
    public Node2D Node=>this;
    public bool Finished=>IsDead;
    public int CollisionZ=>_z>>8;
    internal VeranSpiderwebRoomEntity(RoomEntityManager owner,VeranFinalDatabase data,int subid,int parentSlot,Vector2 position,int z,int angle=0)
    {
        _owner=owner;_data=data;SubId=subid;_parentSlot=parentSlot;_z=(z>>8)<<8;Angle=angle;
        InitializeEnemy(position.Floor(),EnemyCharacterConfiguration.FromImported(data.Actor(0x56)),positionedOam:true);Visible=false;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public void ClearHealthAndCollision(){Health=0;_collision=false;}
    private VeranFinalCharacter? Parent=>_owner.ResolveEnemySlot(_parentSlot)?.Node as VeranFinalCharacter;
    private VeranSpiderwebRoomEntity? ParentPart=>_owner.ResolvePartSlot(_parentSlot) as VeranSpiderwebRoomEntity;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(_hit)
        {
            _hit=false;frame.Player.CopyVeranSpiderGrabPosition(new(Position.X,((int)Position.Y-6)&255),_z>>8);_caught=true;
            // The status prelude writes counter1 AND counter2, then the
            // ordinary state1 tail turns the shot around on this dispatch.
            if(State==1){Counter=Counter2=1;}
        }
        switch(SubId)
        {
            case 0:
                if(State==0){State=1;Counter=(Counter+1)&255;Show(ObjectDrawPriority.FixedHighPriorityZIndex);_owner.OnSoundRequested(SoundId.SndBeam2);return;}
                if(Dec()==0){Counter=4;Spawn(2,_parentSlot);}
                var parent=Parent;
                if(parent is null||parent.SubId!=1){Puff();return;}
                _z=unchecked((short)(_z-0x400));
                int y=((int)Position.Y+(_z>>8)+(int)_owner.ToScreen(Vector2.Zero).Y)&255;
                if(y>=176){parent.SignalWebArrived(false);Finish();}return;
            case 1:Shot(frame);return;
            case 2:
                if(State==0){State=1;RestartAnimation(2);Show(ObjectDrawPriority.FixedHighPriorityZIndex);return;}
                var boss=Parent;
                if(boss is not null&&boss.Visible&&(((_z>>8)-1)&255)<(((boss.ZFixed>>8)-1)&255))return;
                Finish();return;
            case 3:
                var shot=ParentPart;
                if(State==0)
                {
                    if(shot is null)throw new NotSupportedException("veranSpiderweb.s:$56:$03 initialization has a missing related PART.");
                    State=1;int angle=((shot.Angle-12)&255);angle=((angle>>2)|(angle<<6))&255;
                    RestartAnimation((angle+1)&255);Show(ObjectDrawPriority.InFrontOfLinkZIndex);return;
                }
                if(shot is not null&&Position.Y<shot.Position.Y)return;Finish();return;
            default:throw new NotSupportedException($"veranSpiderweb.s: PART $56:${SubId:x2}.");
        }
    }
    private void Shot(RoomEntityFrame frame)
    {
        var boss=Parent;
        if(boss is null||boss.SubId!=1||boss.StunCounter!=0){Puff();return;}
        switch(State)
        {
            case 0:
                State=1;Counter=1;Counter2=5;_collision=true;Speed=0x50;
                Position=new(Position.X,(((int)Position.Y+16)&255)+Position.Y%1);Target=(Vector2I)Position.Floor();
                int relative=OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),frame.Player.Position.Floor());Angle=relative<14?12:relative<19?16:20;
                Show(ObjectDrawPriority.FixedLowPriorityZIndex);_owner.OnSoundRequested(SoundId.SndBeam2);return;
            case 1:
                if(Dec()==0)
                {
                    Counter=8;Counter2=(Counter2-1)&255;
                    if(Counter2==0){TurnBack();return;}
                    Spawn(3,Slot);
                }
                Vector2 point=Position.Floor();
                if(_owner.ActiveRoom.GetTerrainInfo(point).Collision!=255&&!_owner.ActiveRoom.IsSolidForEnemyMovement(point,holesAreWalls:false)){Move();return;}
                TurnBack();return;
            case 2:if(Dec()==0)State=3;return;
            case 3:
                Move();if(_caught)frame.Player.CopyVeranSpiderGrabPosition(new(Position.X,((int)Position.Y-6)&255),_z>>8);
                if((((int)Position.Y-Target.Y+2)&255)>=5||(((int)Position.X-Target.X+2)&255)>=5)return;
                boss.SignalWebArrived(_caught);if(!_caught){Finish();return;}State=4;return;
            case 4:
                if(frame.Player.EnemyGrabSubstate==2){Finish();return;}
                Position=new(frame.Player.Position.X+Position.X%1,(((int)frame.Player.Position.Y+6)&255)+Position.Y%1);
                _z=(frame.Player.EnemyContactZ<<8)|(_z&255);return;
            default:throw new NotSupportedException($"veranSpiderweb.s:$56:$01 state ${State:x2}.");
        }
    }
    private void TurnBack(){State=2;Counter=30;Speed=0x3c;Angle^=16;}
    private void Spawn(int subid,int parent)
    {if(_owner.PartSlotAvailable)_owner.AddEntity(new VeranSpiderwebRoomEntity(_owner,_data,subid,parent,Position,_z));}
    private int Dec()=>Counter=Counter==0?0:Counter-1;
    private void Move()=>Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
    private void Show(int priority){Visible=true;ZIndex=priority;}
    private void Puff(){_owner.TryCreateVirePuff(Position,_z>>8,0);Finish();}
    public void HandleLinkContact(Player player)
    {
        if(!CollisionEnabled||_hit||!player.OverlapsEnemyCollision(CollisionBounds,CollisionZ))return;
        if(player.RequestLikeLikeGrab())_hit=true;
    }
}
