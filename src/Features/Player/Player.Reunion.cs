using Godot;
using System;

namespace oracleofages;

public partial class Player
{
    internal void BeginCreditsReunion(object owner,Vector2 position,Action createKissHearts)
    {
        BeginCreditsMaku(owner,position);_creditsReunion=true;_createKissHearts=createKissHearts;
    }
    private bool ReunionShieldEquipped=>_inventory.EquippedA==TreasureId.Shield||_inventory.EquippedB==TreasureId.Shield;
    private void AdvanceReunionLink()
    {
        // ages/specialObjects/linkInCutscene.s: linkCutsceneA. Shield state
        // is sampled again at signal7; it is not cached at construction.
        if(_creditsLinkState==0)
        {
            _creditsLinkState=1;Visible=false;CreditsLinkAnimationSet(ReunionShieldEquipped?0x0f:0x0b);return;
        }
        var runtime=_inventory.RuntimeState;int signal=runtime.ReadWramByte(0xcfc0);
        switch(_creditsLinkSubstate)
        {
            case 0:if(signal==1){_creditsLinkSubstate=1;Visible=true;}return;
            case 1:
                if(signal!=7)return;
                _creditsLinkSubstate=2;CreditsLinkAnimationSet(ReunionShieldEquipped?0x11:0x10);return;
            case 2:
                if(signal!=8)return;
                _creditsLinkSubstate=3;_creditsLinkCounter=360;_createKissHearts!();CreditsLinkAnimationSet(0x12);return;
            case 3:
                CreditsLinkAnimate();if(_creditsLinkCounter!=0)_creditsLinkCounter--;
                if(_creditsLinkCounter==0)runtime.SetWramByte(0xcfc0,9);return;
            default:throw new NotSupportedException($"linkCutsceneA: substate${_creditsLinkSubstate:x2}.");
        }
    }
}
