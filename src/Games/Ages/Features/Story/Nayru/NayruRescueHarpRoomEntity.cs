using Godot;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_PLAY_HARP_SONG $c5:$02. The completion signal is written by this
// actor's dispatch, independently of the script's wait for cfc0 bit 7.
internal sealed class NayruRescueHarpRoomEntity(RoomEventContext context) : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime
{
    public Node2D Node {get;}=new();
    public bool Finished {get;private set;}
    private int _state,_counter;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(_state==0) {context.Player.RequestState08Control(this);Node.Position=context.Player.Position;_counter=4;_state=1;return;}
        if(_state==1)
        {if(--_counter!=0)return;_counter=52;_state=2;context.Player.BeginHarpPose();context.Sound.PlaySound(SoundId.SndTuneOfAges);return;}
        if(_state==6)
        {context.Entities.RuntimeState.SetWramByte(0xcfc0,(byte)(context.Entities.RuntimeState.ReadWramByte(0xcfc0)|0x80));context.Player.EndHarpPose();Finished=true;return;}
        if((context.Entities.FrameCounter&31)==0)NayruRescueMusicNoteRoomEntity.Spawn(context,Node.Position+new Vector2((_state&1)==0?-8:8,-8),(_state&1)==0);
        context.Player.AdvanceHarpPose();if(--_counter==0) {_counter=52;_state++;}
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
