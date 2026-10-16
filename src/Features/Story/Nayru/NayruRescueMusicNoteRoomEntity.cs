using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NayruRescueMusicNoteRoomEntity : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly NpcCharacter _actor;
    private readonly OracleRuntimeState _memory;
    private readonly int _angle;
    private Vector2 _precise;
    private int _counter=70;
    private bool _initialized;
    public Node2D Node => _actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    internal Vector2 PrecisePosition=>_precise;
    private NayruRescueMusicNoteRoomEntity(EffectRecord effect,Vector2 position,bool left,OracleRuntimeState memory)
    {
        _actor=NpcCharacter.CreateFromRecord(effect.ToNpcRecord(0,0x38,(int)position.Y,(int)position.X));
        _actor.SetAnimationRate(0);_actor.SetBlocksLink(false);_actor.SetScriptAnimation(effect.Animation);
        _precise=position;_angle=left?0x1d:3;_memory=memory;
    }
    internal static void Spawn(RoomEventContext context,Vector2 position,bool left)
    {if(context.Entities.InteractionSlotAvailable)context.Entities.AddEntity(new NayruRescueMusicNoteRoomEntity(new SharedEffectDatabase().Effect("MusicNote"),position,left,context.Entities.RuntimeState));}
    internal static void Spawn(RoomEntityManager entities, Vector2 position, bool left)
    { if (entities.InteractionSlotAvailable) entities.AddEntity(new NayruRescueMusicNoteRoomEntity(new SharedEffectDatabase().Effect("MusicNote"), position, left, entities.RuntimeState)); }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(!_initialized) {_initialized=true;_actor.SetFixedDrawPriority(ObjectDrawPriority.FixedHighPriorityZIndex);return;}
        if(--_counter==0) {Finished=true;_actor.SetActive(false);return;}
        _actor.Position=NativeObjectMovement.ApplySpeed(_memory,ref _precise,0x0f,_angle);
        if((frame.Counter&7)==0)
        {
            var position=OracleObjectPosition.FromPixels(_precise).Add(0,new[]{-1,-2,-1,0,1,2,1,0}[(frame.Counter>>3)&7]*256);
            _precise=position.PrecisePosition;_actor.Position=position.PixelPosition;
        }
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
}
