using Godot;
using System.Collections.Generic;

namespace Oracles;

// INTERAC_SNOWDEBRIS $09:$00 uses the common breakTileDebris handler.
internal sealed partial class SnowDebrisRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private bool _initialized;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>true;
    public bool UpdatesDuringRoomEntityFreeze=>true;
    public Node2D Node=>this;
    internal SnowDebrisRoomEntity(DungeonInteractionVisual visual,Vector2 position)
    {InitializeVisual(visual,position.Floor(),positionedOam:true);Visible=false;ZIndex=ObjectDrawPriority.FromVisible(0x83);}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(!_initialized){_initialized=true;Visible=true;return;}
        if((AnimationParameter&128)!=0){Finished=true;Visible=false;return;}
        AdvanceAnimation();
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset)=>SetTransitionDrawOffset(offset);
}
