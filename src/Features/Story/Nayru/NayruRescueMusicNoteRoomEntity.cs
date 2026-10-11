using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NayruRescueMusicNoteRoomEntity : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly NpcCharacter _actor;
    private readonly Vector2 _velocity;
    private int _counter=70;
    private bool _initialized;
    public Node2D Node => _actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    private NayruRescueMusicNoteRoomEntity(EffectRecord effect,Vector2 position,bool left)
    {
        _actor=NpcCharacter.CreateFromRecord(effect.ToNpcRecord(0,0x38,(int)position.Y,(int)position.X));
        _actor.SetAnimationRate(0);_actor.SetBlocksLink(false);_actor.SetScriptAnimation(effect.Animation);
        _velocity=new((left?-1:1)*effect.VelocityXFixed/256f,effect.VelocityYFixed/256f);
    }
    internal static void Spawn(RoomEventContext context,Vector2 position,bool left)
    {if(context.Entities.InteractionSlotAvailable)context.Entities.AddEntity(new NayruRescueMusicNoteRoomEntity(new SharedEffectDatabase().Effect("MusicNote"),position,left));}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized) {_initialized=true;_actor.ShowInitializedPresentation();return;}
        if(--_counter==0) {Finished=true;_actor.SetActive(false);return;}
        _actor.Position+=_velocity;
        if((frame.Counter&7)==0) _actor.Position+=Vector2.Right*new[]{-1,-2,-1,0,1,2,1,0}[(frame.Counter>>3)&7];
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
}
