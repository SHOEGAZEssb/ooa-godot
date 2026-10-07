using System;

using Godot;

namespace oracleofages;

public partial class Player
{
    internal bool FacesTileWallForInteraction
    {
        get
        {
            // checkFacingBottomOfTile reads the preceding adjacentWalls
            // publication, requiring both probes on the selected side.
            int mask=FacingVector == Vector2I.Up ? 0xc0 : FacingVector == Vector2I.Right ? 0x03 :
                FacingVector == Vector2I.Down ? 0x30 : 0x0c;
            return (_tilePushWalls&mask) == mask;
        }
    }
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
            if (_getItemStatePhase >= 2 || _forcedRespawnPhase >= 2 || _forcedRoomEntryMovement || _ledgeJumpState != LedgeJumpState.None ||
                _deathAnimationActive || EnemyGrabActive || WallmasterGrabActive || CollapsedActive || _squishAnimation is not null ||
                GaleActive && _galePending < 2 || _forcedState08Phase >= 2 ||
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
                _companionRideControlled || _raftRideControlled)
                throw new NotSupportedException("INTERAC$33 Link state gate reached a control mode whose native state boundary is not represented.");
            // Pending death/grab/gale/state08 requests do not change state
            // until Link consumes them. Shock, normal item use, knockback,
            // swimming, jumping and minecart riding are owned by linkState01.
            // Minecart movement stays in w1Companion; Link can consume a
            // forced state02 request while that cart keeps updating.
            return true;
        }
    }

    internal void RequestWallSquish(int pushAngle, string source)
    {
        try
        {
            if (!NativeNormalStateForInteraction) return;
            // These owners publish wLinkForceState before Link consumes it.
            if (_enemyGrabRequested || _galePending == 2 || _forcedState08Phase == 1 || _sideScrollSquishPending ||
                _topDownDrownPhase == 1 || _forcedRespawnPhase == 1 || _getItemStatePhase == 1)
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
