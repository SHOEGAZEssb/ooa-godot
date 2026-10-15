using Godot;

namespace oracleofages;

public partial class Player
{
    private bool _babyRequested, _babyRestorationPending, _babyFootstepPending;
    private int _babyRemaining;
    internal int BabyRemaining => _babyRemaining;
    internal bool BabyRequested => _babyRequested;

    // collisionEffect3b queues the new special-object ID. Initialization
    // belongs to the following Link pass, before text and disabled-$81 gates.
    internal void QueueBabyTransformation() => _babyRequested = true;
    private void ResetBabyForForcedState()
    {
        if (_activeTransformation != 2 && !_babyRequested) return;
        // transformedLink.s tests wLinkForceState before palette, scroll,
        // text and object masks. These are the normal Link owner's requests.
        bool forced = _nativeForcedMovementRequest >= 0 || _nativeForcedMovementCounter != 0 || _forcedRoomEntryMovement ||
            _palaceTossRequested || _palaceTossSubstate >= 0 || CollapsePending || CollapsedActive ||
            _wallmasterGrabRequested || WallmasterGrabActive || _enemyGrabRequested || EnemyGrabActive ||
            _sideScrollSquishPending || _squishAnimation is not null || _getItemStatePhase != 0 ||
            _forcedRespawnPhase != 0 || _forcedState08Phase != 0 || _spinnerExitRequested;
        if (!forced) return;
        if (_babyRequested) { ClearNativeItemParents(); EndCarriedObjectPose(); }
        ClearBabyTransformation(); _activeTransformation = 0;
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
        _walking = _pushing = false; _frontTileInteractionReturned = true;
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
        _frontTileInteractionReturned = true; _pushing = false;
        if (IsDying)
        {
            // @disableTransformation uses setLinkIDOverride; the current
            // special-object pass retains ID$02 until the following update.
            _babyRestorationPending = true;
            _world.CreateBabyTransformationPoof(_precisePosition, EnemyContactZ);
            return true;
        }
        if (_world.IsTransitioning || _world.RoomExitPending || _world.NativePaletteChanging ||
            _world.NativeTextActive || _world.PlayerUpdatesFrozen || _world.LinkDisabled) return true;
        _world.Pegasus?.AdvanceCounter();
        if (--_babyRemaining == 0)
        {
            _babyRestorationPending = true; _world.PlaySound(SoundId.SndMagicPowder);
            _world.CreateBabyTransformationPoof(_precisePosition, EnemyContactZ); return true;
        }
        Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        _lastMovementInput = input;
        _tilePushWalls = _world.GetAdjacentWallsBitset(_precisePosition);
        _topDownMovementSpeedRaw = NormalMovementSpeed >> _transformedLink.BabySpeedShift;
        if (_enemyKnockbackFrames > 0)
        {
            AdvanceKnockbackAfterItemParents(ApplicationFixedUpdateScheduler.UpdateDelta, false, _precisePosition);
            ResetBabyAnimation(); return true;
        }
        if (_topDownAirZFixed < 0 && OracleObjectMath.UpdateSpeedZ(ref _topDownAirZFixed, ref _topDownAirSpeedZ, 0x20))
            _topDownAirborne = false;
        _topDownMovementAngle = AngleForVector(input);
        _walking = _topDownMovementAngle < 0x80 && !_spinnerControlled;
        if (_walking)
        {
            if (_babyFootstepPending) { _world.PlaySound(SoundId.SndSplash); _babyFootstepPending = false; }
            UpdateFacing(input);
            if (!_world.MovementDisabled)
                ApplyTopDownObjectSpeed(_topDownMovementSpeedRaw, _topDownMovementAngle, allowWallSlide: true, allowLedgeHop: false);
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
