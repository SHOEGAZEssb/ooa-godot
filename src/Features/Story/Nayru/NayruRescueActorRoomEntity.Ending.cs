namespace oracleofages;

internal sealed partial class NayruRescueActorRoomEntity
{
    private int _endingJump;
    private bool EndingCompanion=>Id==0x36&&SubId==3||Id==0x37&&SubId==5;
    private bool EndingPalaceCompanion=>Id==0x36&&SubId==4||Id==0x37&&SubId==6;
    private void InitializeEndingPalaceCompanion()
    {
        bool farewell=Read(0xcfd0)==0x0b;
        if(farewell)SetPosition(new(Id==0x36?0x40:0x50,0x48));
        if(!(farewell&&IsLinkedGame&&Id==0x36))Start((Id==0x36?"nayru-palace":"ralph-palace")+(farewell?"-farewell":""));SetAnimation(0);
    }
    private void UpdateEndingPalaceCompanion()
    {
        AnimateMotion();
        if(Id==0x36){AdvanceScript();return;}
        switch(Substate)
        {
            case 0:
                PublishTarget();
                if(Read(0xcfd0)==8){BeginEndingJump();return;}
                AdvanceScript();return;
            case 1:
                if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20))return;
                Substate=2;_endingJump=(_endingJump+1)&255;AdvanceScript();return;
            case 2:AdvanceScript();return;
            default:throw UnsupportedCommand($"palace ending substate ${Substate:x2}");
        }
    }
    private void BeginEndingJump()
    {_speedZ=-0x1c0;_context.Sound.PlaySound(SoundId.SndJump);Substate++;}
    private void UpdateEndingCompanion()
    {
        if(Substate<2)_actor.AdvanceAnimationUpdates(1);else AnimateMotion();
        switch(Substate)
        {
            case 0:if(Read(0xcfd0)==1)BeginEndingJump();return;
            case 1:
                if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,Id==0x36?0x24:0x20))return;
                Start(Id==0x36?"nayru-ending":"ralph-ending");Substate=2;return;
            case 2:
                if(Id==0x37&&Read(0xcfd0)==2){BeginEndingJump();return;}
                AdvanceScript();return;
            case 3:
                if(!OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x20))return;
                Substate=4;_endingJump=(_endingJump+1)&255;AdvanceScript();return;
            case 4:AdvanceScript();return;
            default:throw UnsupportedCommand($"ending substate ${Substate:x2}");
        }
    }
}
