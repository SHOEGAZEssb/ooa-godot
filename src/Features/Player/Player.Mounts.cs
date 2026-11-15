namespace Oracles;

public partial class Player
{
	internal int AdvanceRaftKnockback()
	{
		if (!_raftRideControlled || _enemyKnockbackFrames <= 0)
			return 0xff;
		// SPECIALOBJECT_RAFT decrements Link's counter and still moves on zero.
		_enemyKnockbackFrames = Mathf.Max(0, _enemyKnockbackFrames - 1);
		return _enemyKnockbackAngle;
	}

	/// <summary>
	/// INTERAC_MINECART and SPECIALOBJECT_MINECART set wLinkInAir=$81,
	/// SPEED_80, and speedZ=-$01c0 for both boarding and dismounting. Bit 7
	/// permits the fixed movement through the cart/platform collision.
	/// </summary>
	internal void BeginMinecartJump(
		Vector2 position,
		int angle,
		int initialZ)
	{
		if (angle is < 0 or >= OracleObjectSpeedTable.AngleCount)
			throw new ArgumentOutOfRangeException(nameof(angle));

		// Preserve live parents while the interaction forces the airborne arc.
		InterruptCarriedItems(discard: true, preserveSeedShooter: true, preserveBoomerang: true);
		CancelShovelAction();
		ClearShieldParent();
		ClearTopDownAirState();
		_enemyKnockbackFrames = 0.0f;
		_pendingSwordKnockbackFrames = 0;
		_precisePosition = position;
		Position = OracleObjectMath.ToPixelPosition(position);
		_topDownAirZFixed = initialZ << 8;
		_topDownAirSpeedZ = -0x01c0;
		// INTERAC$16 changes air/speed after Link; the first airborne Link
		// visit selects JUMP. This tail still uses the preceding WALK pose.
		_airborneLinkAnimationMode = AirborneLinkAnimationMode.Walk;
		_topDownAirborne = true;
		_topDownJumpSoundPending = true;
		_minecartJumpAngle = angle;
		_minecartJumpControlled = true;
		_minecartRideControlled = false;

		QueueRedraw();
	}

	internal void FinishMinecartMount(
		Vector2 cartPosition,
		int cartDirection,
		int animationParameter)
	{
		ClearNativeParentAnimationClaims();
		_world.Pegasus?.Clear();
		ClearTopDownAirState();
		SetMinecartRidePosition(
			cartPosition,
			cartDirection,
			animationParameter,
			Vector2.Zero);
		_world.PlaySound(TopDownAirDatabase.For(AssetRoot).Parameters.LandSound);
	}

	/// <summary>
	/// companionTryToMount sets Link's vertical speed to -$01c0 while the
	/// companion nudges each high coordinate toward its own center.
	/// </summary>
	internal void BeginCompanionMount(Vector2 position)
	{
		// commonCode.s:setLinkMountingSpeed publishes the native transition mask.
		_inventory.RuntimeState.SetField("wDisableScreenTransitions", 0x81);
		BeginCompanionJump(
			position,
			initialZ: 0,
			angle: 0xff,
			dismount: false);
	}

	internal void NudgeCompanionMountToward(Vector2 target)
	{
		if (!_companionJumpControlled)
			return;
		int x = Mathf.FloorToInt(_precisePosition.X);
		int y = Mathf.FloorToInt(_precisePosition.Y);
		int targetX = Mathf.FloorToInt(target.X);
		int targetY = Mathf.FloorToInt(target.Y);
		if (x != targetX)
			x += x < targetX ? 1 : -1;
		if (y != targetY)
			y += y < targetY ? 1 : -1;
		// companionUpdateMounting nudges yh/xh only, retaining Link's low bytes.
		Vector2 fraction = _precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition);
		_precisePosition = new Vector2(x, y) + fraction;
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void FinishCompanionMount(
		Vector2 companionPosition,
		Vector2 linkOffset,
		int direction,
		int zFixed,
		Texture2D linkTexture,
		Texture2D damageLinkTexture,
		Vector2 textureOffset)
	{
		int retainedZLow = _topDownAirZFixed & 0xff;
		ClearTopDownAirState();
		_companionRideZFixed = retainedZLow;
		SetCompanionRidePosition(
			companionPosition, linkOffset, direction, zFixed, linkTexture,
			damageLinkTexture, textureOffset, Vector2.Zero);
	}

	internal void SetCompanionRidePosition(
		Vector2 companionPosition,
		Vector2 linkOffset,
		int direction,
		int zFixed,
		Texture2D linkTexture,
		Texture2D damageLinkTexture,
		Vector2 textureOffset,
		Vector2 screenOffset)
	{
		if (direction is < 0 or > 3)
			throw new ArgumentOutOfRangeException(nameof(direction));
		// func_410d@companion uses objectCopyPositionWithOffset: copy high
		// coordinates with BC offsets and retain Link's own fractional bytes.
		Vector2 fraction = _precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition);
		_precisePosition = OracleObjectMath.ToPixelPosition(companionPosition) + fraction + linkOffset;
		_companionRideTexture = linkTexture;
		_damageCompanionRideTexture = damageLinkTexture;
		_companionRideTextureOffset = textureOffset;
		// The same copy writes zh only, never the companion's fractional Z.
		_companionRideZFixed = (zFixed & ~0xff) | (_companionRideZFixed & 0xff);
		_companionRideControlled = true;
		_facing = (Facing)direction;
		_walking = false;

		Position = OracleObjectMath.ToPixelPosition(_precisePosition) + screenOffset;
		QueueRedraw();
	}

	internal void BeginCompanionDismount(
		Vector2 companionPosition,
		int companionDirection)
	{
		if (companionDirection is < 0 or > 3)
		{
			throw new ArgumentOutOfRangeException(
				nameof(companionDirection));
		}
		TopDownAirParameters parameters = TopDownAirDatabase.For(AssetRoot).Parameters;
		_facing = (Facing)companionDirection;
		_lastSafePosition = OracleObjectMath.ToPixelPosition(companionPosition);
		_localRespawnFacing = _facing;
		_companionRideControlled = false;
		_inventory.RuntimeState.SetField("wLinkObjectIndex", 0xd0);
		// companionDismount clears this after setLinkMountingSpeed's $81 write.
		_inventory.RuntimeState.SetField("wDisableScreenTransitions", 0);
		BeginCompanionJump(
			// companionDismount's objectCopyPosition also retains Link's low XY.
			OracleObjectMath.ToPixelPosition(companionPosition) +
				_precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition),
			initialZ: parameters.CompanionDismountZ,
			angle: parameters.CompanionDismountAngle,
			dismount: true);
	}

	internal void ApplyCompanionHazardDamage(HazardType hazard)
	{
		ApplyDamage(
			1,
			hazard == HazardType.Hole
				? RingDamageSource.Hole
				: RingDamageSource.TerrainHazard);
		// companionRespawn writes $40 after damageToApply=-2 when Link is
		// still mounted.
		_enemyInvincibilityFrames = 0x40;
	}

	private void BeginCompanionJump(
		Vector2 position,
		int initialZ,
		int angle,
		bool dismount)
	{
		if (angle != 0xff &&
			angle is < 0 or >= OracleObjectSpeedTable.AngleCount)
		{
			throw new ArgumentOutOfRangeException(nameof(angle));
		}

		// setLinkMountingSpeed does not clear zl; dismount's copy changes zh
		// to $f8 while retaining the mounted Link's existing fractional byte.
		int retainedZLow = (dismount ? _companionRideZFixed : _topDownAirZFixed) & 0xff;
		InterruptCarriedItems(discard: true);
		CancelSwordAttack();
		CancelShovelAction();
		ClearShieldParent();
		ClearTopDownAirState();
		_enemyKnockbackFrames = 0.0f;
		_pendingSwordKnockbackFrames = 0;
		_precisePosition = position;
		Position = OracleObjectMath.ToPixelPosition(position);
		TopDownAirParameters parameters = TopDownAirDatabase.For(AssetRoot).Parameters;
		_topDownAirZFixed = (initialZ << 8) | retainedZLow;
		_topDownAirSpeedZ = parameters.CompanionJumpSpeedZ;
		// companionDismount changes Link back to SPECIALOBJECT_LINK state 0.
		// Its same-pass state-0 update is standing; @startedJump selects the
		// jump animation on the following update. Mounting does not reset the
		// Link object and therefore selects its jump pose immediately.
		_airborneLinkAnimationMode = dismount
			? AirborneLinkAnimationMode.None
			: AirborneLinkAnimationMode.Jump;
		_topDownAirborne = true;
		// setLinkMountingSpeed writes wLinkInAir=$81 for mounting and
		// dismounting. Bit 0 requests SND_JUMP on the first in-air update.
		_topDownJumpSoundPending = true;
		_companionJumpAngle = angle;
		_companionJumpControlled = true;
		_companionDismountJump = dismount;
		_companionJumpAnimationDeferred = dismount;
		_walking = false;

		QueueRedraw();
	}

	private bool AdvanceCompanionJumpUpdate()
	{
		TopDownAirParameters parameters = TopDownAirDatabase.For(AssetRoot).Parameters;
		if (_companionJumpAnimationDeferred)
		{
			_companionJumpAnimationDeferred = false;
			// The companion ran first and reset Link to state 0. This
			// same-pass initialization does not integrate the dismount arc.
			return false;
		}
		if (_companionDismountJump)
			_airborneLinkAnimationMode = AirborneLinkAnimationMode.Jump;
		if (_topDownJumpSoundPending)
		{
			_topDownJumpSoundPending = false;
			_world.PlaySound(parameters.JumpSound);
			InitializeNativeJumpAnimation();
		}

		// setLinkMountingSpeed initially writes direction*8 to wLinkAngle,
		// but companionDismount then writes $ff to w1Link.angle. The airborne
		// movement path consumes the object angle, so an ordinary animal
		// dismount has no lateral motion. Mounting also uses $ff while the
		// companion nudges Link toward its center.
		if (_companionJumpAngle != 0xff)
		{
			_precisePosition += OracleObjectMovement.Shared.Delta(
				parameters.CompanionJumpSpeedRaw,
				_companionJumpAngle);
		}
		bool landed = OracleObjectMath.UpdateSpeedZ(
			ref _topDownAirZFixed,
			ref _topDownAirSpeedZ,
			parameters.Gravity);
		if (landed)
		{
			bool resumeOrdinaryMovement = _companionDismountJump;
			// checkUseItems observes wLinkInAir bit7 before linkUpdateInAir
			// clears it on landing. Ordinary movement can resume below, but
			// that terminal update cannot allocate a new equipped parent.
			_itemAllocationBlockedThisUpdate = true;
			ClearTopDownAirState();
			_world.PlaySound(parameters.LandSound);
			Position = OracleObjectMath.ToPixelPosition(_precisePosition);
			QueueRedraw();
			return resumeOrdinaryMovement;
		}
		if (_topDownAirSpeedZ > parameters.MaximumFallSpeed)
			_topDownAirSpeedZ = parameters.MaximumFallSpeed;
		AdvanceNativeLinkAnimation();
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
		return false;
	}

	internal void SetMinecartRidePosition(
		Vector2 cartPosition,
		int cartDirection,
		int animationParameter,
		Vector2 screenOffset)
	{
		if (cartDirection is < 0 or > 3)
			throw new ArgumentOutOfRangeException(nameof(cartDirection));

		bool secondFrame = animationParameter != 0;
		Vector2 linkOffset = !secondFrame
			? new Vector2(0, -9)
			: cartDirection switch
			{
				0 or 2 => new Vector2(-1, -9),
				1 or 3 => new Vector2(0, -8),
				_ => throw new InvalidOperationException()
			};
		// func_410d copies only yh/xh with the animation offset. Link retains
		// his own fractional bytes; the scrolling offset is presentation.
		Vector2 fraction = _precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition);
		_precisePosition = OracleObjectMath.ToPixelPosition(cartPosition) + fraction + linkOffset;
		_minecartMainObjectPosition = cartPosition;
		Position = OracleObjectMath.ToPixelPosition(_precisePosition) + screenOffset;
		_minecartRideControlled = true;
		_walking = false;

		QueueRedraw();
	}

	internal void SetMinecartMainObjectPosition(Vector2 position) =>
		_minecartMainObjectPosition = position;

	internal void BeginRaftRide(Vector2 position)
	{
		_inventory.RuntimeState.SetField("wLinkObjectIndex", 0xd1);
		InterruptCarriedItems(discard: true);
		CancelShovelAction();
		// SPECIALOBJECT_RAFT state0 initializes only the companion. Preserve
		// Link's remaining Feather gravity/landing and do not call
		// animateLinkStanding with the preceding Pegasus signal bit.
		_raftRideControlled = true;
		SetRaftRidePosition(position, animationParameter: 0,
			Vector2.Zero);
	}

	internal void SetRaftRidePosition(
		Vector2 position,
		int animationParameter,
		Vector2 screenOffset)
	{
		Vector2 fraction = _precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition);
		_precisePosition = OracleObjectMath.ToPixelPosition(position) + fraction +
			new Vector2(0, animationParameter == 0 ? -5 : -6);
		// func_410d @ridingRaft copies only position, leaving Link's own
		// direction (including item turning) independent of the raft animation.
		_raftRideControlled = true;
		_walking = false;

		Position = OracleObjectMath.ToPixelPosition(_precisePosition) + screenOffset;
		QueueRedraw();
	}

	internal void EndRaftRide(Vector2 position, int direction)
	{
		_inventory.RuntimeState.SetField("wLinkObjectIndex", 0xd0);
		_raftRideControlled = false;
		_precisePosition = position;
		_facing = (Facing)direction;
		Position = OracleObjectMath.ToPixelPosition(position);
		QueueRedraw();
	}

	internal void SetRaftwreckCutscenePosition(Vector2 position, int direction)
	{
		// objectCopyPosition writes only coordinate high bytes, including
		// the controller's direct-Link path when wLinkObjectIndex=$d0.
		_precisePosition = position.Floor() + _precisePosition - _precisePosition.Floor();
		_facing = (Facing)direction;
		_walking = false;

		Position = OracleObjectMath.ToPixelPosition(position);
		QueueRedraw();
	}

	internal void UpdateMinecartRideDirection(Vector2 input)
	{
		if (!_minecartRideControlled)
		{
			throw new InvalidOperationException(
				"Minecart ride direction requires active cart ownership.");
		}
		if (input.LengthSquared() > 0.01f)
			UpdateFacing(input);
	}

	internal void AdvanceMinecartJumpUpdateForValidation()
	{
		if (_minecartJumpControlled)
			AdvanceMinecartJumpUpdate();
	}

	private void AdvanceMinecartJumpUpdate()
	{
		TopDownAirParameters parameters =
			TopDownAirDatabase.For(AssetRoot).Parameters;
		if (_topDownJumpSoundPending)
		{
			_topDownJumpSoundPending = false;
			_world.PlaySound(parameters.JumpSound);
			InitializeNativeJumpAnimation();
		}

		bool landed = OracleObjectMath.UpdateSpeedZ(
			ref _topDownAirZFixed,
			ref _topDownAirSpeedZ,
			parameters.Gravity);
		if (landed)
		{
			ClearTopDownAirState();
			_world.PlaySound(parameters.LandSound);
		}
		else
		{
			// linkUpdateInAir resolves gravity/landing before the locked
			// planar step. The landing update does not take another hop step.
			// wLinkInAir bit7 clears adjacent walls, so this locked valid
			// angle moves unblocked, but still publishes the native velocity
			// before later ITEM/ENEMY/PART consumers and wraps both words.
			_precisePosition = _world.ApplyNativeUnblockedMovement(_precisePosition, 0x14, _minecartJumpAngle);
			if (_topDownAirSpeedZ > parameters.MaximumFallSpeed)
				_topDownAirSpeedZ = parameters.MaximumFallSpeed;
			AdvanceNativeLinkAnimation();
		}

		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}
}
