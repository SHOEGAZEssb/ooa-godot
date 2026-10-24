using Godot;
using System.Collections.Generic;

namespace oracleofages;

// common/interactions/c1.s. Its word hold and six-update sparkle clock are
// independent; children retain checked allocation and native slot dispatch.
internal sealed class CreditsFlyingSpriteRoomEntity(RoomEventContext context,BlackTowerEndingDatabase data,Vector2 position) :
    IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly NpcCharacter _actor=data.Actor(0xc1,0,position);
    private Vector2 _position=position;
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int SparkleCounter {get;private set;}
    internal NpcCharacter Actor=>_actor;
    internal Vector2 PrecisePosition=>_position;
    public Node2D Node=>_actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;Counter=390;SparkleCounter=6;
            _actor.SetScriptAnimation(_actor.Record.UpAnimation);_actor.ShowInitializedPresentation();_actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);return;
        }
        if(Substate==0)
        {
            Counter=System.Math.Max(0,Counter-1);
            if(Counter==0){Counter=40;Substate=1;}return;
        }
        if(Substate==2)_actor.AdvanceAnimationUpdates(1);
        if(--SparkleCounter==0)
        {
            SparkleCounter=6;
            if(context.Entities.InteractionSlotAvailable)
            {
                var visual=data.ActorRecord(0x84,9);
                context.Entities.Spawn(new InteractionSparkleSpawn(_actor.Position.Floor(),0,
                    new(visual.SpriteName,visual.TileBase,visual.Palette,visual.UpAnimation),9));
            }
        }
        _actor.Position=NativeObjectMovement.ApplySpeed(context.Entities.RuntimeState,ref _position,0x78,0x15);
        if(Substate==1)
        {
            Counter=(Counter-1)&255;
            if(Counter==0){Substate=2;_actor.SetAnimationCounter(1);}
        }
        else if(_actor.CurrentAnimationParameter==255)Finished=true;
    }
}
