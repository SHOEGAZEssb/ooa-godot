using Godot;
using System.Collections.Generic;
namespace oracleofages;
internal sealed class TargetCartDebrisRoomEntity(NpcCharacter actor,Vector2 position,int direction)
    :RoomEntityAdapter<NpcCharacter>(actor,actor.SetTransitionDrawOffset),IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity
{
    private bool _initialized;
    public bool Finished=>!Entity.Active;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized) { _initialized=true; Entity.SetAnimationRate(0); Entity.Position=position; return; }
        if(Entity.CurrentAnimationParameter==0xff) { Entity.SetActive(false); return; }
        Entity.AdvanceAnimationUpdates(1);
        Entity.Position=OracleObjectMovement.Shared.ApplySpeed(ref position,0x28,4+direction*8);
    }
}
