using Godot;

namespace oracleofages;

public partial class MapleEncounter
{
    private RoomEventContext? _creditsContext;
    internal int CreditsState {get;private set;}
    internal int CreditsLap {get;private set;}
    internal void InitializeCredits(RoomEventContext context,Vector2 position)
    {
        _creditsContext=context;_encounter=new();_database=new();_precisePosition=position;Position=position.Floor();
        InitializeVisuals();
        _animation.SetFrameCounter(0);_animation.SetPaletteOverride(context.Entities.NativeObjectPalettes);
        ZIndex=ObjectDrawPriority.FixedHighPriorityZIndex;Visible=false;
    }
    internal void UpdateCredits(int globalFrame)
    {
        _globalFrameCounter=globalFrame&255;
        if(CreditsState==0){CreditsState=1;_zFixed=-0x1000;_angle=8;_counter=90;Visible=true;QueueRedraw();return;}
        _animation.Advance();
        if((_globalFrameCounter&7)==0)
            _zFixed=unchecked((short)(_zFixed+(_database.CreditsZOffsets[(_globalFrameCounter>>3)&7]<<8)));
        switch(_substate)
        {
            case 0:
                if(!_creditsContext!.Transitions.NativePaletteFadeActive&&DecrementCredits()) {_substate=1;LoadCreditsFlight();}
                break;
            case 1:
                if(DecrementCredits()){_counter=90;CreditsLap=(CreditsLap+1)&255;_substate=2;}
                else NativeObjectMovement.ApplySpeed(_creditsContext!.Entities.RuntimeState,ref _precisePosition,_speedRaw,_angle);
                break;
            case 2:
                if(!DecrementCredits())break;
                if(CreditsLap==4){_counter=30;_substate=3;SetAnimation(7);}
                else{_substate=1;_angle^=0x10;LoadCreditsFlight();}
                break;
            case 3:
                if(DecrementCredits())_creditsContext!.Entities.RuntimeState.SetWramByte(0xcfdf,255);
                else OracleObjectMath.UpdateSpeedZ(ref _zFixed,ref _speedZ,2);
                break;
            default:throw new System.NotSupportedException($"companionCutscene.s: SPECIALOBJECT $12 substate ${_substate:x2}.");
        }
        Position=_precisePosition.Floor();QueueRedraw();
    }
    private bool DecrementCredits(){_counter=(_counter-1)&255;return _counter==0;}
    private void LoadCreditsFlight()
    {
        var row=_database.CreditsFlight[CreditsLap];_speedRaw=row.Speed;_counter=row.Frames;
        _precisePosition.Y=row.Y;SetAnimation(row.Animation);
    }
}
