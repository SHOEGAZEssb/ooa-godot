using System.Collections.Generic;

namespace Oracles;

// Native $95 dispatch around the same ball physics used by ordinary $1:49.
internal sealed class NativeBallRoomEntity(Room149Ball ball,OracleRuntimeState runtime)
    : RoomEntityAdapter<Room149Ball>(ball,ball.SetTransitionDrawOffset),IFixedRoomEntity,
        IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    internal int State {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0){State=1;Entity.SetActive(true);return;}
        if(Entity.Idle)
        {
            int signal=runtime.ReadWramByte(0xcfd3);
            if(signal!=0)Entity.Launch(signal);
            return;
        }
        Entity.UpdateFrame();if(Entity.Idle)runtime.SetWramByte(0xcfd3,0);
    }
}
