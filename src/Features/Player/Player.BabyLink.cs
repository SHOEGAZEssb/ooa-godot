using Godot;

namespace Oracles;

public partial class Player
{
    private bool _babyRequested, _babyRestorationPending, _babyFootstepPending;
    private int _babyRemaining;
    internal int BabyRemaining => _babyRemaining;
    internal bool BabyRequested => _babyRequested;

    // collisionEffect3b queues the new special-object ID. Initialization
    // belongs to the following Link pass, before text and disabled-$81 gates.
    internal void QueueBabyTransformation() { _ringTransformationRequested = 0; _babyRequested = true; }
    private void ResetBabyForForcedState()
    {
        if (_activeTransformation == 0 && !_babyRequested && _ringTransformationRequested == 0) return;
        // transformedLink.s tests wLinkForceState before palette, scroll,
        // text and object masks. These are the normal Link owner's requests.
        bool forced = _inventory.RuntimeState.ReadField("wLinkForceState")!=0 || _nativeForcedMovementSelected ||
            _palaceTossRequested || _palaceTossSubstate >= 0 || CollapsePending || CollapsedActive ||
            _wallmasterGrabRequested || WallmasterGrabActive || _enemyGrabRequested || EnemyGrabActive ||
            _sideScrollSquishPending || _squishAnimation is not null || _getItemStatePhase != 0 ||
            _forcedRespawnPhase != 0 || ForcedRespawnRequested || _forcedState08Phase != 0 || _fallingInHole;
        if (!forced) return;
        if (_babyRequested || _ringTransformationRequested != 0) { CancelNativeItemUsage(); EndCarriedObjectPose(); }
        ClearBabyTransformation(); _activeTransformation = 0;
        _ringTransformationRequested = 0; RingTransformationSuppression = 1;
        _transformationFrame = _transformationTicks = 0; ResetLinkWalkAnimation();
    }
    private bool InitializeBabyOverride()
    {
        if (_babyRestorationPending)
        {
            ClearBabyTransformation(); _activeTransformation = 0;
            _transformationFrame = _transformationTicks = 0; QueueRedraw(); return true;
        }
        if (!_babyRequested) return false;
        _babyRequested = false; _babyRemaining = _transformedLink.BabyDuration;
        _babyFootstepPending = false; _activeTransformation = 2;
        _transformationFrame = 0; _transformationTicks = _transformedLink.Record(2, (int)_facing, 0).InitialDuration;
        ClearNativeItemParents(); EndCarriedObjectPose(); CancelSwordAttack(); CancelShovelAction();
        _walking = false;
        _world.PlaySound(SoundId.SndBecomeBaby);
        _world.CreateBabyTransformationPoof(_precisePosition, EnemyContactZ);
        QueueRedraw(); return true;
    }
    private void ClearBabyTransformation()
    {
        _babyRequested = _babyRestorationPending = _babyFootstepPending = false; _babyRemaining = 0;
    }
    private bool AdvanceBabyLink()
    {
        if (_activeTransformation != 2) return false;

        if (_world.IsTransitioning || _world.RoomExitPending || _world.NativePaletteChanging) return true;
        if (IsDying)
        {
            // @disableTransformation uses setLinkIDOverride; the current
            // special-object pass retains ID$02 until the following update.
            _babyRestorationPending = true;
            RingTransformationSuppression = 1;
            _world.CreateBabyTransformationPoof(_precisePosition, EnemyContactZ);
            return true;
        }
        if (_world.NativeTextActive || _world.PlayerUpdatesFrozen || _world.LinkDisabled) return true;
        _world.Pegasus?.AdvanceCounter();
        if (--_babyRemaining == 0)
        {
            _babyRestorationPending = true; _world.PlaySound(SoundId.SndMagicPowder);
            RingTransformationSuppression = 1;
            _world.CreateBabyTransformationPoof(_precisePosition, EnemyContactZ); return true;
        }
        Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        _lastMovementInput = input;
        NativeAdjacentWalls = _world.GetAdjacentWallsBitset(_precisePosition);
        SetTopDownTerrainSpeed(0,GetTopDownMovementSpeed(),writeSpeedDirectly:true);
        _topDownMovementSpeedRaw >>= _transformedLink.BabySpeedShift;
        if (_enemyKnockbackFrames > 0)
        {
            AdvanceKnockbackAfterItemParents(ApplicationFixedUpdateScheduler.UpdateDelta, false, _precisePosition);
            ResetBabyAnimation(); return true;
        }
        if (_topDownAirZFixed < 0 && OracleObjectMath.UpdateSpeedZ(ref _topDownAirZFixed, ref _topDownAirSpeedZ, 0x20))
            _topDownAirborne = false;
        _topDownMovementAngle = AngleForVector(input);
        _walking = _topDownMovementAngle < 0x80 && !NativeMovementSuppressed;
        if (_walking)
        {
            if (_babyFootstepPending) { _world.PlaySound(SoundId.SndSplash); _babyFootstepPending = false; }
            UpdateFacing(input);
            if (!_world.MovementDisabled)
                ApplyTopDownObjectSpeed(_topDownMovementSpeedRaw, _topDownMovementAngle, allowWallSlide: true);
            int previous = _transformationFrame; AdvanceTransformationAnimation(true);
            if (previous == 0 && _transformationFrame == 1) _babyFootstepPending = true;
        }
        else ResetBabyAnimation();
        Position = OracleObjectMath.ToPixelPosition(_precisePosition); _world.CheckRoomExit(this); QueueRedraw(); return true;
    }
    private void ResetBabyAnimation()
    {
        _babyFootstepPending = false; AdvanceTransformationAnimation(false); QueueRedraw();
    }
}
