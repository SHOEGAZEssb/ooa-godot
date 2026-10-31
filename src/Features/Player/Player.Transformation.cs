using Godot;

namespace Oracles;

public partial class Player
{
    private int _ringTransformationRequested;
    internal void RequestNormalLinkOverride()
    {
        if (_activeTransformation == 0) return;
        ClearBabyTransformation();
        _ringTransformationRequested = -1;
    }
    private bool _ringUpdatedInPhysics;
    private byte RingTransformationSuppression
    {
        get => _inventory.RuntimeState.ReadField("wDisableRingTransformations");
        set => _inventory.RuntimeState.SetField("wDisableRingTransformations",value);
    }

    // getTransformedLinkID decrements only when its caller reaches selection.
    private int SelectRingTransformation()
    {
        if (RingTransformationSuppression != 0)
        {
            RingTransformationSuppression--;
            return 0;
        }
        return _world.RingTransformationsAllowed && !GrabStateActive
            ? _transformedLink.Transformation(_inventory.ActiveRing) : 0;
    }

    private bool TryRequestRingTransformation()
    {
        if (_world.SideScrolling || _minecartRideControlled || _raftRideControlled || _companionRideControlled) return false;
        int id = SelectRingTransformation();
        if (id == 0) return false;
        _ringTransformationRequested = id;
        _walking = false;
        QueueRedraw();
        return true;
    }

    private bool InitializeRingOverride()
    {
        if (_ringTransformationRequested == 0) return false;
        int id = _ringTransformationRequested;
        _ringTransformationRequested = 0;
        _ringUpdatedInPhysics = true;
        _activeTransformation = id < 0 ? 0 : id;
        CancelNativeItemUsage();
        EndCarriedObjectPose();
        _transformationFrame = 0;
        _transformationTicks = id < 0 ? 0 : _transformedLink.Record(id,(int)_facing,0).InitialDuration;
        _walking = false;

        if (id < 0) ResetLinkWalkAnimation();
        QueueRedraw();
        return true;
    }

    private void ResetRingToNormal()
    {
        // transformedLink's reset tail runs normal Link state00 immediately.
        _activeTransformation = _ringTransformationRequested = 0;
        RingTransformationSuppression = 1;
        CancelNativeItemUsage();
        _transformationFrame = _transformationTicks = 0;
        ResetLinkWalkAnimation();
        _walking = false;
        QueueRedraw();
    }

    private bool AdvanceRingLink()
    {
        if (_activeTransformation < 3) return false;
        _ringUpdatedInPhysics = true;


        if (_world.IsTransitioning || _world.RoomExitPending || _world.NativePaletteChanging) return true;
        if (IsDying)
        {
            _ringTransformationRequested = -1;
            RingTransformationSuppression = 1;
            return true;
        }
        if (_world.NativeTextActive || _world.PlayerUpdatesFrozen || _world.LinkDisabled) return true;
        _world.Pegasus?.AdvanceCounter();
        var terrain = ApplyTopDownTileTypes().Terrain;
        bool holeImmobilized = (_inventory.RuntimeState.ReadField("wLinkImmobilized") & 0x10) != 0;
        if (_fallingInHole)
        {
            // Nonzero Link ID publishes forceState02 for the next dispatch.
            if(_fallInHoleInitializePending)_forcedRespawnPhase=0;
            _fallingInHole=false;
            _inventory.RuntimeState.SetField("wLinkForceState",2);
            _inventory.RuntimeState.SetField("wLinkStateParameter",0);
        }
        if (terrain.Hazard is HazardType.Water or HazardType.Lava)
        {
            _topDownSwimmingState = terrain.Hazard == HazardType.Lava ? 0x41 : 1;
            ResetRingToNormal();
            return true;
        }
        if (SelectRingTransformation() != _activeTransformation)
        {
            ResetRingToNormal();
            return true;
        }
        Vector2 input = Input.GetVector("move_left","move_right","move_up","move_down");
        _lastMovementInput = input;
        NativeAdjacentWalls = _world.GetAdjacentWallsBitset(_precisePosition);
        if (_enemyKnockbackFrames > 0) AdvanceTopDownKnockback(ApplicationFixedUpdateScheduler.UpdateDelta);
        SetTopDownTerrainSpeed(0,GetTopDownMovementSpeed(),writeSpeedDirectly:true);
        if (_enemyKnockbackFrames > 0)
        {
            AdvanceTransformationAnimation(_world.Pegasus is { Active:true });
        }
        else
        {
            if (_topDownAirZFixed < 0 && OracleObjectMath.UpdateSpeedZ(ref _topDownAirZFixed,ref _topDownAirSpeedZ,0x20))
                _topDownAirborne = false;
            _topDownMovementAngle = AngleForVector(input);
            _walking = _topDownMovementAngle < 0x80 && !NativeMovementSuppressed;
            if (_walking)
            {
                if (CanTurnForTerrainJump) UpdateFacing(input);
                if (!_world.MovementDisabled && !holeImmobilized && terrain.Type != TerrainType.Stump)
                    ApplyTopDownObjectSpeed(_topDownMovementSpeedRaw,_topDownMovementAngle,allowWallSlide:true);
            }
            AdvanceTransformationAnimation(_walking || _world.Pegasus is { Active:true });
        }
        if (_world.Pegasus is { } pegasus && (pegasus.RawCounter & 0x8000) != 0)
            _world.CreateTransformationDust(new(Mathf.Floor(_precisePosition.X),
                unchecked((byte)(Mathf.FloorToInt(_precisePosition.Y) + 5))),unchecked((sbyte)ObjectZHigh));
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        _world.CheckRoomExit(this);
        QueueRedraw();
        return true;
    }

    private void AdvanceTransformationAnimation(bool walking)
    {
        if (_activeTransformation == 0) return;
        var record = _transformedLink.Record(_activeTransformation,(int)_facing,_transformationFrame);
        if (!walking)
        {
            _transformationFrame = 0;
            _transformationTicks = record.InitialDuration;
            return;
        }
        if (--_transformationTicks > 0) return;
        _transformationFrame ^= 1;
        _transformationTicks = record.LoopDuration;
    }
}
