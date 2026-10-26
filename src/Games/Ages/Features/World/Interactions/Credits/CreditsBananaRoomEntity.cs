using Godot;
using System.Collections.Generic;

namespace Oracles;

// common/banana.s:$c0. Construction only writes relatedObj1; the first
// initialized frame remains at zero, before taking Moosh's high XYZ bytes.
internal sealed class CreditsBananaRoomEntity(BlackTowerEndingDatabase data,CompanionCreditsRoomEntity parent) :
    IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    internal NpcCharacter Actor {get;}=data.Actor(0xc0,0,Vector2.Zero);
    internal int State {get;private set;}
    internal int Direction {get;private set;}
    public Node2D Node=>Actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset)=>Actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0){State=1;Actor.SetScriptAnimation(Actor.Record.UpAnimation);Actor.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex);return;}
        Actor.AdvanceAnimationUpdates(1);if(parent.Id!=0x11){Finished=true;return;}
        if(Direction!=parent.Direction)
        {Direction=parent.Direction;Actor.SetFixedDrawPriority(Direction==2?ObjectDrawPriority.FixedHighPriorityZIndex:ObjectDrawPriority.FixedLowPriorityZIndex);Actor.SetScriptAnimation(data.Animation(0xc0,Direction));}
        int offset=Direction switch{1=>5,3=>-5,_=>0};
        Actor.Position=new(((int)parent.Position.X+offset)&255,(int)parent.Position.Y);
        Actor.SetScriptDrawOffset(new(0,parent.ZFixed>>8));
    }
}
