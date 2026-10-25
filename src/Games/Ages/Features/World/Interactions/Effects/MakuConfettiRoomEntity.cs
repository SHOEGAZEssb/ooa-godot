using Godot;
using System.Collections.Generic;

namespace Oracles;

// INTERAC $62:$00. Unlike the remote-Maku presentation, these flowers and
// their $84:$02 sparkles participate in the native interaction slot walk.
internal sealed partial class MakuConfettiRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEntityManager _owner;
    private readonly MakuConfettiDatabase _data;
    internal int Var03 {get;}
    internal int State {get;private set;}
    internal int SpawnCount {get;private set;}
    internal int Delay {get;private set;}
    internal int SoundCounter {get;private set;}
    internal int SparkleCounter {get;private set;}
    internal int Direction {get;private set;}
    internal MakuConfettiStep Motion {get;private set;}
    public Node2D Node=>this;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal MakuConfettiRoomEntity(RoomEntityManager owner,MakuConfettiDatabase data,int var03=0,int soundCounter=0)
    {
        _owner=owner;_data=data;Var03=var03;SoundCounter=soundCounter;Visible=false;
        InitializeVisual(data.Visual,Vector2.Zero);ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(State==0)
        {
            State=1;
            if(Var03==0){Delay=_data.Delays[0];return;}
            State=2;var source=_data.Pieces[Var03-1];Vector2 camera=-_owner.ToScreen(Vector2.Zero);
            Motion=new(unchecked((short)((source[0]+(int)camera.Y)<<8)),unchecked((short)((source[1]+(int)camera.X)<<8)),0,0,source[2],source[3],true);
            SparkleCounter=_data.SparkleInitial;Present();Visible=true;return;
        }
        if(State==1)
        {
            Delay=(Delay-1)&255;if(Delay!=0)return;Delay=1;
            if(!_owner.InteractionSlotAvailable)return;
            _owner.AddEntity(new MakuConfettiRoomEntity(_owner,_data,++SpawnCount,_data.SoundCounter));
            _owner.OnSoundRequested(_data.Sound);
            if(SpawnCount==_data.Pieces.Length){Finish();return;}
            Delay=_data.Delays[SpawnCount];return;
        }
        SoundCounter=(SoundCounter-1)&255;if(SoundCounter==0){SoundCounter=_data.SoundCounter;_owner.OnSoundRequested(_data.Sound);}
        SparkleCounter=(SparkleCounter-1)&255;
        if(SparkleCounter==0)
        {
            SparkleCounter=_data.SparkleRepeat;
            if(_owner.InteractionSlotAvailable)_owner.Spawn(new InteractionSparkleSpawn(new((byte)(Motion.X>>8),(byte)(Motion.Y>>8)),0,_data.Sparkle,Subid:2));
        }
        var m=Motion;
        Motion=MakuConfettiMotion.Advance(m.Y,m.X,m.SpeedY,m.SpeedX,m.AccelerationY,m.AccelerationX,_data.YOffset,_data.YLimit,_data.XLimit,_data.DeleteY);
        Present();if(!Motion.Alive){Finish();return;}
        int direction=Motion.SpeedX<0?1:0;
        if(direction!=Direction){Direction=direction;SetAnimation(Direction);}
    }
    private void Present()
    {
        int y=(byte)(Motion.Y>>8);if(y>=0xd8)y-=256;
        Position=new((byte)(Motion.X>>8),y);
    }
    private void Finish(){Finished=true;Visible=false;}
}
