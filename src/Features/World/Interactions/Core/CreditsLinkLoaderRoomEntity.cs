using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC $a8:$64 / $04 initializes Link during the interaction pass, after
// his special-object update. The loader still occupies its native slot.
internal sealed class CreditsLinkLoaderRoomEntity(Player player,object owner,Vector2 position,Action<Vector2>? initialize=null) : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    public Node2D Node {get;}=new(){Visible=false};
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    public void SetTransitionDrawOffset(Vector2 offset) {}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {if(initialize is null)player.BeginCreditsMaku(owner,position);else initialize(position);Finished=true;}
}
