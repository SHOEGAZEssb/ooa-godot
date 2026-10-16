using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_MISC_PUZZLES $90:$21. Palette threading remains with the existing
// transition/fade owner; this interaction only starts it and emits explosions.
internal sealed class VeranDeathFadeRoomEntity(RoomEventContext context,VeranFinalDatabase data,
    Vector2 position,int z,Action<int> fade) : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public Node2D Node {get;}=new() {Name="VeranDeathFade_90_21",Position=position.Floor(),Visible=false};
    internal int State {get;private set;}
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset) {}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0){State=1;fade(4);return;}
        if(!context.Transitions.NativePaletteFadeActive){Finished=true;return;}
        if((frame.Counter&0x1f)!=0)return;
        int index=((frame.Counter&0x70)>>4)*2;var offsets=data.Bytes("death-explosions");
        Vector2 point=new(((int)Node.Position.X+unchecked((sbyte)offsets[index+1]))&255,
            ((int)Node.Position.Y+unchecked((sbyte)offsets[index]))&255);
        context.Entities.TryCreateInteractionExplosion(point,z,var03:0);
    }
}
