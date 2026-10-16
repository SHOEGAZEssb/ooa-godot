using Godot;
using System;

namespace oracleofages;

public partial class Player
{
    private int _ambiState=-1,_ambiCounter,_ambiAngle;
    private bool _ambiRequested;
    private Action? _ambiExclaim;
    internal void RequestAmbiUnpossessedCutscene(Action exclaim) { _ambiRequested=true; _ambiExclaim=exclaim; }
    internal void CancelAmbiUnpossessedCutscene()
    {
        if(_ambiState>=0) {_topDownAirZFixed=0;_topDownAirSpeedZ=0;_scriptedLinkAnimationMode=null;_walking=false;}
        if(_state08ReleasedByObjectMask) {_state08ReleasedByObjectMask=false;_forcedState08Phase=0;_scriptedLinkAnimationMode=null;}
        _ambiRequested=false;_ambiState=-1;_ambiExclaim=null;_cutsceneDrawZFixed=0;
    }
    private bool AdvanceAmbiUnpossessedCutscene()
    {
        if(_ambiRequested) {_ambiRequested=false;_ambiState=0;return true;}
        if(_ambiState<0)return false;
        // LINK_STATE_AMBI_UNPOSSESSED_CUTSCENE $09 does not test Link's
        // ordinary text/input masks. Boundary handlers intentionally fall
        // through and decrement the next state's freshly assigned counter.
        switch(_ambiState)
        {
            case 0: _ambiState=1;_ambiCounter=12;_ambiAngle=8;Face(Vector2I.Left);goto case 1;
            case 1:
                if(--_ambiCounter!=0)break;
                _ambiState=2;_ambiCounter=12;_ambiAngle=24;goto case 2;
            case 2:
                if(--_ambiCounter!=0)break;
                _ambiState=3;_ambiCounter=50;Face(Vector2I.Down);goto case 3;
            case 3:
                if(--_ambiCounter!=0)return true;
                _ambiState=4;_ambiCounter=30;_ambiAngle=16;Face(Vector2I.Up);_ambiExclaim?.Invoke();return true;
            case 4:
                if(--_ambiCounter!=0)return true;
                _ambiState=5;_topDownAirSpeedZ=-0x180;SetScriptedLinkAnimationMode(0x18);_world.PlaySound(SoundId.SndJump);return true;
            case 5:
                if(OracleObjectMath.UpdateSpeedZ(ref _topDownAirZFixed,ref _topDownAirSpeedZ,0x18))
                {
                    _ambiState=-1;_ambiExclaim=null;_cutsceneDrawZFixed=0;
                    // Source changes directly to state08/substate0, rather
                    // than requesting it through checkLinkForceState.
                    _forcedState08Phase=2;_state08ReleasedByObjectMask=true;return true;
                }
                _cutsceneDrawZFixed=(_topDownAirZFixed>>8)<<8;break;
            default:throw new NotSupportedException($"link.s: LINK_STATE $09 substate ${_ambiState:x2}.");
        }
        _walking=true;
        if(_ambiState==5)AdvanceTopDownAirAnimation(TopDownAirDatabase.Shared.Parameters);
        else AdvanceLinkWalkAnimation();
        // specialObjectUpdatePositionWithoutTileEdgeAdjust supplies a zero
        // wall mask but still publishes getPositionOffsetForVelocity's
        // shared wTmpcec0 bytes through the ordinary movement owner.
        SetScriptedPosition(_precisePosition+_world.ResolveNativeMovement(_precisePosition,0x28,_ambiAngle,
            allowWallSlide:false,adjacentWalls:0));
        return true;
    }
}
