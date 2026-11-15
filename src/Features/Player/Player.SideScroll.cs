namespace Oracles;

public partial class Player
{
	private void UpdateSideScrollMovement(
		double delta,
		Vector2 input,
		bool movementAllowed)
	{
		_sideScrollUpdateAccumulator += delta * 60.0;
		while (_sideScrollUpdateAccumulator + 0.000001 >= 1.0)
		{
			_sideScrollUpdateAccumulator -= 1.0;
			AdvanceSideScrollUpdate(
				input,
				movementAllowed,
				Input.IsActionJustPressed("attack"),
				Input.IsActionJustPressed("move_up") ||
				Input.IsActionJustPressed("move_right") ||
				Input.IsActionJustPressed("move_down") ||
				Input.IsActionJustPressed("move_left"));
		}
	}

	private void AdvanceSideScrollUpdate(
		Vector2 input,
		bool movementAllowed,
		bool attackJustPressed,
		bool directionJustPressed)
	{
		SideScrollPlayerParameters parameters = _world.SideScrollParameters;
		SideScrollActiveTiles terrain = _world.SideScrollActiveTiles;

		if ((terrain.ActiveType & SideScrollTileType.Water) != 0)
		{
			if (_sideScrollSwimmingState == 0)
			{
				_sideScrollSwimmingState = 1;
				_world.SpawnDrowningSplash(OracleObjectMath.ToPixelPosition(_precisePosition) + new Vector2(0, -3), HazardType.Water);
			}
			AdvanceSideScrollSwimming(
				input,
				movementAllowed,
				attackJustPressed,
				directionJustPressed,
				parameters);
			return;
		}

		if (_sideScrollSwimmingState != 0)
		{
			bool surfacedFromWaterLadder =
				// Despite its name, sidescrollUpdateActiveTile writes the
				// CURRENT y+$08 probe to wLastActiveTileType.
				terrain.BelowType ==
				(SideScrollTileType.Ladder | SideScrollTileType.Water);
			_sideScrollSwimmingState = 0;
			if (!surfacedFromWaterLadder)
			{
				// The hop writes wLinkInAir=$02 directly, bypassing the
				// jump initializer: retain swimming speed and animation.
				_sideScrollAirborne = true;
				_airborneLinkAnimationMode = AirborneLinkAnimationMode.SideScrollSwim;
				_sideScrollSpeedZ = parameters.WaterExitSpeedZ;
				_sideScrollReducedGravity = false;
				_sideScrollAngle = AngleForVector(input);
				_world.SpawnDrowningSplash(OracleObjectMath.ToPixelPosition(_precisePosition) + new Vector2(0, -3), HazardType.Water);
			}
		}

		int walls = _world.GetAdjacentWallsBitset(_precisePosition);
		bool grounded = (walls & parameters.GroundWallMask) != 0;
		bool onLadder =
			(terrain.CombinedType & SideScrollTileType.Ladder) != 0;
		if (_sideScrollAirborne || !_world.RidingObject && !grounded && !onLadder)
		{
			if (!_sideScrollAirborne) BeginSideScrollAirborne(jumped: false, parameters);
			if (!AdvanceSideScrollAirborne(input, terrain, walls, parameters)) return;
		}

		if (_enemyKnockbackFrames > 0.0f)
		{
			return;
		}
		if (terrain.ActiveTile == parameters.SpikeTile)
			ApplySpikeDamage(_sideScrollAngle);
		AdvanceGroundedSideScroll(
			input, movementAllowed, terrain, parameters);
	}

	private void AdvanceGroundedSideScroll(
		Vector2 input,
		bool movementAllowed,
		SideScrollActiveTiles terrain,
		SideScrollPlayerParameters parameters)
	{
		Vector2 beforeMovement = _precisePosition;
		bool onLadder =
			(terrain.CombinedType & SideScrollTileType.Ladder) != 0;
		int inputAngle = movementAllowed
			? AngleForVector(input)
			: 0xff;
		int walls = _world.GetAdjacentWallsBitset(_precisePosition);
		bool appliesIce =
			!RingEffects.IgnoresIce(_inventory) &&
			((terrain.BelowType == SideScrollTileType.Ice) ||
			 (_sideScrollForceIcePhysics != 0 &&
			  (walls & parameters.GroundWallMask) != 0));

		if (appliesIce)
		{
			SetSideScrollTerrainSpeed(
				terrainMode: 0x08,
				initialSpeed: 0,
				velocityInterval: parameters.IceVelocityInterval,
				targetSpeed: NormalMovementSpeed,
				writeSpeedDirectly: false);
			UpdateSideScrollVelocity(inputAngle, inAir: false);
			// wForceIcePhysics stores $06 as a nonzero latch. It is not
			// decremented; the grounded branch retains it until the source
			// reaches @notOnIce.
			_sideScrollForceIcePhysics = 0x06;
			_walking = inputAngle < 0x80;
			if (_sideScrollAngle < 0x80 && _sideScrollSpeedRaw != 0)
			{
				ApplySideScrollVelocity(
					_sideScrollSpeedRaw,
					_sideScrollAngle,
					allowWallSlide: true);
			}
		}
		else
		{
			_sideScrollForceIcePhysics = 0;
			// updateLinkSpeed_standard reads the raw tile-type byte in both
			// views: side-view LAVA$04 aliases top-down VINES$04.
			int speed = GetTopDownMovementSpeed();
			SetSideScrollTerrainSpeed(
				terrainMode: 0,
				initialSpeed: speed,
				velocityInterval: 0,
				targetSpeed: speed,
				writeSpeedDirectly: true);
			_sideScrollAngle = onLadder
				? inputAngle
				: SideScrollHorizontalAngle(inputAngle);
			_walking = inputAngle < 0x80;
			if (_walking && _sideScrollAngle < 0x80)
			{
				ApplySideScrollVelocity(
					_sideScrollSpeedRaw,
					_sideScrollAngle,
					allowWallSlide: true);
			}
			else
			{
				_sideScrollSpeedRaw = 0;
			}
		}

		// linkState01_sidescroll uses movement flags $07 only with input;
		// released ice coasting uses $02. linkUpdateMovement counts accepted
		// planar motion before the ladder-top clamp, never air or swim motion.
		if (inputAngle < 0x80)
			UpdateHeartRingCounter(_precisePosition - beforeMovement);

		if (_walking)
		{
			if (!IsUsingItem)
				// linkAdjustAngleInSidescrollingArea changes
				// SpecialObject.angle for movement only. The final
				// updateLinkDirectionFromAngle still reads the unmodified
				// wLinkAngle, so Up/Down input changes Link's facing while
				// ordinary dry movement remains horizontal.
				UpdateFacing(input);
			AdvanceLinkWalkAnimation();
		}
		else
		{
			ResetLinkWalkAnimation();
		}

		// The ladder-top clamp writes only Link's high Y byte. It prevents an
		// upward input from crossing the top half of a ladder-top metatile
		// until Link has reached its source-defined ninth pixel.
		if (IsUpwardAngle(_sideScrollAngle) &&
			terrain.ActiveType == SideScrollTileType.None &&
			terrain.BelowType ==
				(SideScrollTileType.Ladder | SideScrollTileType.LadderTop))
		{
			int high = Mathf.FloorToInt(_precisePosition.Y);
			if ((high & 0x0f) < 9)
			{
				high = (high & 0xf0) + 9;
				_precisePosition.Y = high +
					FractionalByte(_precisePosition.Y) / 256.0f;
			}
		}

		_sideScrollYFixed =
			Mathf.FloorToInt(_precisePosition.Y * 256.0f);
		// linkState01_sidescroll uses the pre-movement wall/tile probes
		// for wLinkClimbingVine, including the update crossing a tile edge.
		SideScrollClimbing =
			(walls & parameters.GroundWallMask) == 0 &&
			(terrain.ActiveType & SideScrollTileType.Ladder) != 0;
	}

	// True retains linkUpdateInAir_sidescroll's NZ return into ground movement.
	private bool AdvanceSideScrollAirborne(
		Vector2 input,
		SideScrollActiveTiles terrain,
		int walls,
		SideScrollPlayerParameters parameters)
	{
		if (_sideScrollJumpSoundPending)
		{
			_sideScrollJumpSoundPending = false;
			_world.PlaySound(parameters.JumpSound);
			InitializeNativeJumpAnimation();
		}

		if (_sideScrollSpeedZ >= 0)
		{
			if (_world.RidingObject)
			{
				_sideScrollAngle = 0xff;
				LandFromSideScrollAir(parameters, snapToGround: false);
				return false;
			}
			if ((walls & parameters.GroundWallMask) != 0)
			{
				LandFromSideScrollAir(parameters, snapToGround: true);
				return false;
			}
			if ((terrain.ActiveType & SideScrollTileType.Ladder) != 0 &&
				IsUpwardAngle(AngleForVector(input)))
			{
				LandFromSideScrollAir(parameters, snapToGround: false);
				return false;
			}

			int highY = _sideScrollYFixed >> 8;
			if ((highY & 0x08) != 0 &&
				terrain.BelowType ==
					(SideScrollTileType.Ladder |
					 SideScrollTileType.LadderTop))
			{
				LandFromSideScrollAir(parameters, snapToGround: true);
				return false;
			}
			if (terrain.ActiveType == SideScrollTileType.Lava)
			{
				StartDrowning(HazardType.Lava);
				// linkCreateSplash returns NZ: the caller still runs its
				// ordinary movement/animation tail on the request update.
				return true;
			}
			if ((terrain.ActiveType & SideScrollTileType.Hole) != 0 &&
				terrain.BelowType == SideScrollTileType.None)
			{
				_world.PlaySound(SoundId.SndDamageLink);
				// bank0.respawnLink writes forceState=$02/parameter=$02.
				// The next state01 consumes it; state02 initializes on the
				// following update, before the invisible counter starts.
				RequestForcedRespawn();
				return false;
			}

			// linkUpdateInAir_sidescroll calls linkUpdateVelocity on descent.
			// Its ordinary-air branch reads wLinkAngle regardless of the
			// item's wLinkImmobilized byte; turning remains a separate gate.
			UpdateSideScrollVelocity(AngleForVector(input), inAir: true);
		}

		bool ceilingBlocked =
			_sideScrollSpeedZ < 0 &&
			(walls & parameters.CeilingWallMask) != 0;
		if (!ceilingBlocked)
		{
			_sideScrollYFixed =
				unchecked((ushort)(_sideScrollYFixed + _sideScrollSpeedZ));
			_precisePosition.Y = _sideScrollYFixed / 256.0f;
		}

		_sideScrollSpeedZ = Math.Min(
			parameters.MaximumFallSpeed,
			_sideScrollSpeedZ +
				(_sideScrollReducedGravity
					? parameters.ReducedGravity
					: parameters.Gravity));

		walls = _world.GetAdjacentWallsBitset(_precisePosition);
		if ((walls & parameters.GroundWallMask) != 0)
		{
			LandFromSideScrollAir(parameters, snapToGround: true);
			return false;
		}

		int horizontalAngle =
			SideScrollHorizontalAngle(_sideScrollAngle);
		_sideScrollAngle = horizontalAngle;
		if (horizontalAngle < 0x80 && _sideScrollSpeedRaw != 0)
		{
			// linkUpdateInAir_sidescroll calls specialObjectUpdatePosition,
			// retaining @tileEdgeAdjust even after its horizontal-angle clamp.
			// @updateDirection is reached only after landing.
			ApplySideScrollVelocity(
				_sideScrollSpeedRaw,
				horizontalAngle,
				allowWallSlide: true);
		}

		_walking = false;
		AdvanceSideScrollAirAnimation();
		if ((_sideScrollYFixed >> 8) >= parameters.BottomBoundary)
		{
			if (parameters.BottomLadderTransition == 0)
				LandFromSideScrollAir(parameters, snapToGround: true);
			else if (terrain.ActiveType == SideScrollTileType.Ladder)
				_inventory.RuntimeState.SetField("wScreenTransitionDirection",(byte)parameters.BottomLadderTransition);
		}
		return false;
	}

	private bool TryStartSideScrollJump()
	{
		if (!_world.SideScrolling || _sideScrollAirborne ||
			_drowning || _fallingInHole || _pullingIntoHole ||
			IsCarryingObject || IsHoldingItemOneHand ||
			IsHoldingItemTwoHands ||
			_inventory.FeatherLevel <= 0)
		{
			return false;
		}

		SideScrollActiveTiles terrain =
			_world.SideScrollActiveTiles;
		if ((terrain.ActiveType &
				(SideScrollTileType.Water | SideScrollTileType.Hole)) != 0)
		{
			return false;
		}

		BeginSideScrollAirborne(
			jumped: true,
			_world.SideScrollParameters);
		return true;
	}

	private void BeginSideScrollAirborne(
		bool jumped,
		SideScrollPlayerParameters parameters,
		int? speedOverride = null)
	{
		_sideScrollYFixed =
			Mathf.FloorToInt(_precisePosition.Y * 256.0f);
		_sideScrollSpeedZ =
			speedOverride ?? (jumped ? parameters.JumpSpeedZ : 0);
		int speed = GetTopDownMovementSpeed();
		SetSideScrollTerrainSpeed(
			terrainMode: 0,
			initialSpeed: speed,
			velocityInterval: 0,
			targetSpeed: speed,
			writeSpeedDirectly: true);
		_sideScrollAirborne = true;
		_sideScrollJumpSoundPending = jumped;
		_capeJumpEligible = false;
		ClearCapeParents();
		_sideScrollReducedGravity = false;
		SideScrollClimbing = false;
		_airborneLinkAnimationMode = IsAttacking
			? AirborneLinkAnimationMode.Walk
			: AirborneLinkAnimationMode.Jump;
		_walking = false;
		if (!jumped) InitializeNativeJumpAnimation();
	}

	private void LandFromSideScrollAir(
		SideScrollPlayerParameters parameters,
		bool snapToGround)
	{
		if (snapToGround)
		{
			int high = _sideScrollYFixed >> 8;
			high = (high & parameters.LandingHighMask) +
				parameters.LandingHighOffset;
			_sideScrollYFixed =
				(high << 8) | (_sideScrollYFixed & 0xff);
			_precisePosition.Y = _sideScrollYFixed / 256.0f;
		}

		_sideScrollSpeedZ = 0;
		_sideScrollAirborne = false;
		_sideScrollJumpSoundPending = false;
		SideScrollClimbing = false;
		_airborneLinkAnimationMode = AirborneLinkAnimationMode.None;
		ResetLinkWalkAnimation();
		_walking = false;
		ClearCapeParents();
		_capeJumpEligible = false;
		_sideScrollReducedGravity = false;
		SideScrollActiveTiles terrain =
			_world.SideScrollActiveTiles;
		if (terrain.ActiveTile == parameters.SpikeTile)
			ApplySpikeDamage(_sideScrollAngle);
		_world.PlaySound(parameters.LandSound);
	}

	private void AdvanceSideScrollAirAnimation()
	{
		if (_airborneLinkAnimationMode == AirborneLinkAnimationMode.SideScrollSwim)
		{
			AdvanceSideScrollSwimAnimation();
			return;
		}
		AdvanceNativeLinkAnimation();
	}

	private void SetSideScrollTerrainSpeed(
		int terrainMode,
		int initialSpeed,
		int velocityInterval,
		int targetSpeed,
		bool writeSpeedDirectly)
	{
		if (_sideScrollTerrainMode != terrainMode)
		{
			_sideScrollTerrainMode = terrainMode;
			if (initialSpeed != 0)
				_sideScrollSpeedRaw = initialSpeed;
			_sideScrollVelocityCounter = 0;
			_sideScrollVelocityInterval = velocityInterval;
		}
		_sideScrollTargetSpeedRaw = targetSpeed;
		if (writeSpeedDirectly)
			_sideScrollSpeedRaw = targetSpeed;
	}

	/// <summary>
	/// Exact func_5933 angle/speed convergence used by jumping, ice, and both
	/// swimming views. The byte counter intentionally executes when it is
	/// equal to the interval, not one update later.
	/// </summary>
	private void UpdateSideScrollVelocity(int inputAngle, bool inAir)
	{
		UpdateConvergingVelocity(
			ref _sideScrollAngle,
			ref _sideScrollSpeedRaw,
			_sideScrollTargetSpeedRaw,
			ref _sideScrollVelocityCounter,
			_sideScrollVelocityInterval,
			inputAngle,
			inAir);
	}

	private bool ApplySideScrollVelocity(
		int speed,
		int angle,
		bool allowWallSlide)
	{
		if (angle >= 0x80)
			return false;
		Vector2 resolved = _world.ResolveNativeMovement(
			_precisePosition, speed, angle, allowWallSlide);
		if (resolved == Vector2.Zero)
			return false;
		_precisePosition += resolved;
		_sideScrollYFixed =
			Mathf.FloorToInt(_precisePosition.Y * 256.0f);
		return true;
	}

	private static int SideScrollHorizontalAngle(int angle) =>
		angle switch
		{
			>= 0x01 and <= 0x0f => 0x08,
			>= 0x11 and <= 0x1f => 0x18,
			_ => 0xff
		};

	private static bool IsUpwardAngle(int angle) =>
		angle < 0x80 && ((angle + 4) & ObjectAngle.Mask) < 9;

	private void UpdateFacingFromSideScrollAngle(int angle)
	{
		int horizontal = SideScrollHorizontalAngle(angle);
		if (horizontal == 0x08)
			_facing = Facing.Right;
		else if (horizontal == 0x18)
			_facing = Facing.Left;
	}

	internal void ForceSideScrollSquish(bool vertical = false)
	{
		if (SideScrollSquished || IsDying)
			return;
		_sideScrollSquishVertical = vertical;
		_sideScrollSquishPending = true;
	}

	private void AdvanceSideScrollSquish()
	{
		if (_sideScrollSquishPending)
		{
			_sideScrollSquishPending = false;
			// checkLinkForceState changes state and returns. State11's
			// initialization is dispatched on the following Link update.
			_squishAnimation = new LinkSquishAnimation(_sideScrollSquishVertical, AssetRoot);
			return;
		}
		var squish = _squishAnimation!;
		if (squish.Finished)
		{
			// respawnLink/checkLinkForceState selected state02 last update;
			// its initialization (including the position copy) runs now.
			_squishAnimation = null;
			CancelNativeItemUsage();
			_forcedRespawnPhase=3;
			BeginNativeRespawnWait(collisionsDisabled: true);
			return;
		}
		if (squish.State == 0)
		{
			CancelNativeItemUsage();
			_world.PlaySound(SoundId.SndDamageEnemy);
			_walking = false;
		}
		squish.Advance(_world.FrameCounter);
		Visible = squish.Visible;
		QueueRedraw();
	}

	private void ClearSideScrollState(Vector2 position)
	{
		_sideScrollUpdateAccumulator = 0.0;
		_sideScrollYFixed = Mathf.FloorToInt(position.Y * 256.0f);
		_sideScrollSpeedZ = 0;
		_sideScrollAngle = 0xff;
		_sideScrollSpeedRaw = 0;
		_sideScrollTargetSpeedRaw = 0;
		_sideScrollVelocityCounter = 0;
		_sideScrollVelocityInterval = 0;
		_sideScrollTerrainMode = -1;
		_sideScrollForceIcePhysics = 0;
		_sideScrollSwimmingState = 0;
		_sideScrollSwimBurstState = 0;
		_sideScrollSwimBurstCounter = 0;
		_sideScrollSwimAnimationFrame = 0;
		_sideScrollSwimAnimationCounter = 0;
		_sideScrollSwimMermaidAnimation = false;
		_sideScrollMermaidImpulseCounter = 0;
		_sideScrollBubbleCounter = 0;
		_sideScrollSquishVertical = false;
		ClearCapeParents();
		_capeJumpEligible = false;
		_sideScrollReducedGravity = false;
		_sideScrollAirborne = false;
		_airborneLinkAnimationMode = AirborneLinkAnimationMode.None;
		_sideScrollJumpSoundPending = false;
		SideScrollClimbing = false;
	}
}
