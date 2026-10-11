using Godot;
using System.Linq;

namespace oracleofages;

// INTERAC $dc:$09/$0a and CUTSCENE_JABU_OPEN $1d.
internal sealed class JabuExteriorEvent(RoomEventContext context):IRoomEvent,IRoomEntryEvent
{
    private readonly JabuExteriorDatabase _data=new();
    private int _state,_phase,_counter;
    public bool HasState{get;private set;}
    internal bool Opening{get;private set;}
    internal int OpeningState=>_state;
    internal int OpeningPhase=>_phase;
    internal int Counter=>_counter;
    public bool BlocksGameplay=>Opening;
    public bool OwnsGameLogic=>Opening;
    public bool DisablesLink=>Opening;
    public bool MenusDisabled=>Opening;
    public bool AllScreenTransitionsDisabled=>Opening;
    public bool Matches(int group,OracleRoomData room)=>group==_data.Group&&room.Id==_data.Room;
    public void Start(OracleRoomData room)
    {
        _state=_phase=_counter=0;
        var trigger=context.Entities.EntityAdapters<JabuExteriorActorRoomEntity>().SingleOrDefault(actor=>actor.SubId==0x0a);
        HasState=trigger is not null;
        if(trigger is not null)trigger.TriggerOpening=BeginOpening;
    }
    public void UpdateFrame()
    {
    }
    private void BeginOpening()
    {
        Opening=true;_state=0;
        Write(WramAddress.wDisabledObjects,0x81);Write(WramAddress.wMenuDisabled,0x81);
        context.Player.Face(Vector2I.Up);
    }
    private void Write(int address,int value)=>context.Entities.RuntimeState.SetWramByte(address,(byte)value);
    internal void AdvanceBeforeObjects()
    {
        if(!Opening)return;
        if(_state==0)
        {
            for(int address=0xcbb3;address<0xcbc3;address++)Write(address,0);
            context.RoomView.SuspendUnderwaterWave(true);
            context.Rooms.SaveData.SetRoomFlag(2,0x90,2);
            _counter=4;_phase=0;_state=1;
        }
        else if(_state==1)
        {
            if(_phase>=2)context.Entities.BeginScreenShake(60);
            if(--_counter!=0){Publish();return;}
            _counter=60;
            if(_phase==0)
            {
                context.Rooms.CurrentRoom.RegenerateBackgroundMappings(context.AnimationTick());
                context.RoomView.SuspendUnderwaterWave(false);
                context.RoomView.UpdateUnderwaterWave(context.Rooms.CurrentRoom,context.Entities.FrameCounter);
            }
            if(_phase==2)
            {
                if(context.Entities.InteractionSlotAvailable)context.Entities.AddEntity(new JabuBubbleSpawnerRoomEntity(context.Entities,_data));
                OpeningMap(1);
            }
            if(_phase==3){OpeningMap(2);_state=2;}
            if(_state==1)_phase++;
        }
        else if(--_counter==0)
        {
            context.Sound.PlaySound(SoundId.SndSolvePuzzle);
            Opening=false;Write(WramAddress.wDisabledObjects,0);Write(WramAddress.wMenuDisabled,0);
            context.Rooms.ReloadCurrentRoomLayout();
        }
        Publish();
    }
    private void Publish(){Write(0xcbb3,_phase);Write(0xcbb4,_counter);}
    private void OpeningMap(int index)
    {
        context.Rooms.CurrentRoom.SetBackgroundMappingRectangle(new(0,56),20,_data.Bytes($"opening-{index}"),context.AnimationTick());
        context.Sound.PlaySound(SoundId.SndDoorClose);
    }
    public void Cancel()
    {
        if(Opening){Write(WramAddress.wDisabledObjects,0);Write(WramAddress.wMenuDisabled,0);context.RoomView.SuspendUnderwaterWave(false);}
        HasState=Opening=false;
    }
}
