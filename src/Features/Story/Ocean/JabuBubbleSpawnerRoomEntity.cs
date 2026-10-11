using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class JabuBubbleSpawnerRoomEntity(RoomEntityManager entities,JabuExteriorDatabase data)
    : Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
      IUpdatesDuringRoomEntityFreeze
{
    private int _state,_counter,_remaining;
    public Node2D Node=>this;
    public bool Finished{get;private set;}
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(_state==0)
        {
            _state=1;_counter=30;_remaining=4;
            for(int i=11;i>=0;i--)Spawn(i);
            return;
        }
        if(--_counter!=0)return;
        _counter=18;
        if(--_remaining==0){Finished=true;return;}
        Spawn((entities.Random.Next().Value&3)+12);
    }
    private void Spawn(int index)
    {
        var bytes=data.Bytes("bubble-positions");
        if(entities.PartSlotAvailable)entities.AddEntity(new JabuBubbleRoomEntity(new(bytes[index*3+1],bytes[index*3]),bytes[index*3+2],data,entities.Random));
    }
    public void SetTransitionDrawOffset(Vector2 offset){ }
}
