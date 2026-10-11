using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// miscellaneous2.s:$dc:$09/$0a retain their native interaction slots.
// Only the $0a handoff belongs to CUTSCENE_JABU_OPEN's separate owner.
internal sealed partial class JabuExteriorActorRoomEntity(byte subid, JabuExteriorDatabase data,
    RoomEntityManager entities, RoomSession rooms) : Node2D, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IScreenTransitionPreloadRoomEntity
{
    private int _state, _phase, _counter;
    public Node2D Node=>this;
    public bool Finished{get;private set;}
    public bool UpdatesDuringDialogue=>_state==0;
    internal byte SubId=>subid;
    internal Action? TriggerOpening{get;set;}
    internal int State=>_state;
    internal int Phase=>_phase;
    internal int Counter=>_counter;
    private void Initialize()
    {
        _state=1;
        if(subid==0x0a)Finished=rooms.SaveData.HasRoomFlag(data.Group,data.Room,2);
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();Visible=false;
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(Finished)return;
        if(subid==0x0a)
        {
            if(_state==0)
            {
                Initialize();
                return;
            }
            Finished=true;
            if(rooms.SaveData.HasGlobalFlag(GlobalFlag.GotPermissionToEnterJabu))
                (TriggerOpening ?? throw new InvalidOperationException("INTERAC $dc:$0a has no cutscene handoff."))();
            return;
        }
        if(subid!=9)throw new InvalidOperationException($"miscellaneous2.s: unsupported Jabu actor $dc:${subid:x2}.");
        if(_state==0){Initialize();return;}
        if(_state==1)
        {
            if(entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled)!=0||(entities.FrameCounter&7)!=0)return;
            var random=entities.Random.Next();
            if((random.Value&7)!=0)return;
            _state=(random.High&1)==0?2:3;_phase=0;
            return;
        }
        if(_phase!=0&&--_counter!=0)return;
        if(_phase==(_state==2?2:3)){_state=1;return;}
        int index=_state==2?_phase:2+_phase;
        var bytes=data.Bytes($"idle-{index}");
        for(int i=0;i<bytes.Length;i+=2)rooms.TrySetTile(bytes[i],bytes[i+1]);
        _counter=_state==2?8:12;_phase++;
    }
    public void SetTransitionDrawOffset(Vector2 offset){ }
}
