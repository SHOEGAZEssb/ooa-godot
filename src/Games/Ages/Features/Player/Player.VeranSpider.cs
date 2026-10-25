using Godot;

namespace Oracles;

public partial class Player
{
    private int _enemyGrabReleaseCounter;
    internal void ReleaseVeranSpiderGrab()
    {if(EnemyGrabActive){_enemyGrabSubstate=2;_enemyGrabCollisionDisabled=false;}}
    internal void CopyVeranSpiderGrabPosition(Vector2 position,int z)
    {CopyLikeLikePosition(position);_topDownAirZFixed=(z<<8)|(_topDownAirZFixed&255);}
    internal void ApplyVeranSpiderGrabDamage(int raw,int invincibility)
    {_pendingContactDamageRaw=raw;_enemyInvincibilityFrames=invincibility;}
    private void StartGrabRelease()
    {
        _enemyGrabSubstate=3;_enemyGrabReleaseCounter=30;_topDownAirSpeedZ=-0x1e0;Face(Vector2I.Up);
        _galeFrames??=new NewGameIntroDatabase().SpriteFrames("link-gale");_galeFrame=0;_galeFrameTicks=_galeFrames[0].Duration;
        SetCutsceneSpriteFrame(_galeFrames[0]);
    }
    private void AdvanceGrabRelease()
    {
        if(--_enemyGrabReleaseCounter==0)
        {
            _enemyGrabSubstate=-1;_world.SetNativeWarpsDisabled(false);_walking=false;ResetLinkWalkAnimation();
            SetCutsceneSpriteFrame(null);return;
        }
        OracleObjectMath.UpdateSpeedZ(ref _topDownAirZFixed,ref _topDownAirSpeedZ,0x20);
        _topDownAirborne=_topDownAirZFixed<0;
        ApplyTopDownObjectSpeed(0x3c,16,allowWallSlide:true,allowLedgeHop:false);Position=OracleObjectMath.ToPixelPosition(_precisePosition);
        if(--_galeFrameTicks==0){_galeFrame=(_galeFrame+1)%_galeFrames!.Length;_galeFrameTicks=_galeFrames[_galeFrame].Duration;}
        SetCutsceneSpriteFrame(_galeFrames![_galeFrame]);
    }
}
