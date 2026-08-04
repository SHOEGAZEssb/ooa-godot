using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRickyRiding()
    {
        RickyGlovesEventDatabase database =
            _roomEvents.RickyGloves.Database;
        RickyGlovesEventRecord record = database.Record;
        RickyCompanionBehaviorRecord behavior = database.Behavior;

        if (CompanionRuntimeState.AnyActive(_runtimeState))
        {
            ActiveCompanion active =
                CompanionRuntimeState.Read(_runtimeState);
            CompanionRuntimeState.Clear(_runtimeState, active.Id);
        }
        CompanionRuntimeState.ForgetRemembered(_runtimeState);
        _saveData.SetGlobalFlag(record.PrerequisiteGlobalFlag, true);
        _saveData.WriteWramByte(
            record.RickyStateAddress,
            checked((byte)record.CompleteMask));
        LoadValidationRoom(record.Group, record.Room);

        Vector2 mountPosition = new(80, 64);
        _player.WarpTo(mountPosition);
        var companion = _entities.Spawn<RickyCompanionRoomEntity>(
            new RickyCompanionSpawn(
                mountPosition,
                Direction: 1,
                record.Group,
                record.Room,
                Riding: true));
        CompanionRuntimeState.Begin(
            _runtimeState,
            CompanionRuntimeState.RickyId,
            record.Room,
            mountPosition,
            direction: 1);
        StepRickyApplicationInput(pressed: [], justPressed: []);
        FailIf(
            companion.Phase != RickyCompanionPhase.Riding ||
            !companion.LinkRiding || !_player.CompanionRideActive ||
            _player.PrecisePosition != companion.PrecisePosition ||
            !CompanionRuntimeState.IsActive(
                _runtimeState, CompanionRuntimeState.RickyId),
            "Ricky did not finish the ordinary state-$03 mount into the " +
            $"live ridden owner (phase={companion.Phase}, " +
            $"ride={_player.CompanionRideActive}, " +
            $"link={_player.PrecisePosition}, ricky={companion.PrecisePosition}).");
        // This focused harness spawns the post-event riding owner directly;
        // release cutscene control that LoadValidationRoom retained from the
        // room-$6a event setup so Player executes its ordinary mounted state.
        _player.EndCutsceneControl();

        Vector2 movementStart = companion.PrecisePosition;
        for (int update = 0; update < behavior.HopDelay; update++)
        {
            StepRickyApplicationInput(
                pressed: ["move_right"],
                justPressed: []);
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Riding ||
            !Mathf.IsEqualApprox(
                companion.PrecisePosition.X - movementStart.X,
                behavior.HopDelay * 0.75f) ||
            !Mathf.IsEqualApprox(
                companion.PrecisePosition.Y, movementStart.Y),
            "Ricky did not apply exactly `$10 grounded SPEED_c0 movement " +
            "updates before the periodic hop.");

        int jumpSounds =
            _sound.PlayRequestsFor(behavior.JumpSound) +
            _sound.PlayRequestsFor(record.RickySound);
        StepRickyApplicationInput(
            pressed: ["move_right"],
            justPressed: []);
        FailIf(
            companion.Phase != RickyCompanionPhase.Hopping ||
            companion.ZFixed != 0 ||
            companion.AnimationIndex != behavior.HopAnimation + 1 ||
            _sound.PlayRequestsFor(behavior.JumpSound) +
                _sound.PlayRequestsFor(record.RickySound) != jumpSounds + 1,
            "Ricky did not begin his -$0180/SPEED_200 normal hop with one " +
            "shared-RNG SND_JUMP/SND_RICKY choice on the update after `$10 " +
            $"(phase={companion.Phase}, z={companion.ZFixed}, " +
            $"animation=${companion.AnimationIndex:x2}, " +
            $"sounds={_sound.PlayRequestsFor(behavior.JumpSound) + _sound.PlayRequestsFor(record.RickySound) - jumpSounds}).");
        StepRickyApplicationInput(
            pressed: ["move_right"],
            justPressed: []);
        FailIf(
            companion.ZFixed >= 0 ||
            companion.PrecisePosition.X <= movementStart.X +
                behavior.HopDelay * 0.75f,
            "Ricky's hop did not attach Link to live Z motion and continue " +
            "at SPEED_200.");

        int groundedHopUpdates = 0;
        for (int update = 0; update < 120 &&
            companion.Phase == RickyCompanionPhase.Hopping; update++)
        {
            StepRickyApplicationInput(
                pressed: ["move_right"],
                justPressed: []);
            if (companion.ZFixed == 0)
                groundedHopUpdates++;
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Landing ||
            companion.ZFixed != 0 ||
            groundedHopUpdates != behavior.LandingDelay,
            "Ricky's `$40-gravity hop did not count exactly `$08 grounded " +
            $"substate-$01 updates before entering substate $03 " +
            $"(updates={groundedHopUpdates}).");
        StepRickyApplicationInput(pressed: [], justPressed: []);
        FailIf(
            companion.Phase != RickyCompanionPhase.Riding ||
            companion.AnimationIndex != behavior.IdleAnimation + 1,
            "Ricky did not return to the direction+$20 ridden pose on the " +
            "exact `$08 landing boundary.");

        int slashSounds =
            _sound.PlayRequestsFor(behavior.SwordSlashSound);
        int cueSounds = _sound.PlayRequestsFor(behavior.PunchCueSound);
        StepRickyApplicationInput(
            pressed: ["attack"],
            justPressed: ["attack"]);
        RickyPunchAttackRoomEntity punch =
            _entities.Entities<RickyPunchAttackRoomEntity>().Single();
        FailIf(
            companion.Phase != RickyCompanionPhase.Punching ||
            companion.AnimationIndex != behavior.PunchAnimation + 1 ||
            punch.Damage != behavior.PunchDamage ||
            punch.Counter != behavior.PunchLifetime ||
            punch.Position != companion.RidingLinkPosition +
                new Vector2(8, -2) ||
            _sound.PlayRequestsFor(behavior.SwordSlashSound) != slashSounds + 1,
            "Ricky's A press did not create ITEM_28, select direction+$09, " +
            "copy the mounted Link position/directional box, and play " +
            "SND_SWORDSLASH.");

        for (int update = 0; update < 40 &&
            companion.Phase != RickyCompanionPhase.Charging; update++)
        {
            StepRickyApplicationInput(
                pressed: ["attack"],
                justPressed: []);
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Charging ||
            companion.AnimationIndex != behavior.ChargeAnimation + 1 ||
            _sound.PlayRequestsFor(behavior.PunchCueSound) != cueSounds + 1,
            "Ricky's punch animation parameters did not emit SND_UNKNOWN5 " +
            "and enter the held-A tornado charge.");

        int chargeSounds = _sound.PlayRequestsFor(behavior.ChargeSound);
        while (companion.ChargeCounter < behavior.ChargeUpdates)
        {
            StepRickyApplicationInput(
                pressed: ["attack"],
                justPressed: []);
        }
        FailIf(
            companion.ChargeCounter != behavior.ChargeUpdates ||
            _sound.PlayRequestsFor(behavior.ChargeSound) != chargeSounds + 1,
            "Ricky's tornado charge did not reach `$1e and play " +
            "SND_CHARGE_SWORD on the exact held-A boundary.");
        bool sawChargePalette = false;
        bool sawNormalPalette = false;
        for (int update = 0; update < 8; update++)
        {
            StepRickyApplicationInput(
                pressed: ["attack"],
                justPressed: []);
            bool expected = (_entities.FrameCounter & 0x04) == 0;
            sawChargePalette |= expected;
            sawNormalPalette |= !expected;
            FailIf(
                companion.ChargePaletteActive != expected ||
                (companion.RickyTexturePixelHash !=
                    companion.NormalRickyTexturePixelHash) != expected ||
                companion.ChargeLinkTextureSelected != expected ||
                _player.CompanionRideTexturePixelHash !=
                    companion.LinkTexturePixelHash,
                "Ricky's fully charged palette `$02 did not flash both " +
                $"mounted sprites in global-frame bit-2 bands (frame=" +
                $"{_entities.FrameCounter}, expected={expected}, " +
                $"active={companion.ChargePaletteActive}, " +
                $"rickyChanged={companion.RickyTexturePixelHash != companion.NormalRickyTexturePixelHash}, " +
                $"chargeLink={companion.ChargeLinkTextureSelected}, " +
                $"playerMatch={_player.CompanionRideTexturePixelHash == companion.LinkTexturePixelHash}).");
        }
        FailIf(!sawChargePalette || !sawNormalPalette,
            "Ricky's charge sample did not cover both palette bands.");

        int spinSounds = _sound.PlayRequestsFor(behavior.SwordSpinSound);
        StepRickyApplicationInput(pressed: [], justPressed: []);
        RickyTornadoRoomEntity tornado =
            _entities.Entities<RickyTornadoRoomEntity>().Single();
        Vector2 tornadoStart = tornado.Position;
        FailIf(
            companion.Phase != RickyCompanionPhase.Punching ||
            _entities.Entities<RickyPunchAttackRoomEntity>().Count < 1 ||
            tornado.Damage != behavior.TornadoDamage ||
            _sound.PlayRequestsFor(behavior.SwordSpinSound) != spinSounds + 1,
            "Releasing a fully charged Ricky attack did not create " +
            "ITEM_RICKY_TORNADO, restart ITEM_28, and play SND_SWORDSPIN.");
        StepRickyApplicationInput(pressed: [], justPressed: []);
        FailIf(
            !Mathf.IsEqualApprox(
                tornado.Position.X - tornadoStart.X, 3.0f) ||
            !Mathf.IsEqualApprox(tornado.Position.Y, tornadoStart.Y),
            "ITEM_RICKY_TORNADO did not move right at SPEED_300 after its " +
            $"state-0 initialization update (start={tornadoStart}, " +
            $"end={tornado.Position}, finished={tornado.Finished}, " +
            $"direction={companion.Direction}).");

        for (int update = 0; update < 40 &&
            companion.Phase != RickyCompanionPhase.Riding; update++)
        {
            StepRickyApplicationInput(pressed: [], justPressed: []);
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Riding,
            "Ricky's post-tornado punch did not return to ridden movement " +
            "after A remained released.");

        StepRickyApplicationInput(
            pressed: ["attack"],
            justPressed: ["attack"]);
        RickyPunchAttackRoomEntity grassPunch =
            _entities.Entities<RickyPunchAttackRoomEntity>()[^1];
        Vector2 punchProbe = grassPunch.Position + new Vector2(8, 8);
        Vector2 grassCenter = new(
            Mathf.FloorToInt(punchProbe.X / OracleRoomData.MetatileSize) *
                OracleRoomData.MetatileSize + 8,
            Mathf.FloorToInt(punchProbe.Y / OracleRoomData.MetatileSize) *
                OracleRoomData.MetatileSize + 8);
        _currentRoom.SetPositionTileAndCollision(
            grassCenter, 0xc5, null, (long)_animationTicks);
        var breakables = new BreakableTileDatabase();
        if (!breakables.TryGet(
                _currentRoom.ActiveCollisions,
                0xc5,
                out BreakableTileRecord grassBreakable))
        {
            throw new InvalidOperationException(
                "Validation room has no bush $c5 breakable record.");
        }
        byte expectedGrassReplacement =
            grassBreakable.ReplacementFor(_currentRoom, grassCenter);
        StepRickyApplicationInput(pressed: [], justPressed: []);
        StepRickyApplicationInput(pressed: [], justPressed: []);
        IReadOnlyList<GrassDebrisEffect> grassDebris =
            _entities.Entities<GrassDebrisEffect>();
        FailIf(
            _currentRoom.GetMetatile(grassCenter) != expectedGrassReplacement ||
            grassDebris is not
                [{ Position: var debrisPosition, Flickers: false }] ||
            debrisPosition != grassCenter,
            "ITEM_28 did not replace bush $c5 and create the normal " +
            "tile-centered INTERAC_GRASSDEBRIS effect " +
            $"(tile=${_currentRoom.GetMetatile(grassCenter):x2}, " +
            $"expected=${expectedGrassReplacement:x2}, " +
            $"debris={grassDebris.Count}, punch={grassPunch.Position}, " +
            $"direction={companion.Direction}, counter={grassPunch.Counter}, " +
            $"probe={punchProbe}, center={grassCenter}, probes=" +
            $"${_currentRoom.GetMetatile(grassPunch.Position + new Vector2(8, -8)):x2}/" +
            $"${_currentRoom.GetMetatile(grassPunch.Position + new Vector2(-8, -8)):x2}/" +
            $"${_currentRoom.GetMetatile(grassPunch.Position + new Vector2(8, 8)):x2}/" +
            $"${_currentRoom.GetMetatile(grassPunch.Position + new Vector2(-8, 8)):x2}).");
        for (int update = 0; update < 40 &&
            companion.Phase != RickyCompanionPhase.Riding; update++)
        {
            StepRickyApplicationInput(pressed: [], justPressed: []);
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Riding,
            "Ricky did not return to riding after the grass-break punch.");
        StepRickyApplicationInput(
            pressed: ["move_right"],
            justPressed: []);
        FailIf(
            companion.AnimationFrameIndex == 0,
            "Ricky's ridden movement did not advance from the stationary " +
            "direction pose.");
        ulong rightFrameHash = companion.RickyTexturePixelHash;
        StepRickyApplicationInput(pressed: [], justPressed: []);
        FailIf(
            companion.AnimationFrameIndex != 0 ||
            companion.AnimationIndex != behavior.IdleAnimation + 1,
            "Ricky's stationary branch retained a moving OAM frame instead " +
            "of reselecting direction+$20 frame 0.");
        StepRickyApplicationInput(pressed: ["move_left"], justPressed: []);
        StepRickyApplicationInput(pressed: ["move_left"], justPressed: []);
        FailIf(
            rightFrameHash != 5676622319406760977UL ||
            companion.RickyTexturePixelHash != 8777466194033064437UL ||
            companion.AnimationIndex != behavior.IdleAnimation + 3 ||
            companion.AnimationFrameIndex != 1,
            "Ricky's right/left ridden frame-1 OAM did not resolve through " +
            "the retained $1060 source graphics load.");

        companion.SetScreenTransitionBoundaryCoordinate(
            horizontal: true, 80, _player);
        companion.SetScreenTransitionBoundaryCoordinate(
            horizontal: false, 96, _player);
        Vector2 cliffLeft = new(72, 88);
        Vector2 cliffRight = new(88, 88);
        byte cliffLeftTile = _currentRoom.GetMetatile(cliffLeft);
        byte cliffRightTile = _currentRoom.GetMetatile(cliffRight);
        byte cliffLeftCollision = _currentRoom.GetTerrainInfo(cliffLeft).Collision;
        byte cliffRightCollision = _currentRoom.GetTerrainInfo(cliffRight).Collision;
        _currentRoom.SetPositionTileAndCollision(
            cliffLeft, 0xa0, 0x03, (long)_animationTicks);
        _currentRoom.SetPositionTileAndCollision(
            cliffRight, 0xa0, 0x03, (long)_animationTicks);

        int rickyJumpSounds = _sound.PlayRequestsFor(record.RickySound);
        StepRickyApplicationInput(pressed: ["move_up"], justPressed: []);
        FailIf(
            companion.Phase != RickyCompanionPhase.JumpingUpCliff ||
            companion.AnimationIndex != behavior.LongJumpAnimation ||
            !companion.DisablesScreenTransitions,
            "Ricky did not recognize the source $c0 wall pair plus paired " +
            "$03 landing probes as a state-$02 upward cliff jump.");
        Vector2 cliffJumpStart = companion.PrecisePosition;
        for (int update = 0; update < behavior.LongJumpDelay - 1; update++)
            StepRickyApplicationInput(pressed: ["move_up"], justPressed: []);
        FailIf(
            companion.PrecisePosition != cliffJumpStart ||
            _sound.PlayRequestsFor(record.RickySound) != rickyJumpSounds,
            "Ricky moved or sounded before the first seven state-$02 delay " +
            "updates elapsed.");
        StepRickyApplicationInput(pressed: ["move_up"], justPressed: []);
        FailIf(
            companion.PrecisePosition.Y >= cliffJumpStart.Y ||
            companion.ZFixed >= 0 ||
            _sound.PlayRequestsFor(record.RickySound) != rickyJumpSounds + 1,
            "Ricky did not play SND_RICKY and apply -$0300/$40/SPEED_140 " +
            "motion on the exact eighth state-$02 update.");
        for (int update = 0; update < 120 &&
            companion.Phase == RickyCompanionPhase.JumpingUpCliff; update++)
        {
            StepRickyApplicationInput(pressed: ["move_up"], justPressed: []);
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Landing ||
            companion.ZFixed == 0 ||
            companion.DisablesScreenTransitions,
            "Ricky did not clear wDisableScreenTransitions as soon as the " +
            "state-$02 wall crossing completed while still airborne.");

        _currentRoom.SetPositionTileAndCollision(
            cliffLeft, cliffLeftTile, cliffLeftCollision,
            (long)_animationTicks);
        _currentRoom.SetPositionTileAndCollision(
            cliffRight, cliffRightTile, cliffRightCollision,
            (long)_animationTicks);
        for (int update = 0; update < 120 &&
            companion.Phase == RickyCompanionPhase.Landing; update++)
        {
            StepRickyApplicationInput(pressed: [], justPressed: []);
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Riding,
            "Ricky did not finish state-$05 substate-$03 after the upward " +
            "cliff wall crossing.");

        companion.SetScreenTransitionBoundaryCoordinate(
            horizontal: true, 80, _player);
        companion.SetScreenTransitionBoundaryCoordinate(
            horizontal: false, 64, _player);
        Vector2 downCliffLeft = new(72, 72);
        Vector2 downCliffRight = new(88, 72);
        byte downLeftTile = _currentRoom.GetMetatile(downCliffLeft);
        byte downRightTile = _currentRoom.GetMetatile(downCliffRight);
        byte downLeftCollision =
            _currentRoom.GetTerrainInfo(downCliffLeft).Collision;
        byte downRightCollision =
            _currentRoom.GetTerrainInfo(downCliffRight).Collision;
        _currentRoom.SetPositionTileAndCollision(
            downCliffLeft, 0x05, 0xff, (long)_animationTicks);
        _currentRoom.SetPositionTileAndCollision(
            downCliffRight, 0x05, 0xff, (long)_animationTicks);

        int downJumpSounds = _sound.PlayRequestsFor(behavior.JumpSound);
        StepRickyApplicationInput(pressed: ["move_down"], justPressed: []);
        FailIf(
            companion.Phase != RickyCompanionPhase.JumpingDownCliff ||
            companion.AnimationIndex != behavior.LongJumpAnimation + 2 ||
            companion.DisablesScreenTransitions,
            "Ricky did not route a source cliffTilesTable $05/$10 match " +
            "through state $07 with screen transitions enabled.");
        Vector2 downJumpStart = companion.PrecisePosition;
        for (int update = 0; update < behavior.LongJumpDelay; update++)
            StepRickyApplicationInput(pressed: ["move_down"], justPressed: []);
        FailIf(
            companion.PrecisePosition != downJumpStart ||
            _sound.PlayRequestsFor(behavior.JumpSound) != downJumpSounds + 1,
            "Ricky's immediate state-$07 jump did not hold for `$08 updates " +
            $"and emit SND_JUMP on the exact zero boundary (position=" +
            $"{companion.PrecisePosition}, start={downJumpStart}, delay=" +
            $"{companion.AirborneDelay}, sounds=" +
            $"{_sound.PlayRequestsFor(behavior.JumpSound) - downJumpSounds}, " +
            $"phase={companion.Phase}).");
        StepRickyApplicationInput(pressed: ["move_down"], justPressed: []);
        FailIf(
            companion.PrecisePosition.Y <= downJumpStart.Y ||
            companion.ZFixed >= 0,
            "Ricky did not apply the immediate state-$07 -$0300/" +
            "SPEED_140 motion after its delay.");
        for (int update = 0; update < 120 &&
            companion.Phase == RickyCompanionPhase.JumpingDownCliff; update++)
        {
            StepRickyApplicationInput(pressed: ["move_down"], justPressed: []);
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Landing ||
            companion.ZFixed == 0,
            "Ricky state $07 did not finish on the moving-away wall-mask " +
            "crossing while airborne.");
        _currentRoom.SetPositionTileAndCollision(
            downCliffLeft, downLeftTile, downLeftCollision,
            (long)_animationTicks);
        _currentRoom.SetPositionTileAndCollision(
            downCliffRight, downRightTile, downRightCollision,
            (long)_animationTicks);
        while (companion.Phase == RickyCompanionPhase.Landing)
            StepRickyApplicationInput(pressed: [], justPressed: []);
        FailIf(
            companion.Phase != RickyCompanionPhase.Riding,
            "Ricky did not finish landing after state $07.");

        companion.SetScreenTransitionBoundaryCoordinate(
            horizontal: true, 80, _player);
        companion.SetScreenTransitionBoundaryCoordinate(
            horizontal: false, 64, _player);
        Vector2 holeTile = new(88, 72);
        byte holeOriginalTile = _currentRoom.GetMetatile(holeTile);
        byte holeOriginalCollision =
            _currentRoom.GetTerrainInfo(holeTile).Collision;
        _currentRoom.SetPositionTileAndCollision(
            holeTile, 0xf3, 0xff, (long)_animationTicks);
        int holeJumpSounds = _sound.PlayRequestsFor(record.RickySound);
        StepRickyApplicationInput(pressed: ["move_right"], justPressed: []);
        FailIf(
            companion.Phase != RickyCompanionPhase.JumpingOverHole ||
            companion.AnimationIndex != behavior.LongJumpAnimation + 1 ||
            !companion.DisablesScreenTransitions,
            "Ricky did not recognize the direction-table one-tile $f3 probe " +
            "as state-$05 substate-$02 with transitions disabled.");
        Vector2 holeJumpStart = companion.PrecisePosition;
        for (int update = 0; update < behavior.LongJumpDelay - 1; update++)
            StepRickyApplicationInput(pressed: ["move_right"], justPressed: []);
        FailIf(
            companion.PrecisePosition != holeJumpStart ||
            _sound.PlayRequestsFor(record.RickySound) != holeJumpSounds,
            "Ricky's hole jump moved or sounded before its `$08 delay.");
        StepRickyApplicationInput(pressed: ["move_right"], justPressed: []);
        FailIf(
            companion.PrecisePosition.X <= holeJumpStart.X ||
            companion.ZFixed >= 0 ||
            _sound.PlayRequestsFor(record.RickySound) != holeJumpSounds + 1,
            "Ricky's hole jump did not start -$0300/SPEED_140 motion with " +
            "SND_RICKY on its exact zero boundary.");
        _currentRoom.SetPositionTileAndCollision(
            holeTile, holeOriginalTile, holeOriginalCollision,
            (long)_animationTicks);
        for (int update = 0; update < 120 &&
            companion.Phase == RickyCompanionPhase.JumpingOverHole; update++)
        {
            StepRickyApplicationInput(pressed: ["move_right"], justPressed: []);
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Landing ||
            companion.DisablesScreenTransitions,
            "Ricky's hole jump did not enter substate $03 and clear the " +
            "transition lock after landing or meeting a forward wall.");
        for (int update = 0; update < 120 &&
            companion.Phase == RickyCompanionPhase.Landing; update++)
        {
            StepRickyApplicationInput(pressed: [], justPressed: []);
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Riding,
            "Ricky did not finish the hole-jump landing probes.");

        Func<bool> originalTransitionLock =
            _transitions.ScreenTransitionsDisabledSource;
        _transitions.ScreenTransitionsDisabledSource = static () => true;
        try
        {
            companion.SetScreenTransitionBoundaryCoordinate(
                horizontal: true,
                _currentRoom.Width - 6,
                _player);
            while (companion.HopCounter > 0)
            {
                StepRickyApplicationInput(
                    pressed: ["move_right"],
                    justPressed: []);
            }
            StepRickyApplicationInput(
                pressed: ["move_right"],
                justPressed: []);
            for (int update = 0; update < 120 &&
                companion.Phase != RickyCompanionPhase.Landing; update++)
            {
                StepRickyApplicationInput(
                    pressed: ["move_right"],
                    justPressed: []);
            }
            while (companion.Phase == RickyCompanionPhase.Landing)
                StepRickyApplicationInput(pressed: [], justPressed: []);
            FailIf(
                companion.Phase != RickyCompanionPhase.Riding ||
                companion.HopCounter != behavior.HopDelay,
                "Ricky did not restore var39=$10 after landing at the " +
                "horizontal screen boundary.");
        }
        finally
        {
            _transitions.ScreenTransitionsDisabledSource =
                originalTransitionLock;
        }

        // Begin the periodic hop just inside the right edge, then release the
        // direction. rickySetJumpSpeed_andcc91 holds scrolling throughout the
        // airborne and eight-update grounded interval. Once
        // rickyStopUntilLandedOnGround clears the lock, screenTransitionState2
        // still requires wLinkAngle to point toward the boundary.
        // Reuse the transition handoff to construct an exact $.00 fixed-point
        // coordinate; the ordinary boundary setter intentionally retains the
        // existing fractional byte.
        companion.BeginScreenTransition(_currentRoom);
        companion.FinishScreenTransition(
            new Vector2(_currentRoom.Width - 20, 64), _player);
        StepRickyApplicationInput(pressed: [], justPressed: []);
        for (int update = 0; update < behavior.HopDelay; update++)
        {
            StepRickyApplicationInput(
                pressed: ["move_right"], justPressed: []);
        }
        StepRickyApplicationInput(
            pressed: ["move_right"],
            justPressed: []);
        FailIf(
            companion.Phase != RickyCompanionPhase.Hopping ||
            !companion.DisablesScreenTransitions ||
            _transitions.IsTransitioning,
            "Ricky did not enter the source-locked periodic hop before the " +
            "right screen boundary.");
        for (int update = 0; update < 120 &&
            companion.Phase == RickyCompanionPhase.Hopping; update++)
        {
            StepRickyApplicationInput(pressed: [], justPressed: []);
            FailIf(
                _transitions.IsTransitioning,
                "Ricky started a screen transition before the periodic " +
                "hop's eighth grounded update cleared the source lock.");
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Landing ||
            companion.DisablesScreenTransitions,
            "Ricky's periodic edge hop did not clear the transition lock on " +
            "the exact rickyStopUntilLandedOnGround update.");
        StepRickyApplicationInput(pressed: [], justPressed: []);
        FailIf(
            _transitions.IsTransitioning ||
            companion.Phase != RickyCompanionPhase.Riding,
            "Ricky bypassed screenTransitionState2's Link-angle gate after " +
            "landing at the boundary with input released.");
        StepRickyApplicationInput(
            pressed: ["move_right"], justPressed: []);
        FailIf(
            _transitions.IsTransitioning ||
            Mathf.FloorToInt(companion.PrecisePosition.X) !=
                _currentRoom.Width - 6,
            "Mounted Ricky treated the retained fractional X byte as a " +
            "screen-edge crossing on the first grounded movement update " +
            $"(transition={_transitions.IsTransitioning}, " +
            $"position={companion.PrecisePosition}, " +
            $"phase={companion.Phase}, hop={companion.HopCounter}).");
        StepRickyApplicationInput(
            pressed: ["move_right"], justPressed: []);
        bool hasRightDestination =
            _transitions.TryGetScreenTransitionDestinationForValidation(
                Vector2I.Right,
                out int rightDestination);
        FailIf(
            !_transitions.IsTransitioning ||
            !_entities.ScreenTransitionActive,
            "Mounted Ricky did not start the live horizontal $0:6a->$0:6b " +
            "screen transition from his owned boundary coordinate " +
            $"(room=${_rooms.CurrentRoom.Id:x2}, " +
            $"position={companion.PrecisePosition}, phase={companion.Phase}, " +
            $"ride={_player.CompanionRideActive}, " +
            $"cutscene={_player.CutsceneControlled}, " +
            $"lock={companion.DisablesScreenTransitions}, " +
            $"globalLock={originalTransitionLock()}, " +
            $"delay={_transitions.ScreenTransitionDelay}, " +
            $"destination={hasRightDestination}/${rightDestination:x2}).");
        while (_transitions.IsTransitioning)
            _transitions.UpdateScroll(1.0 / 60.0);
        Vector2 screenRespawn =
            OracleObjectMath.ToPixelPosition(companion.PrecisePosition);
        FailIf(
            _rooms.CurrentRoom.Id != 0x6b ||
            companion.PrecisePosition.X >= _currentRoom.Width / 2.0f ||
            _player.PrecisePosition != companion.PrecisePosition ||
            _player.LocalRespawnPosition != screenRespawn ||
            CompanionRuntimeState.ReadLastAnimalMountPosition(_runtimeState) !=
                screenRespawn,
            "Mounted Ricky did not finish the horizontal scroll in room " +
            "$0:6b with Link at Ricky's exact object position and both " +
            "finishScrollingTransition respawn coordinates updated.");

        // companionRespawn validates the local point once, but its
        // @invalidPosition branch copies wLastAnimalMountPointY/X without a
        // second collision or hazard test. Make the screen-entry point itself
        // a hole to cover the exact branch which used to throw in room $0:89.
        Vector2 hazardProbe = screenRespawn + new Vector2(0, 5);
        byte respawnOriginalTile = _currentRoom.GetMetatile(hazardProbe);
        byte respawnOriginalCollision =
            _currentRoom.GetTerrainInfo(hazardProbe).Collision;
        try
        {
            _currentRoom.SetPositionTileAndCollision(
                hazardProbe, 0xf3, 0xff, (long)_animationTicks);
            StepRickyApplicationInput(pressed: [], justPressed: []);
            FailIf(
                companion.Phase != RickyCompanionPhase.HazardFalling,
                "Ricky did not enter state $04 on the hazardous mounted " +
                "screen-entry point.");
            for (int update = 0; update < 240 &&
                companion.Phase == RickyCompanionPhase.HazardFalling; update++)
            {
                StepRickyApplicationInput(pressed: [], justPressed: []);
            }
            FailIf(
                companion.Phase != RickyCompanionPhase.Riding ||
                companion.PrecisePosition != screenRespawn ||
                _player.LocalRespawnPosition != screenRespawn,
                "companionRespawn did not copy the shared last mount point " +
                "without revalidating it after the local point failed.");
        }
        finally
        {
            _currentRoom.SetPositionTileAndCollision(
                hazardProbe,
                respawnOriginalTile,
                respawnOriginalCollision,
                (long)_animationTicks);
        }

        StepRickyApplicationInput(
            pressed: ["item"],
            justPressed: ["item"]);
        FailIf(
            companion.Phase != RickyCompanionPhase.Dismounting,
            "Ricky did not route mounted B to the source dismount state.");

        for (int update = 0; update < 120 &&
            companion.Phase != RickyCompanionPhase.AwaitingDistance; update++)
        {
            StepRickyApplicationInput(pressed: [], justPressed: []);
        }
        int frozenDismountFrame = companion.AnimationFrameIndex;
        StepRickyApplicationInput(pressed: [], justPressed: []);
        FailIf(
            companion.Phase != RickyCompanionPhase.AwaitingDistance ||
            companion.AnimationIndex != 0x17 ||
            companion.AnimationFrameIndex != frozenDismountFrame,
            "Ricky state-$06 substate-$02 did not freeze animation $17 " +
            "while waiting for Link to move away.");

        _player.WarpTo(companion.PrecisePosition + new Vector2(20, 0));
        for (int update = 0; update < 3 &&
            companion.Phase != RickyCompanionPhase.Waiting; update++)
        {
            StepRickyApplicationInput(pressed: [], justPressed: []);
        }
        FailIf(
            companion.Phase != RickyCompanionPhase.Waiting ||
            companion.AnimationFrameIndex != frozenDismountFrame,
            "Ricky did not resume state $01 from the frozen post-dismount " +
            "animation after Link crossed the source distance-$09 boundary.");

        for (int update = 0; update < 58; update++)
            StepRickyApplicationInput(pressed: [], justPressed: []);
        FailIf(
            companion.AnimationFrameIndex != 5 || companion.ZFixed != 0,
            "Ricky animation $17 did not reach its one-update parameter-$80 " +
            "idle-hop cue after the exact 20+6+6+6+20 updates.");
        StepRickyApplicationInput(pressed: [], justPressed: []);
        FailIf(
            companion.AnimationFrameIndex != 6 ||
            companion.ZFixed != record.JumpSpeedZ,
            "Ricky animation $17 parameter $40 did not apply the imported " +
            "-$0100 idle-hop speed on the update after parameter $80.");
        for (int update = 0; update < 8; update++)
            StepRickyApplicationInput(pressed: [], justPressed: []);
        FailIf(
            companion.AnimationFrameIndex != 6 || companion.ZFixed != 0,
            "Ricky's animation-$17 idle hop did not land after exactly nine " +
            "parameter-$40 gravity updates.");

        GD.Print(
            "Validated Ricky riding: `$10 SPEED_c0 ground cadence, shared-RNG " +
            "normal hop, -$0180/$40/SPEED_200 airborne motion, `$08 landing, " +
            "A-owned ITEM_28 punch, animation-parameter cues, `$1e tornado " +
            "charge and palette flash, ITEM_RICKY_TORNADO SPEED_300 release, " +
            "directional punch poses, normal grass debris, exact Ricky/Link " +
            "position synchronization, retained-$1060 right/left graphics " +
            "loads, state-$02/$07 cliff delays and wall crossings, explicit " +
            "hole-jump timing, transition-lock release, grounded edge delay, " +
            "held-input horizontal scroll ownership after a periodic edge " +
            "hop, mounted-scroll respawn anchors, unvalidated shared-mount " +
            "hazard fallback, B dismount routing, frozen state-$06 animation, and " +
            "parameter-driven $17 idle hopping.");
    }

    private void StepRickyApplicationInput(
        IReadOnlyList<string> pressed,
        IReadOnlyList<string> justPressed)
    {
        Vector2 movement = new(
            (pressed.Contains("move_right") ? 1.0f : 0.0f) -
                (pressed.Contains("move_left") ? 1.0f : 0.0f),
            (pressed.Contains("move_down") ? 1.0f : 0.0f) -
                (pressed.Contains("move_up") ? 1.0f : 0.0f));
        if (movement.LengthSquared() > 1.0f)
            movement = movement.Normalized();
        Input.BeginOriginalUpdate(new ApplicationInputSnapshot(
            pressed, justPressed, movement));
        try
        {
            _player.AdvanceApplicationUpdate();
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
            _interactions.Update(1.0 / 60.0, _player);
            UpdatePostObjectPlayerState();
            _dialogue.AdvanceApplicationUpdate();
            _sound.Tick();
        }
        finally
        {
            Input.EndOriginalUpdate();
        }
    }
}
