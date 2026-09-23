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
            if (_deathAnimationActive || EnemyGrabActive || _squishAnimation is not null ||
                GaleActive && !_galePending || _forcedState08Phase >= 2 ||
                _sideScrollInstantRespawnCounter != 0 || _instantRespawnRecoveryCounter != 0)
                return false;
            // These controllers currently combine native request, state and
            // presentation phases. Do not guess their state from groundedness.
            if (_world.SideScrolling || _world.IsTransitioning ||
                _forcedRoomEntryMovement || _spinnerControlled ||
                _cutsceneControlled && _forcedState08Phase == 0 ||
                _getItemOneHandPose || _getItemTwoHandPose ||
                _ledgeJumpState != LedgeJumpState.None || _newGameSlowFalling ||
                _roomWarpFallActive || _roomWarpFallCollapsed ||
                _drowning || _fallingInHole || _hazardRecoveryTime > 0 ||
                _floorDoorRespawnCounter != 0 || _floorDoorRecoveryCounter != 0 ||
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
            if (_enemyGrabRequested || _galePending || _forcedState08Phase == 1 || _sideScrollSquishPending)
                return;
            if (_deathPending)
                throw new NotSupportedException("Pending death versus retained forced-state lifetime is not represented.");
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
            if (_world.SideScrolling || _ledgeJumpState != LedgeJumpState.None ||
                _companionJumpControlled || _minecartJumpControlled ||
                _newGameSlowFalling || _roomWarpFallActive)
                throw new NotSupportedException("INTERAC$33 wLinkInAir gate reached an unrepresented airborne control mode.");
            // linkUpdateInAir adopts negative zh only when Link runs. A
            // frozen scripted lift must not change the existing in-air byte.
            return _topDownAirborne;
        }
    }
}
