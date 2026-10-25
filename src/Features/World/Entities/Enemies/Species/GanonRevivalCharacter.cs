using Godot;
using System;

namespace Oracles;

// ENEMY $60 never increments state: common graphics/RNG initialization runs
// on EVERY visit, including during the controller's BG-only palette fade.
internal sealed partial class GanonRevivalCharacter : EnemyCharacter
{
    internal GanonEnvironment World {get;}
    internal int Subid {get;}
    internal int ParentSlot {get;}
    internal int Initialized {get;private set;}
    internal int Counter {get;private set;}
    internal int Counter2 {get;private set;}
    internal int Interval {get;private set;}
    internal int Speed {get;private set;}
    internal int Angle {get;private set;}
    private Func<int,IRoomEntity?> _resolve=null!;
    internal override bool InitializationPending=>true;
    internal override bool CollisionEnabled=>false;
    internal void Bind(Func<int,IRoomEntity?> resolve)=>_resolve=resolve;
    internal GanonRevivalCharacter(GanonEnvironment world,int subid,int parent,Vector2 position,int angle)
    {
        World=world;Subid=subid;ParentSlot=parent;Angle=angle;
        InitializeEnemy(position,EnemyCharacterConfiguration.FromImported(world.Data.Actor(0x60)),positionedOam:true);
        Visible=false;Animation.SetFrameCounter(0);ZIndex=ObjectDrawPriority.FromVisible(0x83);
    }
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if(IsDead)return;
        World.Entities.Random.Next();RestartAnimation(0);
        if(Subid==0)
        {
            if(Initialized==0)
            {
                if(World.Entities.PaletteFadeActiveSource())return;
                Initialized=1;Interval=40;World.StatusBar(false);World.Fade(true,14,true);
            }
            if(Counter2!=0)Counter2--;
            if(Counter2!=0)return;
            int previous=Counter;Counter=(Counter+1)&255;
            if(previous>=16)
            {
                if(_resolve(ParentSlot) is not GanonRoomEntity parent)throw new NotSupportedException($"ganonRevivalCutscene_controller: related ENEMY slot${ParentSlot:x2} has no counter1 owner.");
                parent.Actor.Counter=(parent.Actor.Counter+1)&255;Finish();return;
            }
            World.Entities.TryAllocateEnemy(_=>
            {
                var table=World.Data.Bytes("shadows");int at=(Counter&7)*3;
                return new GanonRevivalRoomEntity(new(World,1,0,new(table[at+1],table[at]),table[at+2]));
            });
            Counter2=Interval;if(Interval-4>=16)Interval-=4;return;
        }
        if(Initialized==0){Initialized=1;Speed=0x50;Visible=true;World.Entities.OnSoundRequested(SoundId.SndWind);}
        if((((int)Position.X-0x78+8)&255)<17&&(((int)Position.Y-0x54+8)&255)<17){Finish();return;}
        if((frame.Counter&7)==0)Angle=OracleObjectMath.NudgeAngleTowards(Angle,OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),new(0x78,0x54)));
        Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),Speed,Angle).PrecisePosition;
        Visible=!Visible;QueueRedraw();
    }
}
