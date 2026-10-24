using Godot;

namespace oracleofages;

public partial class Player
{
    private int _creditsLinkState=-1,_creditsLinkSubstate,_creditsLinkCounter,_creditsLinkSpeedZ;
    private int _creditsLinkAnimation,_creditsLinkFrame,_creditsLinkTicks,_creditsLinkDirection;
    private bool _creditsReunion;
    private bool _ritesReaction;
    private System.Action? _createRitesMark;
    internal void BeginRitesReaction(object owner,Vector2 position,System.Action createMark)
    {BeginCreditsMaku(owner,position);_ritesReaction=true;_createRitesMark=createMark;}
    private System.Action? _createKissHearts;
    internal int CreditsLinkState=>_creditsLinkState;
    internal int CreditsLinkSubstate=>_creditsLinkSubstate;
    internal int CreditsLinkCounter=>_creditsLinkCounter;
    internal int CreditsLinkAnimation=>_creditsLinkAnimation;
    internal int CreditsLinkAnimationCounter=>_creditsLinkTicks;
    internal int CreditsLinkSpeedZ=>_creditsLinkSpeedZ;
    internal void BeginCreditsMaku(object owner,Vector2 position)
    {
        BeginCutsceneControl(owner:owner);SetScriptedPosition(position);_creditsLinkState=0;_creditsLinkSubstate=0;
        _creditsReunion=false;_ritesReaction=false;_createKissHearts=null;_createRitesMark=null;
        _creditsLinkCounter=_creditsLinkSpeedZ=_creditsLinkDirection=0;SetCutsceneDrawZFixed(0);
    }
    internal void CancelCreditsLink()
    {
        if(_creditsLinkState<0)return;
        _creditsLinkState=-1;_creditsReunion=false;_ritesReaction=false;_createKissHearts=null;_createRitesMark=null;SetCutsceneSpriteFrame(null);SetCutsceneDrawZFixed(0);EndCutsceneControl();
    }
    private void CreditsLinkAnimationSet(int animation)
    {
        _creditsLinkAnimation=animation;_creditsLinkFrame=0;
        var frame=CreditsLinkDatabase.Shared.Frames(animation)[0];_creditsLinkTicks=frame.Duration;SetCutsceneSpriteFrame(frame);
    }
    private void CreditsLinkAnimate()
    {
        _creditsLinkTicks=(_creditsLinkTicks-1)&255;if(_creditsLinkTicks!=0)return;
        var frames=CreditsLinkDatabase.Shared.Frames(_creditsLinkAnimation);
        if(++_creditsLinkFrame>=frames.Length)_creditsLinkFrame=CreditsLinkDatabase.Shared.Loop(_creditsLinkAnimation);
        _creditsLinkTicks=frames[_creditsLinkFrame].Duration;SetCutsceneSpriteFrame(frames[_creditsLinkFrame]);
    }
    private bool AdvanceCreditsLink()
    {
        if(_creditsLinkState<0)return false;
        if(_ritesReaction)
        {
            if(_creditsLinkState==0)
            {
                _creditsLinkState=1;Visible=true;CreditsLinkAnimationSet(6);
                _createRitesMark!();
            }
            return true;
        }
        if(_creditsReunion){AdvanceReunionLink();return true;}
        if(_creditsLinkState==0)
        {_creditsLinkState=1;CreditsLinkAnimationSet(2);Visible=false;return true;}
        var runtime=_inventory.RuntimeState;int signal=runtime.ReadWramByte(0xcfc0);
        switch(_creditsLinkSubstate)
        {
            case 0:if(signal==1){_creditsLinkSubstate=1;Visible=true;}return true;
            case 1:if(signal!=3)return true;_creditsLinkSubstate=2;goto case 2;
            case 2:
                if(signal==6)
                {_creditsLinkSubstate=3;_creditsLinkSpeedZ=-0x1c0;CreditsLinkAnimationSet(0x0d);return true;}
                // Link precedes the entity manager's counter increment, as
                // does the existing companion special-object prepass.
                if(((_world.FrameCounter+1)&7)==0)
                {
                    int direction=((OracleObjectMovement.Shared.RelativeAngle(_precisePosition.Floor(),new(runtime.ReadWramByte(0xcfd6),runtime.ReadWramByte(0xcfd5)))+4)&0x18)>>3;
                    if(direction!=_creditsLinkDirection){_creditsLinkDirection=direction;CreditsLinkAnimationSet(direction);}
                }
                return true;
            case 3:
                if(OracleObjectMath.UpdateSpeedZ(ref _topDownAirZFixed,ref _creditsLinkSpeedZ,0x20))
                {_creditsLinkSubstate=4;_creditsLinkCounter=120;_creditsLinkTicks=1;}
                _cutsceneDrawZFixed=(_topDownAirZFixed>>8)<<8;QueueRedraw();return true;
            case 4:
                _creditsLinkCounter=(_creditsLinkCounter-1)&255;
                if(_creditsLinkCounter!=0)CreditsLinkAnimate();else runtime.SetWramByte(0xcfdf,255);
                return true;
            default:throw new System.NotSupportedException($"linkInCutscene.s: linkCutscene9 substate ${_creditsLinkSubstate:x2}.");
        }
    }
}
