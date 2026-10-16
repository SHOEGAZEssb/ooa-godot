using Godot;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_VERAN_CUTSCENE_WALLMASTER $2c, not ENEMY_WALLMASTER.
internal sealed class VeranCutsceneWallmasterRoomEntity : IRoomEntity,IFixedRoomEntity,IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEventContext _context;
    private readonly BlackTowerEscapeDatabase _data;
    private readonly NpcCharacter _actor;
    private int _z,_speedZ;
    internal int State {get;private set;}
    internal int Substate {get;private set;}
    internal int Counter {get;private set;}
    internal int ZFixed=>_z;
    internal int SpeedZ=>_speedZ;
    public Node2D Node=>_actor;
    public bool Finished {get;private set;}
    public bool UpdatesDuringDialogue=>State==0;
    public bool UpdatesDuringRoomEntityFreeze=>State==0;
    internal VeranCutsceneWallmasterRoomEntity(RoomEventContext context,BlackTowerEscapeDatabase data,Vector2 position)
    {_context=context;_data=data;_actor=data.Wallmaster(position);}
    public void SetTransitionDrawOffset(Vector2 offset)=>_actor.SetTransitionDrawOffset(offset);
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(State==0)
        {
            State=1;_speedZ=0x140;_z=-0x6000;Counter=20;
            // objectSetVisiblec3: show the hand above the floor with terrain effects.
            _actor.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex,withTerrainEffects:true);
        }
        _actor.AdvanceAnimationUpdates(1);
        switch(Substate)
        {
            case 0:if(--Counter==0)Substate=1;break;
            case 1:
                if(OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0)){_actor.SetScriptAnimation(_data.Animation(1));Substate=2;}
                break;
            case 2:
                int parameter=_actor.CurrentAnimationParameter;
                if((parameter&0x80)!=0){Substate=3;break;}
                if(parameter==0)break;
                _context.Player.Visible=false;Counter=30;_context.Sound.PlaySound(SoundId.SndBossDead);break;
            case 3:
                if(Counter!=0){Counter--;break;}
                _z=unchecked((short)(_z-0x100));
                if((_z>>8)==-80){_context.Entities.RuntimeState.SetWramByte(0xcbb5,8);Finished=true;_actor.SetActive(false);}
                break;
        }
        _actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));
    }
}
