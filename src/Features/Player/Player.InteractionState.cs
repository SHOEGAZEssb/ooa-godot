using System;

namespace oracleofages;

public partial class Player
{
    // INTERAC$33/$dc read w1Link.state, not whether movement/input is enabled.
    // Resolve represented owners without manufacturing a second state byte.
    internal bool NativeNormalStateForInteraction
    {
        get
        {
            // checkLinkJumpingOffCliff enters state12 immediately, including
            // its pre-scroll and waiting phases; landing restores state01.
            // BossEntryMovement arms separately and sets this flag only when
            // consuming the request. State0b restores state01 on counter zero.
            if (_getItemStatePhase >= 2 || _floorDoorRespawnPhase >= 2 || _forcedRoomEntryMovement || _ledgeJumpState != LedgeJumpState.None ||
                _deathAnimationActive || EnemyGrabActive || _squishAnimation is not null ||
                GaleActive && !_galePending || _forcedState08Phase >= 2 ||
                _sideScrollInstantRespawnCounter != 0 || _instantRespawnRecoveryCounter != 0 ||
                _fallingInHole || _drowning && _topDownDrownPhase >= 2)
                return false;
            // linkState01_sidescroll also owns jumping and swimming. Its
            // drowning handler changes state only at the terminal frame;
            // the following dispatch initializes state02's respawn phase.
            if (_world.SideScrolling && (_sideScrollDrownRespawnPending || _drownRespawning))
                return false;
            // These controllers currently combine native request, state and
            // presentation phases. Do not guess their state from groundedness.
            if (_world.IsTransitioning ||
                _spinnerControlled ||
                _cutsceneControlled && _forcedState08Phase == 0 && _getItemStatePhase == 0 ||
                _getItemOneHandPose || _getItemTwoHandPose ||
                _newGameSlowFalling ||
                _roomWarpFallActive || _roomWarpFallCollapsed ||
                _drowning && !_world.SideScrolling && _topDownDrownPhase == 0 ||
                _companionRideControlled || _minecartRideControlled || _raftRideControlled)
                throw new NotSupportedException("INTERAC$33 Link state gate reached a control mode whose native state boundary is not represented.");
            // Pending death/grab/gale/state08 requests do not change state
            // until Link consumes them. Shock, normal item use, knockback,
            // swimming and jumping are all owned by linkState01.
            return true;
        }
    }

    internal void RequestWallSquish(int pushAngle, string source)
    {
        try
        {
            if (!NativeNormalStateForInteraction) return;
            // These owners publish wLinkForceState before Link consumes it.
            if (_enemyGrabRequested || _galePending || _forcedState08Phase == 1 || _sideScrollSquishPending ||
                _topDownDrownPhase == 1 || _floorDoorRespawnPhase == 1 || _getItemStatePhase == 1)
                return;
            // interactiondc_subid17 does not read wLinkDeathTrigger. A lethal
            // hit after Link's update may therefore leave this request pending.
            // linkState01 checks death before checkLinkForceState: dying wins
            // without consuming the request (AdvancePhysics preserves it too).
            _sideScrollSquishVertical = (pushAngle & 8) == 0;
            _sideScrollSquishPending = true;
        }
        catch (NotSupportedException error)
        {
            throw new NotSupportedException($"INTERAC $dc:$17 at {source}: {error.Message}", error);
        }
    }

    internal bool NativeInAirForInteraction
    {
        get
        {
            // State12 retains wLinkInAir=$81 through both halves of a scroll.
            if (_ledgeJumpState != LedgeJumpState.None) return true;
            if (_world.SideScrolling ||
                _companionJumpControlled || _minecartJumpControlled ||
                _newGameSlowFalling || _roomWarpFallActive)
                throw new NotSupportedException("INTERAC$33 wLinkInAir gate reached an unrepresented airborne control mode.");
            // linkUpdateInAir adopts negative zh only when Link runs. A
            // frozen scripted lift must not change the existing in-air byte.
            return _topDownAirborne;
        }
    }
}
