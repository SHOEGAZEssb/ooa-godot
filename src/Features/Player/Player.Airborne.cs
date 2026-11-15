namespace Oracles;

public partial class Player
{
	internal void StartLedgeHop(LedgeJumpPlan plan)
	{
		if (plan.AnimationPhaseDurations.Length != 3 ||
			plan.Direction == Vector2I.Zero)
		{
			throw new ArgumentException(
				"Invalid imported ledge-jump plan.",
				nameof(plan));
		}

		_ledgeGroundYFixed = Mathf.FloorToInt(_precisePosition.Y * 256.0f);
		_ledgeGroundXFixed = Mathf.FloorToInt(_precisePosition.X * 256.0f);
		_ledgeZFixed = 0;
		_ledgeSpeedZ = plan.InitialSpeedZ;
		_ledgeSpeedRaw = plan.SpeedRaw;
		_ledgeGravity = plan.Gravity;
		_ledgeLandSound = plan.LandSound;
		_ledgeCliffLength = plan.CliffLength;
		_ledgeAnimationDurations = (int[])plan.AnimationPhaseDurations.Clone();
		_ledgeAnimationPhase = 0;
		_ledgeAnimationCounter = _ledgeAnimationDurations[0];
		_ledgeDirection = plan.Direction;
		_ledgeCrossedScreen = false;
		_ledgeUpdateAccumulator = 0.0;
		_ledgeJumpState = plan.CrossesScreen
			? LedgeJumpState.AirborneBeforeScroll
			: LedgeJumpState.Airborne;

		if (plan.CrossesScreen)
		{
			int sourceY = _ledgeGroundYFixed >> 8;
			_ledgeGroundYFixed =
				(plan.ScreenBoundaryY << 8) |
				(_ledgeGroundYFixed & 0xff);
			_ledgeZFixed = unchecked(
				(sbyte)(byte)(sourceY - plan.ScreenBoundaryY)) << 8;
			_ledgeSpeedZ = plan.TransitionSpeedZ;
			_ledgeSpeedRaw = 0;
			SetLedgeGroundPosition();
		}

		_walking = false;

		_world.PlaySound(plan.JumpSound);
		QueueRedraw();
	}

	internal void ResumeLedgeHopAfterScroll(
		Vector2 landingPosition,
		int cliffLength)
	{
		if (_ledgeJumpState != LedgeJumpState.WaitingForScroll)
		{
			throw new InvalidOperationException(
				"A ledge jump can resume only after its scrolling transition.");
		}

		int currentYFixed = Mathf.FloorToInt(_precisePosition.Y * 256.0f);
		_ledgeGroundYFixed =
			(Mathf.FloorToInt(landingPosition.Y) << 8) |
			(currentYFixed & 0xff);
		_ledgeGroundXFixed = Mathf.FloorToInt(_precisePosition.X * 256.0f);
		int currentY = currentYFixed >> 8;
		int landingY = _ledgeGroundYFixed >> 8;
		_ledgeZFixed = unchecked(
			(sbyte)(byte)(currentY - landingY)) << 8;
		_ledgeCliffLength = cliffLength;
		_ledgeCrossedScreen = true;
		_ledgeJumpState = LedgeJumpState.AirborneAfterScroll;
		SetLedgeGroundPosition();
		QueueRedraw();
	}

	private bool TryStartFeatherJump(string buttonAction)
	{
		Span<ParentItemSlotState> slots = stackalloc ParentItemSlotState[4];
		CollectFreeItemParents(slots);
		int slot = _parentItemUsage.ChooseSlot(TreasureId.Feather,slots);
		if (slot < 0) return false;
		// featherParent state0 rejects isLinkUnderwater and swimming before
		// writing speedZ/wLinkInAir. Underwater floors are otherwise walkable.
		if (_world.Underwater || TopDownSwimming || SideScrollSwimming || _world.FeatherBlockedByMagnet ||
			_companionRideControlled || _minecartRideControlled || _raftRideControlled ||
			_drowning || (TerrainType)_inventory.RuntimeState.ReadField("wActiveTileType") is TerrainType.Hole or TerrainType.WarpHole ||
			IsCarryingObject || IsHoldingItemOneHand || IsHoldingItemTwoHands)
			return false;
		if (_capeJumpEligible && (_topDownAirborne || _sideScrollAirborne))
		{
			_capeParents[slot - 3] = false;
			ParentItemButtons.SetAction(_inventory.RuntimeState, slot, buttonAction);
			return true;
		}
		bool started = _world.SideScrolling ? TryStartSideScrollJump() : TryStartTopDownJump();
		if (started)
		{
			int level = TopDownAirDatabase.For(AssetRoot).Parameters.CapeLevel;
			_capeJumpEligible = level != 0 && _inventory.FeatherLevel == level;
			if (_capeJumpEligible)
			{
				_capeParents[slot - 3] = true;
				ParentItemButtons.SetAction(_inventory.RuntimeState, slot, buttonAction);
			}
		}
		return started;
	}

	private bool TryStartTopDownJump()
	{
		// featherParent checks wLinkObjectIndex (mounted companion/cart),
		// not wLinkRidingObject. A moving platform must allow jumping off.
		if (_topDownAirborne || _companionRideControlled || _minecartRideControlled || _raftRideControlled ||
			_drowning || (TerrainType)_inventory.RuntimeState.ReadField("wActiveTileType") is TerrainType.Hole or TerrainType.WarpHole ||
			IsCarryingObject || IsHoldingItemOneHand ||
			IsHoldingItemTwoHands || _inventory.FeatherLevel <= 0)
		{
			return false;
		}

		TopDownAirParameters parameters =
			TopDownAirDatabase.For(AssetRoot).Parameters;
		_topDownAirUpdateAccumulator = 0.0;
		_topDownAirZFixed = 0;
		_topDownAirSpeedZ = parameters.JumpSpeedZ;
		_topDownReducedGravity = false;
		_capeJumpEligible = false;
		// linkUpdateInAir's @startedJump branch normalizes wActiveTileType,
		// snapshots wLinkAngle and the current normal/Pegasus speed. Rising movement retains it;
		// the descending branch calls linkUpdateVelocity before movement.
		if (!UsesTopDownIcePhysics)
		{
			_inventory.RuntimeState.SetField("wActiveTileType",0);
			_topDownMovementAngle = AngleForVector(_lastMovementInput);
			SetTopDownTerrainSpeed(0, NormalMovementSpeed, writeSpeedDirectly: true);
		}
		_airborneLinkAnimationMode = IsAttacking
			? AirborneLinkAnimationMode.Walk
			: AirborneLinkAnimationMode.Jump;
		_topDownAirborne = true;
		_topDownJumpSoundPending = true;
		_walking = false;

		return true;
	}

	// scriptHelp.tokayMakeLinkJump writes wLinkInAir=$81 and speedZ only.
	// Retain Z and let the existing Link air owner perform the next update,
	// including @startedJump's additional SND_JUMP and the landing sound.
	internal void BeginControlledTopDownJump(object owner,int speedZ)
	{
		if (!IsCutsceneControlOwner(owner))
			throw new InvalidOperationException("A scripted Link jump requires its existing control owner.");
		_topDownAirSpeedZ=speedZ;
		_topDownAirborne=true;
		_topDownJumpSoundPending=true;
	}

	// Native state01 terrain jumps retain bit $80 until landing and can carry
	// the var37/var38 target used by stump entry (ordinary Feather jumps do not).
	internal void BeginTerrainAirJump(int angle, int speedRaw, int? targetSpeedRaw, int speedZ, Vector2? target, bool cancelItems)
	{
		if (cancelItems) CancelNativeItemUsage();
		_topDownAirborne = true;
		_topDownAirIgnoresWalls = true;
		_topDownAirLandingTarget = target;
		_topDownAirSpeedZ = speedZ;
		_topDownMovementAngle = angle;
		_topDownMovementSpeedRaw = speedRaw;
		if (targetSpeedRaw is { } speed) _topDownMovementTargetSpeedRaw = speed;
		_topDownJumpSoundPending = true;
		if (!IsUsingItem) _airborneLinkAnimationMode = AirborneLinkAnimationMode.Jump;
		_walking = false;
	}

	internal void AdvanceControlledTopDownAir(object owner)
	{
		if (!IsCutsceneControlOwner(owner) || !_topDownAirborne ||
			_world.NativeTextActive || _world.NativePaletteChanging ||
			_world.RoomExitPending || _world.PlayerUpdatesFrozen || _world.LinkDisabled)
			return;
		AdvanceTopDownAirUpdate();
		QueueRedraw();
	}

	internal void CancelControlledTopDownAir(object owner)
	{
		if (IsCutsceneControlOwner(owner)) ClearTopDownAirState();
	}

	private void UpdateTopDownAirMovement(
		double delta,
		Vector2 input,
		bool movementAllowed,
		Vector2 movementStart)
	{
		int landingAngle = _topDownMovementAngle;
		ActiveTerrainInfo? landedTerrain = null;
		_topDownAirUpdateAccumulator += delta * 60.0;
		while (_topDownAirUpdateAccumulator + 0.000001 >= 1.0 &&
			_topDownAirborne)
		{
			_topDownAirUpdateAccumulator -= 1.0;
			landedTerrain = AdvanceTopDownAirUpdate();
			if (_topDownAirborne)
				AdvanceTopDownAirMomentum(input);
		}

		if (!_topDownAirborne && _enemyKnockbackFrames > 0)
		{
			// After @landed, @notInAir returns through func_5631 while recoil
			// remains. Ice retains the knockback angle, without input motion.
			if (UsesTopDownIcePhysics)
				_topDownMovementAngle = _enemyKnockbackAngle;
			_walking = false;
		}
		else if (!_topDownAirborne)
		{
			// linkUpdateInAir applies landing terrain before state01 resumes
			// ground movement. Water enters its state1 handler immediately;
			// its locked entry angle is the airborne object's last angle.
			if (TryAdvanceTopDownSwimming(input, landingAngle, movementStart,
					attackJustPressed: false, diveJustPressed: false,terrainBeforeMotion:landedTerrain))
				return;
			// A landing calls animateLinkStanding inside linkUpdateInAir, then
			// resumes the ordinary movement path in the same update. Held
			// movement therefore performs the first WALK decrement immediately.
			if (_raftRideControlled)
			{
				// @notInAir branches to direction when wLinkObjectIndex is
				// odd, before linkUpdateMovement/animateLinkStanding.
				_walking = false;
				if (input.LengthSquared() > 0.01f) UpdateFacing(input);
			}
			else
			{
				if (TryRequestRingTransformation()) return;
				_walking = AdvanceTopDownInputMovement(input, movementAllowed);
				if (_walking)
					AdvanceLinkWalkAnimation();
				else
					ResetLinkWalkAnimation();
			}
		}
		else
		{
			_walking = false;
		}

		Position = OracleObjectMath.ToPixelPosition(_precisePosition);

		if (_topDownAirborne || !_world.CheckTileWarp(this))
			_world.CheckRoomExit(this);
		if (!_world.IsTransitioning && !_topDownAirborne)
			ApplyTerrainAtFeet();
		AdvanceTransformationAnimation(_walking);
		QueueRedraw();
	}

	private void AdvanceSwitchHookAirState()
	{
		// linkUpdateInAir still runs after the hook parent. Observe Z
		// from the preceding item pass; this update's hook pass will
		// overwrite the intermediate gravity result later.
		if (_world.SideScrolling) return;
		if (TopDownAirZ < 0) _topDownAirborne = true;
		ActiveTerrainInfo? landedTerrain = _topDownAirborne ? AdvanceTopDownAirUpdate() : null;
		if (!_topDownAirborne)
		{
			// @landed reapplies tile types, then state01 dispatches swimming
			// even while the exchange parent immobilizes Link. Its parent
			// clear leaves the physical hook for the ordinary/post item passes.
			if (!TryAdvanceTopDownSwimming(Vector2.Zero, _topDownMovementAngle,
					_precisePosition, attackJustPressed: false, diveJustPressed: false,terrainBeforeMotion:landedTerrain))
				ApplyTerrainAtFeet();
		}
	}

	private ActiveTerrainInfo? AdvanceTopDownAirUpdate()
	{
		TopDownAirParameters parameters =
			TopDownAirDatabase.For(AssetRoot).Parameters;
		// @inAir clears both var12 and var13, including on an ice launch.
		_topDownMovementVelocityCounter = _topDownMovementVelocityInterval = 0;
		if (_topDownJumpSoundPending)
		{
			_topDownJumpSoundPending = false;
			_world.PlaySound(parameters.JumpSound);
			InitializeNativeJumpAnimation();
		}
		if (_topDownAirIgnoresWalls) NativeAdjacentWalls = 0;

		if (OracleObjectMath.UpdateSpeedZ(
			ref _topDownAirZFixed,
			ref _topDownAirSpeedZ,
			_topDownReducedGravity ? parameters.ReducedGravity : parameters.Gravity))
		{
			_topDownAirborne = false;
			_capeJumpEligible = false;
			_topDownReducedGravity = false;
			ClearCapeParents();
			_topDownAirSpeedZ = 0;
			// @landed clears var36, retaining the object's velocity until
			// the following ordinary terrain/movement handler updates it.
			_topDownMovementTerrainMode = 0;
			_airborneLinkAnimationMode = AirborneLinkAnimationMode.None;
			ResetLinkWalkAnimation();
			// @landed reapplies linkApplyTileTypes after clearing wLinkInAir,
			// before resuming ground motion. checkAndUpdateLinkOnChest already
			// ran earlier; the landing terrain handler must not run it again.
			if (_topDownAirLandingTarget is { } target)
			{
				SetCoordinateHigh(horizontal:false,Mathf.FloorToInt(target.Y));
				SetCoordinateHigh(horizontal:true,Mathf.FloorToInt(target.X));
			}
			_topDownAirLandingTarget = null;
			_topDownAirIgnoresWalls = false;
			var active = ApplyTopDownTileTypes();
			if ((TerrainType)_inventory.RuntimeState.ReadField("wActiveTileType") is TerrainType.Hole or TerrainType.WarpHole)
			{
				_holePullCounter = parameters.HoleStandingCounter;
				// initLinkState supersedes a state02 selected by landing.
				if(_fallInHoleInitializePending)_forcedRespawnPhase=0;
				_fallingInHole=false;
				_pullingIntoHole = true;
				_holePullPackedPosition = active.PackedPosition;
			}
			_world.PlaySound(parameters.LandSound);
			return active;
		}

		if (_topDownAirSpeedZ > parameters.MaximumFallSpeed)
			_topDownAirSpeedZ = parameters.MaximumFallSpeed;
		AdvanceNativeLinkAnimation();
		return null;
	}

	internal void AdvanceTopDownAirUpdateForValidation(
		bool startJump = false,
		Vector2 movementInput = default)
	{
		if (_world.SideScrolling)
		{
			throw new InvalidOperationException(
				"A top-down Link air update was requested in a side-scrolling room.");
		}
		_lastMovementInput = movementInput;
		if (startJump && !TryStartTopDownJump())
		{
			throw new InvalidOperationException(
				"The validation top-down jump could not start.");
		}
		if (_topDownAirborne)
		{
			AdvanceTopDownAirUpdate();
			if (_topDownAirborne)
				AdvanceTopDownAirMomentum(movementInput);
		}
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	private void AdvanceTopDownAirMomentum(Vector2 input)
	{
		if (_topDownAirIgnoresWalls)
		{
			if (_topDownAirLandingTarget is { } target)
				_topDownMovementAngle = OracleObjectMovement.Shared.RelativeAngle(_precisePosition.Floor(),target);
			SetScriptedPosition(_world.ApplyNativeUnblockedMovement(_precisePosition,_topDownMovementSpeedRaw,_topDownMovementAngle));
			return;
		}
		// linkState01 calls linkUpdateVelocity after speedZ becomes nonnegative.
		// linkUpdateInAir clears var12/var13 immediately before this call, so
		// func_5933 performs one convergence step on each descending update.
		if (_topDownAirSpeedZ >= 0)
		{
			int velocityCounter = 0;
			UpdateConvergingVelocity(ref _topDownMovementAngle, ref _topDownMovementSpeedRaw,
				_topDownMovementTargetSpeedRaw, ref velocityCounter, _topDownMovementVelocityInterval,
				AngleForVector(input), inAir: true);
		}
		if (_topDownMovementAngle >= 0x80 || _topDownMovementSpeedRaw == 0)
			return;
		ApplyTopDownObjectSpeed(
			_topDownMovementSpeedRaw,
			_topDownMovementAngle,
			allowWallSlide: true);
	}

	private void InitializeNativeJumpAnimation()
	{
		if ((_inventory.RuntimeState.ReadField("wLinkGrabState") | _inventory.RuntimeState.ReadField("wLinkTurningDisabled")) == 0)
		{
			_scriptedLinkAnimationMode = null;
			LinkNativeAnimationDatabase.For(AssetRoot).Set(_inventory.RuntimeState, 0x18);
			_airborneLinkAnimationMode = AirborneLinkAnimationMode.Jump;
		}
	}

	private void AdvanceNativeLinkAnimation() =>
		LinkNativeAnimationDatabase.For(AssetRoot).Animate(_inventory.RuntimeState);

	private void ClearTopDownAirState()
	{
		_topDownAirIgnoresWalls = false;
		_topDownAirLandingTarget = null;
		_capeJumpEligible = false;
		_topDownReducedGravity = false;
		ClearCapeParents();
		_topDownAirUpdateAccumulator = 0.0;
		_topDownAirZFixed = 0;
		_topDownAirSpeedZ = 0;
		_topDownMovementAngle = 0xff;
		_topDownMovementSpeedRaw = _topDownMovementTargetSpeedRaw = 0;
		_topDownMovementTerrainMode = 0;
		_topDownMovementVelocityCounter = _topDownMovementVelocityInterval = 0;
		_topDownAirborne = false;
		_airborneLinkAnimationMode = AirborneLinkAnimationMode.None;
		ResetLinkWalkAnimation();
		_topDownJumpSoundPending = false;
		_minecartJumpControlled = false;
		_minecartRideControlled = false;
		_minecartJumpAngle = 0xff;
		_companionJumpControlled = false;
		_companionJumpAngle = 0xff;
		_companionDismountJump = false;
		_companionJumpAnimationDeferred = false;
		_companionRideControlled = false;
		_raftRideControlled = false;
		_companionRideTexture = null;
		_damageCompanionRideTexture = null;
		_companionRideTextureOffset = Vector2.Zero;
		_companionRideZFixed = 0;
	}

	private void UpdateLedgeHop(double delta)
	{
		if (delta < 0.0)
			throw new ArgumentOutOfRangeException(nameof(delta));
		if (_ledgeJumpState == LedgeJumpState.WaitingForScroll)
			return;

		_ledgeUpdateAccumulator += delta * 60.0;
		while (_ledgeUpdateAccumulator + 0.000001 >= 1.0 &&
			_ledgeJumpState is not (
				LedgeJumpState.None or LedgeJumpState.WaitingForScroll))
		{
			_ledgeUpdateAccumulator -= 1.0;
			AdvanceLedgeHopUpdate();
		}
		QueueRedraw();
	}

	private void AdvanceLedgeHopUpdate()
	{
		int movementFixed = _ledgeSpeedRaw * 256 / 40;
		_ledgeGroundYFixed += _ledgeDirection.Y * movementFixed;
		_ledgeGroundXFixed += _ledgeDirection.X * movementFixed;
		bool landed = OracleObjectMath.UpdateSpeedZ(
			ref _ledgeZFixed,
			ref _ledgeSpeedZ,
			_ledgeGravity);
		SetLedgeGroundPosition();

		if (!landed)
		{
			AdvanceLedgeAnimation();
			return;
		}

		if (_ledgeJumpState == LedgeJumpState.AirborneBeforeScroll)
		{
			_ledgeJumpState = LedgeJumpState.WaitingForScroll;
			_ledgeUpdateAccumulator = 0.0;
			_world.BeginLedgeScreenTransition(this);
			return;
		}

		if (_ledgeCrossedScreen)
		{
			_lastSafePosition = _precisePosition;
			_localRespawnFacing = _facing;
		}
		_world.CompleteLedgeLanding(this);
		int landSound = _ledgeLandSound;
		// State $12 uses the same object angle/speed as ordinary motion.
		// Its landing epilogue retains them; underwater/ice convergence resumes
		// from the cliff speed rather than the blocked approach's speed.
		_topDownMovementAngle = AngleForVector(_ledgeDirection);
		_topDownMovementSpeedRaw = _ledgeSpeedRaw;
		ClearLedgeHop();
		ResetLinkWalkAnimation();
		_walking = false;

		_world.PlaySound(landSound);
	}

	private void AdvanceLedgeAnimation()
	{
		if (IsAttacking)
		{
			AdvanceLinkWalkAnimation();
			return;
		}
		if (_ledgeAnimationPhase >= _ledgeAnimationDurations.Length)
			return;
		_ledgeAnimationCounter--;
		if (_ledgeAnimationCounter > 0)
			return;

		_ledgeAnimationPhase++;
		_ledgeAnimationCounter =
			_ledgeAnimationPhase < _ledgeAnimationDurations.Length
				? _ledgeAnimationDurations[_ledgeAnimationPhase]
				: int.MaxValue;
	}

	private void SetLedgeGroundPosition()
	{
		_precisePosition = new Vector2(
			_ledgeGroundXFixed / 256.0f,
			_ledgeGroundYFixed / 256.0f);
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
	}

	private void ClearLedgeHop()
	{
		_ledgeUpdateAccumulator = 0.0;
		_ledgeGroundYFixed = 0;
		_ledgeGroundXFixed = 0;
		_ledgeZFixed = 0;
		_ledgeSpeedZ = 0;
		_ledgeSpeedRaw = 0;
		_ledgeGravity = 0;
		_ledgeLandSound = 0;
		_ledgeCliffLength = 0;
		_ledgeAnimationPhase = 0;
		_ledgeAnimationCounter = 0;
		_ledgeAnimationDurations = [];
		_ledgeDirection = Vector2I.Zero;
		_ledgeJumpState = LedgeJumpState.None;
		_ledgeCrossedScreen = false;
	}
}
