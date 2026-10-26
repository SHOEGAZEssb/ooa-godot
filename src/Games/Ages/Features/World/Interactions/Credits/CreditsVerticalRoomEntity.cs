using Godot;
using System.Collections.Generic;

namespace Oracles;

// INTERAC $af directly submits text OAM during its interaction update. It has
// no ordinary visible sprite; each native credits dispatch clears that OAM.
internal sealed class CreditsVerticalRoomEntity(RoomEventContext context,CreditsVerticalDatabase data,EndingScreen screen,
    bool child=false,int line=0,Vector2 position=default) : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private Vector2 _precise=position;
    internal bool Child=>child;
    internal int Line {get;private set;}=line;
    internal int State {get;private set;}
    internal int Wait {get;private set;}
    internal Vector2 Position=>_precise;
    public Node2D Node {get;}=new(){Visible=false};
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset) {}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        screen.RemoveObjectOam(this);
        if(State==0){State=1;if(!child)Wait=data.Wait(0);return;}
        if(context.Transitions.NativePaletteFadeActive)return;
        if(child)
        {
            NativeObjectMovement.ApplySpeed(context.Entities.RuntimeState,ref _precise,0x14,0);
            if((int)_precise.Y==0){Finished=true;return;}
            screen.SetObjectOam(this,data.Line(Line),(int)_precise.Y,(int)_precise.X,context.Entities.InteractionSlot(Node));return;
        }
        if(Wait!=0)Wait--;
        if(Wait!=0)return;
        if(context.Entities.InteractionSlotAvailable)
            context.Entities.AddEntity(new CreditsVerticalRoomEntity(context,data,screen,true,Line,_precise.Floor()));
        Line=(Line+1)&255;Wait=data.Wait(Line);
        if((Wait&255)!=255)return;
        context.Entities.RuntimeState.SetWramByte(0xcfdf,255);Finished=true;
    }
}
