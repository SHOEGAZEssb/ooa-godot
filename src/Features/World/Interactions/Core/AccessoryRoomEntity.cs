using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_ACCESSORY $63 with var03=1: bowties and carried balls share
// the original parent-animation attachment table and deletion dispatch.
internal sealed class AccessoryRoomEntity(NpcCharacter actor,NpcCharacter parent,Func<bool> finished,
    Func<int> zFixed,Func<int,string> animation,int[] offsets,Action<NpcCharacter>? palette=null)
    : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private int _animation;
    internal int State {get;private set;}
    public Node2D Node=>actor;
    public bool Finished=>finished();
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset)=>actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        State=1;
        if(Finished)return;
        actor.ShowInitializedPresentation();actor.Visible=parent.Visible;
        if(!actor.Visible)return;
        int index=parent.CurrentAnimationParameter*4;
        if(index+3>=offsets.Length)throw new InvalidOperationException($"accessory.s:$63:${actor.Record.SubId:x2} parent animParameter ${parent.CurrentAnimationParameter:x2}.");
        if(_animation!=offsets[index+3]){_animation=offsets[index+3];actor.SetScriptAnimation(animation(_animation));}
        actor.SetStatePosition(new(((int)parent.Position.X+unchecked((sbyte)offsets[index+1]))&255,
            ((int)parent.Position.Y+unchecked((sbyte)offsets[index]))&255));
        palette?.Invoke(actor);
        actor.SetFixedDrawPriority(ObjectDrawPriority.FromVisible(offsets[index+2]));
        actor.SetScriptDrawOffset(Vector2.Down*(zFixed()>>8));
    }
}
