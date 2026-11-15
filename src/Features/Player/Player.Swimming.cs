namespace Oracles;

public partial class Player
{
	/// <summary>
	/// Port of the non-side-view Flippers/Mermaid linkUpdateSwimming states.
	/// This includes Mermaid Suit impulses and normal-water linkUpdateDiving;
	/// the transition owner may consume source-placed dive interactions.
	/// Deep-water tile transitions remain owned by the transition subsystem.
	/// </summary>
	private bool TryAdvanceTopDownSwimming(
		Vector2 input,
		int entryAngle,
		Vector2 movementStart,
		bool attackJustPressed,
		bool diveJustPressed,
		ActiveTerrainInfo? terrainBeforeMotion = null)
	{
		// commonCode.s:@swimming does not initialize surface swimming when
		// var2f bit 7 marks Link underwater, even on water/sea/whirlpool tiles.
		if (_topDownAirborne || _world.RidingObject ||
			_minecartRideControlled || _companionRideControlled)
		{
			ClearTopDownSwimmingState();
			return false;
		}

		ActiveTerrainInfo activeTerrain = terrainBeforeMotion ?? _world.GetActiveTerrain(Position);
		if (!terrainBeforeMotion.HasValue)
		{
			// Landing reapplies linkApplyTileTypes before resuming movement.
			ApplyTopDownTerrainMotion(activeTerrain.Terrain.Type, NativeAdjacentWalls);
			Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		}
		// @tileType_lava sets swimming state $41 before ordinary movement;
		// overworldSwimmingState1 then requests drowning even with Flippers,
		// Mermaid Suit, or var2f bit 7. It does not use the water entry gate.
		if (activeTerrain.Terrain.Hazard == HazardType.Lava)
		{
			ClearTopDownSwimmingState();
			_world.ClearItemParents(this);
			ClearShieldParent();
			StartDrowning(HazardType.Lava);
			return true;
		}
		if (_world.Underwater)
		{
			ClearTopDownSwimmingState();
			return false;
		}
		if (activeTerrain.Terrain.Hazard != HazardType.Water)
		{
			ClearTopDownSwimmingState();
			return false;
		}
		NativeAdjacentWalls = _world.GetAdjacentWallsBitset(_precisePosition);

		// checkSwimmingOverSeawater returns nonzero with var2f bit 6;
		// ordinary Flippers still drown on TILETYPE_SEAWATER ($fc).
		bool seaWaterWithoutMermaid = activeTerrain.Terrain.Type == TerrainType.SeaWater &&
			!HasMermaidSuit;
		bool hasFlippers =
			_inventory.HasTreasure(TreasureId.Flippers);
		if (seaWaterWithoutMermaid || !hasFlippers)
		{
			ClearTopDownSwimmingState();
			_world.ClearItemParents(this);
			ClearShieldParent();
			StartDrowning(HazardType.Water);
			return true;
		}

		if (_topDownSwimmingState is 0 or 1)
		{
			BeginTopDownSwimming(entryAngle);
			FinalizeTopDownSwimmingUpdate(movementStart);
			return true;
		}

		if (_topDownSwimmingState == 2)
		{
			_topDownSwimmingEntryCounter =
				(_topDownSwimmingEntryCounter - 1) & 0xff;
			if (_topDownSwimmingEntryCounter != 0)
			{
				ApplyTopDownSwimMomentum();
				FinalizeTopDownSwimmingUpdate(movementStart);
				return true;
			}
			_topDownSwimmingState = 3;
		}

		AdvanceTopDownSwimmingAnimation();
		// linkUpdateDiving animates first, then the already-diving $fc path
		// returns from overworldSwimmingState3 before timer, input or motion.
		if (TopDownDiving && activeTerrain.Terrain.Tile == 0xfc &&
			_world.RequestDeepWaterDive(this, activeTerrain.PackedPosition))
		{
			FinalizeTopDownSwimmingUpdate(movementStart);
			return true;
		}
		UpdateTopDownDiving(diveJustPressed);
		int inputAngle = AngleForVector(input);
		if (inputAngle < 0x80)
			UpdateFacing(input);
		if (HasMermaidSuit)
			UpdateTopDownMermaidSuit(inputAngle);
		else UpdateTopDownFlippers(inputAngle, attackJustPressed);
		ApplyTopDownSwimMomentum();
		FinalizeTopDownSwimmingUpdate(movementStart);
		return true;
	}

	private void BeginTopDownSwimming(int entryAngle)
	{
		TopDownSwimmingParameters parameters =
			_topDownSwimmingData.Parameters;
		int baseSpeed = RingEffects.UsesFastSwim(_inventory)
			? parameters.FastSpeed
			: parameters.BaseSpeed;
		// overworldSwimmingState1 calls linkCancelAllItemUsage: drop held
		// objects and clear parents, retaining physical items until their pass.
		_world.ClearItemParents(this);
		ClearShieldParent();
		CancelSwordAttack();
		CancelShovelAction();
		_topDownSwimmingState = 2;
		_topDownSwimmingEntryCounter = HasMermaidSuit
			? MermaidSwimming.MermaidEntryUpdates : parameters.EntryUpdates;
		_topDownMermaidVelocityInitialized = false;
		_topDownSwimAngle = entryAngle;
		_topDownSwimSpeedRaw = baseSpeed;
		_topDownSwimTargetSpeedRaw = baseSpeed;
		// linkSetSwimmingSpeed writes speed/target and var13=$03, retaining
		// var12. Re-entering water must preserve its convergence phase.
		_topDownSwimBurstState = 0;
		_topDownSwimBurstCounter = 0;
		_topDownSwimAnimationFrame = 0;
		_topDownSwimAnimationCounter =
			parameters.AnimationFrameDurations[0];
		_topDownDiving = false;
		if (ZIndex == DivingZIndex)
			ZIndex = NormalZIndex;
		_walking = false;

		ResetLinkWalkAnimation();
		_world.SpawnDrowningSplash(Position, HazardType.Water);
		QueueRedraw();
	}

	private void ApplyTopDownTerrainMotion(TerrainType terrain, int adjacentWalls)
	{
		bool conveyor = terrain is TerrainType.UpConveyor or TerrainType.RightConveyor or
			TerrainType.DownConveyor or TerrainType.LeftConveyor;
		// @tileType_conveyor checks QUICKSAND_RING; @tileType_current enters
		// @adjustLinkOnConveyor directly and therefore bypasses that check.
		if (conveyor && (_world.RidingObject || RingEffects.IgnoresQuicksand(_inventory))) return;
		int angle = terrain switch
		{
			TerrainType.UpCurrent or TerrainType.UpConveyor => ObjectAngle.Up,
			TerrainType.RightCurrent or TerrainType.RightConveyor => ObjectAngle.Right,
			TerrainType.DownCurrent or TerrainType.DownConveyor => ObjectAngle.Down,
			TerrainType.LeftCurrent or TerrainType.LeftConveyor => ObjectAngle.Left,
			_ => 0xff
		};
		if (angle >= 0x80)
			return;

		// linkApplyTileTypes@tileType_current applies SPEED_c0 before it
		// enters the shared water branch. Swimming momentum is then applied
		// independently by linkUpdateSwimming on the same update.
		ApplyTopDownObjectSpeed(
			conveyor ? ObjectSpeed.Speed80 : ObjectSpeed.Speedc0,
			angle,
			allowWallSlide: true,
			adjacentWalls: adjacentWalls);
		_screenTransitionTerrainMotion = true;
	}

	private void UpdateTopDownMermaidSuit(int inputAngle)
	{
		var parameters = MermaidSwimming;
		// updateLinkSpeed_withParam($98) preserves speed but resets var12
		// once on entering Mermaid velocity mode and writes var13=$05.
		if (!_topDownMermaidVelocityInitialized)
		{
			_topDownMermaidVelocityInitialized = true;
			_topDownSwimVelocityCounter = 0;
		}
		_topDownSwimTargetSpeedRaw = RingEffects.UsesFastSwim(_inventory)
			? parameters.MermaidFastTargetSpeed : parameters.MermaidTargetSpeed;
		UpdateMermaidImpulseVelocity(ref _topDownSwimAngle, ref _topDownSwimSpeedRaw,
			_topDownSwimTargetSpeedRaw, ref _topDownSwimVelocityCounter, inputAngle);
	}

	private void UpdateMermaidImpulseVelocity(ref int angle, ref int speed, int target,
		ref int velocityCounter, int inputAngle, bool movementAllowed = true)
	{
		var parameters = MermaidSwimming;
		bool directionPressed = movementAllowed && !_world.MovementDisabled && DirectionalInputJustPressed();
		if (directionPressed)
		{
			_world.PlaySound(SoundId.SndSplash);
			_topDownMermaidImpulseCounter = parameters.MermaidImpulseUpdates;
		}
		else
		{
			_topDownMermaidImpulseCounter = (_topDownMermaidImpulseCounter - 1) & 0xff;
			if ((_topDownMermaidImpulseCounter & 0x80) != 0)
			{
				_topDownMermaidImpulseCounter = 0xff;
				UpdateConvergingVelocity(ref angle, ref speed, target, ref velocityCounter,
					parameters.MermaidVelocityInterval, 0xff, inAir: false);
				return;
			}
		}
		velocityCounter = parameters.MermaidImpulseCounter;
		UpdateConvergingVelocity(ref angle, ref speed, target, ref velocityCounter,
			parameters.MermaidVelocityInterval, inputAngle, inAir: false);
	}

	private void UpdateTopDownFlippers(
		int inputAngle,
		bool attackJustPressed)
	{
		TopDownSwimmingParameters parameters =
			_topDownSwimmingData.Parameters;
		int baseSpeed = RingEffects.UsesFastSwim(_inventory)
			? parameters.FastSpeed
			: parameters.BaseSpeed;
		if (_topDownSwimBurstState == 0)
		{
			if (!attackJustPressed)
			{
				_topDownSwimTargetSpeedRaw = baseSpeed;
				UpdateTopDownSwimVelocity(inputAngle, inAir: false);
				return;
			}

			_topDownSwimBurstState = 1;
			int facingAngle = ((int)_facing * 8) & 0x1f;
			for (int update = 0;
				update < parameters.BurstTurnUpdates;
				update++)
			{
				UpdateTopDownSwimVelocity(facingAngle, inAir: false);
			}
			_topDownSwimBurstCounter = parameters.BurstAccelerateUpdates;
			_world.PlaySound(parameters.SwimSound);
		}

		int speedStep = _topDownSwimBurstState == 1
			? parameters.BurstSpeedStep
			: -parameters.BurstSpeedStep;
		_topDownSwimBurstCounter =
			(_topDownSwimBurstCounter - 1) & 0xff;
		if (_topDownSwimBurstCounter == 0)
		{
			if (_topDownSwimBurstState == 1)
			{
				_topDownSwimBurstState = 2;
				_topDownSwimBurstCounter =
					parameters.BurstDecelerateUpdates;
			}
			else
			{
				_topDownSwimBurstState = 0;
				_topDownSwimSpeedRaw = baseSpeed;
				_topDownSwimTargetSpeedRaw = baseSpeed;
				UpdateTopDownSwimVelocity(
					inputAngle < 0x80
						? inputAngle
						: ((int)_facing * 8) & 0x1f,
					inAir: false);
				return;
			}
		}

		if ((_topDownSwimBurstCounter & 0x03) == 0)
		{
			_topDownSwimTargetSpeedRaw = Math.Max(
				0,
				_topDownSwimTargetSpeedRaw + speedStep);
		}

		UpdateTopDownSwimVelocity(
			inputAngle < 0x80
				? inputAngle
				: ((int)_facing * 8) & 0x1f,
			inAir: false);
	}

	private void AdvanceTopDownSwimmingAnimation()
	{
		if (--_topDownSwimAnimationCounter > 0)
			return;
		_topDownSwimAnimationFrame ^= 1;
		int[] durations = TopDownDiving
			? _topDownSwimmingData.Parameters.DiveAnimationFrameDurations
			: _topDownSwimmingData.Parameters.AnimationFrameDurations;
		_topDownSwimAnimationCounter =
			durations[_topDownSwimAnimationFrame];
	}

	private void UpdateTopDownDiving(bool diveJustPressed)
	{
		if (diveJustPressed)
		{
			if (TopDownDiving)
				SurfaceFromTopDownDive();
			else
				BeginTopDownDive();
			return;
		}

		// linkUpdateDiving decrements counter2 even while surfaced. Its
		// byte wrap periodically resets SWIM animation; pressing B to surface
		// retains the remaining counter rather than clearing it.
		if (RingEffects.RemovesDiveTimer(_inventory))
			return;

		_topDownDiveCounter = (_topDownDiveCounter - 1) & 0xff;
		if (_topDownDiveCounter == 0)
			SurfaceFromTopDownDive();
	}

	private void BeginTopDownDive()
	{
		TopDownSwimmingParameters parameters =
			_topDownSwimmingData.Parameters;
		_topDownDiving = true;
		_topDownDiveCounter = parameters.DiveUpdates;
		_topDownSwimAnimationFrame = 0;
		_topDownSwimAnimationCounter =
			parameters.DiveAnimationFrameDurations[0];
		ZIndex = DivingZIndex;
		_world.SpawnDrowningSplash(Position, HazardType.Water);
		QueueRedraw();
	}

	private void SurfaceFromTopDownDive()
	{
		_topDownDiving = false;
		_topDownSwimAnimationFrame = 0;
		_topDownSwimAnimationCounter =
			_topDownSwimmingData.Parameters.AnimationFrameDurations[0];
		if (ZIndex == DivingZIndex)
			ZIndex = NormalZIndex;
		QueueRedraw();
	}

	private void ApplyTopDownSwimMomentum()
	{
		if (_topDownSwimAngle >= 0x80 || _topDownSwimSpeedRaw == 0)
			return;
		ApplyTopDownObjectSpeed(
			_topDownSwimSpeedRaw,
			_topDownSwimAngle,
			allowWallSlide: true);
	}

	private void FinalizeTopDownSwimmingUpdate(Vector2 movementStart)
	{
		_walking = false;

		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		if (!_world.CheckTileWarp(this))
			_world.CheckRoomExit(this);
		AdvanceTransformationAnimation(walking: false);
		QueueRedraw();
	}

	private void ClearTopDownSwimmingState()
	{
		_topDownMermaidVelocityInitialized = false;
		_topDownSwimmingState = 0;
		_topDownSwimmingEntryCounter = 0;
		_topDownSwimAngle = 0xff;
		_topDownSwimSpeedRaw = 0;
		_topDownSwimTargetSpeedRaw = 0;
		_topDownSwimBurstState = 0;
		_topDownSwimBurstCounter = 0;
		_topDownSwimAnimationFrame = 0;
		_topDownSwimAnimationCounter = 0;
		_topDownDiving = false;
		if (ZIndex == DivingZIndex)
			ZIndex = NormalZIndex;
	}

	internal void AdvanceTopDownSwimmingUpdateForValidation(
		Vector2 movementInput = default,
		int entryAngle = 0xff,
		bool attackJustPressed = false,
		bool diveJustPressed = false)
	{
		if (_world.SideScrolling)
		{
			throw new InvalidOperationException(
				"A top-down Link swimming update was requested in a side-scrolling room.");
		}
		Vector2 movementStart = _precisePosition;
		_lastMovementInput = movementInput;
		if (!TryAdvanceTopDownSwimming(
				movementInput,
				entryAngle,
				movementStart,
				attackJustPressed,
				diveJustPressed))
		{
			throw new InvalidOperationException(
				"The validation top-down swimming update was not handled as water.");
		}
	}

	private void AdvanceSideScrollSwimming(
		Vector2 input,
		bool movementAllowed,
		bool attackJustPressed,
		bool directionJustPressed,
		SideScrollPlayerParameters parameters)
	{
		_sideScrollAirborne = false;
		SideScrollClimbing = false;
		_airborneLinkAnimationMode = AirborneLinkAnimationMode.None;
		ResetLinkWalkAnimation();
		_sideScrollSpeedZ = 0;
		_sideScrollReducedGravity = false;
		ClearCapeParents();
		_capeJumpEligible = false;
		_walking = false;

		if (_sideScrollSwimmingState == 1)
		{
			InterruptCarriedItems(discard: true);
			CancelSwordAttack();
			CancelShovelAction();
			_sideScrollSwimmingState = 2;
			int swimSpeed = RingEffects.UsesFastSwim(_inventory)
				? parameters.FastSwimSpeed
				: parameters.SwimSpeed;
			TopDownSwimmingParameters sharedSwimmingParameters =
				_topDownSwimmingData.Parameters;
			_sideScrollSpeedRaw = swimSpeed;
			_sideScrollTargetSpeedRaw = swimSpeed;
			_sideScrollVelocityInterval =
				sharedSwimmingParameters.VelocityInterval;
			// linkSetSwimmingSpeed writes var13=$03 after two LDI stores
			// starting at speed ($10); var12 and var36 remain intact.
			_sideScrollSwimBurstState = 0;
			if (!_inventory.HasTreasure(TreasureId.Flippers))
			{
				_sideScrollSwimmingState = 3;
				StartSideScrollDrowningWithoutEntryEffects();
			}
			else
			{
				_sideScrollSwimMermaidAnimation = HasMermaidSuit;
				_sideScrollSwimAnimationFrame = 0;
				_sideScrollSwimAnimationCounter =
					_sideScrollPlayerData.SwimmingFrame(
						_sideScrollSwimMermaidAnimation,
						frame: 0,
						direction: (int)_facing).Duration;
			}
			return;
		}

		if (_sideScrollSwimmingState == 3)
			return;

		if (movementAllowed && !_world.MovementDisabled)
		{
			int inputAngle = AngleForVector(input);
			// State 2 turns from wLinkAngle BEFORE the Flippers burst's
			// eight convergence calls, not from the lagging object angle.
			UpdateFacingFromSideScrollAngle(inputAngle);
			_facing = (Facing)((int)_facing | 1);
			bool mermaidSuit = HasMermaidSuit;
			if (mermaidSuit)
			{
				UpdateSideScrollMermaidSuit(
					inputAngle,
					movementAllowed,
					directionJustPressed);
			}
			else
			{
				UpdateSideScrollFlippers(
					inputAngle, attackJustPressed, parameters);
			}

			if (_sideScrollAngle < 0x80 && _sideScrollSpeedRaw != 0)
			{
				ApplySideScrollVelocity(
					_sideScrollSpeedRaw,
					_sideScrollAngle,
					allowWallSlide: true);
				_walking = inputAngle < 0x80;
			}
			_sideScrollYFixed =
				Mathf.FloorToInt(_precisePosition.Y * 256.0f);
		}

		_sideScrollBubbleCounter =
			(_sideScrollBubbleCounter - 1) & 0xff;
		if ((_sideScrollBubbleCounter & 0x80) != 0)
		{
			// Link consumes the interval draw now. INTERAC_BUBBLE $91:$00
			// consumes its separate turn-direction draw in the later pass.
			_sideScrollBubbleCounter =
				(_random.Next().Value & 0x1f) + 50;
			_world.SpawnSideScrollBubble(_precisePosition);
		}
		AdvanceSideScrollSwimAnimation();
	}

	private void UpdateSideScrollFlippers(
		int inputAngle,
		bool attackJustPressed,
		SideScrollPlayerParameters parameters)
	{
		TopDownSwimmingParameters swimmingParameters =
			_topDownSwimmingData.Parameters;
		int baseSpeed = RingEffects.UsesFastSwim(_inventory)
			? parameters.FastSwimSpeed
			: parameters.SwimSpeed;
		if (_sideScrollSwimBurstState == 0)
		{
			if (!attackJustPressed)
			{
				_sideScrollTargetSpeedRaw = baseSpeed;
				UpdateSideScrollVelocity(inputAngle, inAir: false);
				return;
			}

			_sideScrollSwimBurstState = 1;
			for (int update = 0;
				 update < swimmingParameters.BurstTurnUpdates;
				 update++)
			{
				UpdateSideScrollVelocity(
					((int)_facing * 8) & 0x1f,
					inAir: false);
			}
			_sideScrollSwimBurstCounter =
				swimmingParameters.BurstAccelerateUpdates;
			_world.PlaySound(swimmingParameters.SwimSound);
		}

		// The transition update retains the outgoing state's C register:
		// state1 adds +5 when installing state2's $0c counter.
		int speedStep = _sideScrollSwimBurstState == 1
			? swimmingParameters.BurstSpeedStep
			: -swimmingParameters.BurstSpeedStep;
		_sideScrollSwimBurstCounter =
			(_sideScrollSwimBurstCounter - 1) & 0xff;
		if (_sideScrollSwimBurstCounter == 0)
		{
			if (_sideScrollSwimBurstState == 1)
			{
				_sideScrollSwimBurstState = 2;
				_sideScrollSwimBurstCounter =
					swimmingParameters.BurstDecelerateUpdates;
			}
			else
			{
				_sideScrollSwimBurstState = 0;
				_sideScrollSpeedRaw = baseSpeed;
				_sideScrollTargetSpeedRaw = baseSpeed;
				_sideScrollVelocityInterval = swimmingParameters.VelocityInterval;
				UpdateSideScrollVelocity(
					inputAngle < 0x80
						? inputAngle
						: ((int)_facing * 8) & 0x1f,
					inAir: false);
				return;
			}
		}

		if ((_sideScrollSwimBurstCounter & 0x03) == 0)
		{
			_sideScrollTargetSpeedRaw =
				Math.Max(0, _sideScrollTargetSpeedRaw + speedStep);
		}

		UpdateSideScrollVelocity(
			inputAngle < 0x80
				? inputAngle
				: ((int)_facing * 8) & 0x1f,
			inAir: false);
	}

	private void UpdateSideScrollMermaidSuit(
		int inputAngle,
		bool movementAllowed,
		bool directionJustPressed)
	{
		var parameters = MermaidSwimming;
		SetSideScrollTerrainSpeed(
			terrainMode: 0x10,
			initialSpeed: 0,
			velocityInterval: parameters.MermaidVelocityInterval,
			targetSpeed: RingEffects.UsesFastSwim(_inventory)
				? parameters.MermaidFastTargetSpeed
				: parameters.MermaidTargetSpeed,
			writeSpeedDirectly: false);

		if (movementAllowed && directionJustPressed)
		{
			_world.PlaySound(SoundId.SndSplash);
			_sideScrollMermaidImpulseCounter = parameters.MermaidImpulseUpdates;
		}
		else
		{
			_sideScrollMermaidImpulseCounter =
				(_sideScrollMermaidImpulseCounter - 1) & 0xff;
			if ((_sideScrollMermaidImpulseCounter & 0x80) != 0)
			{
				_sideScrollMermaidImpulseCounter = 0xff;
				UpdateSideScrollVelocity(0xff, inAir: false);
				return;
			}
		}

		_sideScrollVelocityCounter = parameters.MermaidImpulseCounter;
		UpdateSideScrollVelocity(inputAngle, inAir: false);
	}

	private void AdvanceSideScrollSwimAnimation()
	{
		if (--_sideScrollSwimAnimationCounter > 0)
			return;
		_sideScrollSwimAnimationFrame ^= 1;
		_sideScrollSwimAnimationCounter =
			_sideScrollPlayerData.SwimmingFrame(
				_sideScrollSwimMermaidAnimation,
				_sideScrollSwimAnimationFrame,
				(int)_facing).Duration;
	}

	private void StartSideScrollDrowningWithoutEntryEffects()
	{
		_drowningHazard = HazardType.Water;
		_drowning = true;
		_drownRespawning = false;
		_walking = false;
		CancelSwordAttack();
		CancelShovelAction();
		Visible = true;
		SetScriptedLinkAnimationMode(null);
		LinkNativeAnimationDatabase.For(AssetRoot).Set(_inventory.RuntimeState, 0x0a);
		QueueRedraw();
	}

	private void UpdateTopDownSwimVelocity(int inputAngle, bool inAir)
	{
		UpdateConvergingVelocity(
			ref _topDownSwimAngle,
			ref _topDownSwimSpeedRaw,
			_topDownSwimTargetSpeedRaw,
			ref _topDownSwimVelocityCounter,
			_topDownMermaidVelocityInitialized ? MermaidSwimming.MermaidVelocityInterval :
				_topDownSwimmingData.Parameters.VelocityInterval,
			inputAngle,
			inAir);
	}
}
