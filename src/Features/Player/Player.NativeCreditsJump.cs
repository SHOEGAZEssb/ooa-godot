namespace oracleofages;

public partial class Player
{
    // companionCutscene.s:Ricky substate6 writes normal Link's live air
    // registers before his update. Keep the normal air/terrain owner active.
    internal void BeginNativeCreditsJump()
    {
        _topDownAirSpeedZ=-0x200;_topDownAirborne=true;_topDownJumpSoundPending=true;
        _topDownMovementSpeedRaw=0x14;_topDownMovementAngle=0x18;
        _topDownAirAnimationPhase=0;_topDownAirAnimationCounter=TopDownAirDatabase.Shared.Parameters.AnimationPhaseDurations[0];
        _airborneLinkAnimationMode=AirborneLinkAnimationMode.Jump;
    }
}
