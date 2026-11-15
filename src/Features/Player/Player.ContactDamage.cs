namespace Oracles;

public partial class Player
{
    private int _pendingContactDamageRaw;
    private bool _nativeContactSignal;

    // w1Link.var2a is a one-object-pass publication, independently of the
    // damageToApply byte which remains pending until a consuming handler runs.
    internal bool NativeContactSignal => _nativeContactSignal;
    internal int PendingContactDamageRaw => _pendingContactDamageRaw;

    internal void TransferNativeCompanionContact()
    {
        // raft.s:@transferKnockbackToLink copies the pending collision before
        // either raft or Link decrements recoil. Only var2a/2b are cleared.
        OracleRuntimeState memory = _inventory.RuntimeState;
        if (_enemyInvincibilityFrames == 0 && !_nativeContactSignal)
        {
            if (memory.ReadWramByte(0xd12a) == 0) return;
            _nativeContactSignal = true;
            _enemyInvincibilityFrames = unchecked((sbyte)memory.ReadWramByte(0xd12b));
            _enemyKnockbackAngle = memory.ReadWramByte(0xd12c);
            _enemyKnockbackFrames = memory.ReadWramByte(0xd12d);
            _pendingContactDamageRaw = memory.ReadWramByte(0xd125);
        }
        memory.SetWramByte(0xd12a, 0);
        memory.SetWramByte(0xd12b, 0);
    }

    private void ClearContactDamage()
    {
        // clearLinkObject/full room loading and resetLinkInvincibility clear
        // both bytes. Coordinate-only scrolling does not reset either byte.
        _pendingContactDamageRaw = 0;
        _nativeContactSignal = false;
    }

    private bool ConsumeContactDamageBeforeStateDispatch()
    {
        if (WallmasterGrabActive) return false; // linkState0c never calls updateLinkDamageTaken.
        if (SubrosiaWarpActive && !SubrosiaWarpPending) return false; // state0a has no normal-state palette/damage gate.
        // commonCode.updateLinkDamageTaken is called by state01, transformed
        // state1, animal-rider state1 and grabbed state0d substate1. The latter
        // has no text/palette/scroll gate. Other represented states retain the
        // pending byte; their explicit damage calls remain immediate.
        if (EnemyGrabActive)
        {
            if (_enemyGrabSubstate != 1) return false;
        }
        else
        {
            if (_deathPending || _deathAnimationActive || _getItemStatePhase >= 2 ||
                _forcedRespawnPhase >= 2 || _forcedState08Phase >= 2 ||
                _nativeForcedMovementSelected ||
                _ledgeJumpState != LedgeJumpState.None || _squishAnimation is not null ||
                RespawnWaiting || RespawnRecovering ||
                _fallingInHole || _drowning && _topDownDrownPhase >= 2 ||
                _newGameSlowFalling || _roomWarpFallActive || _roomWarpFallCollapsed ||
                _cutsceneControlled || GaleActive && _galePending < 2)
                return false;
            // Normal Link writes this before its palette/scroll return. Other
            // special-object IDs call damage without that state01 publication.
            if (_activeTransformation == 0 && !_companionRideControlled && !_world.LinkDisabled)
                SetNativePushAnimationOverride(0x80);
            if (_world.NativePaletteChanging || _world.ScreenScrolling && !_companionRideControlled)
                return true; // Native handler returns before damage, input and parents.
        }

        if (_pendingContactDamageRaw != 0)
        {
            int raw = RingEffects.ModifyIncomingDamageRaw(_inventory, _pendingContactDamageRaw);
            _pendingContactDamageRaw = 0; // linkApplyDamage clears before arithmetic/death/potion.
            ApplyRawDamage(raw);
        }
        // linkApplyDamage uses the live var2a signal, even for harmless
        // shield recoil. A palette/scroll update may have already cleared it.
        if (_nativeContactSignal)
            _enemyKnockbackFrames = RingEffects.KnockbackFrames(_inventory, (int)_enemyKnockbackFrames);
        return false;
    }

	public bool ApplyDamage(int quarters)
	{
		return ApplyDamage(quarters, RingDamageSource.Generic);
	}

	internal bool ApplyDamage(int quarters, RingDamageSource source)
	{
		int raw = RingEffects.IncomingDamageRaw(_inventory, quarters, source);
		return ApplyRawDamage(raw);
	}

	private bool ApplyRawDamage(int raw)
	{
		if (raw == 0 || _inventory.HealthQuarters == 0) return false;
		_damageAccumulator = (_damageAccumulator + raw) & 0xff;
		int damage = 0;
		if ((_damageAccumulator & 0x80) != 0)
		{
			damage = (0x100 - _damageAccumulator + 1) / 2;
			_damageAccumulator = (_damageAccumulator + damage * 2) & 0xff;
		}
		bool hadPotion = _inventory.HasTreasure(TreasureId.Potion);
		ApplyUnmodifiedDamage(damage);
		if (_inventory.HealthQuarters == 0) _damageAccumulator = 0;
		else if (hadPotion && !_inventory.HasTreasure(TreasureId.Potion)) _damageAccumulator = 1;
		// A half-quarter hit can leave displayed health unchanged and still
		// trigger sound, invincibility and knockback in the collision caller.
		return true;
	}

	private bool ApplyUnmodifiedDamage(int quarters)
	{
		bool applied = quarters > 0 && _inventory.ApplyDamage(quarters);
		if (applied && _inventory.HealthQuarters == 0)
			_deathPending = true;
		return applied;
	}

	public bool ApplyEnemyContactDamage(Vector2 sourcePosition, int quarters) =>
		ApplyEnemyContactDamage(
			sourcePosition, quarters, RingDamageSource.Generic);

	internal void ApplyInteractionInvincibility(int frames)
	{
		_enemyInvincibilityFrames = -frames;
		_enemyKnockbackFrames = 0;
	}

	internal bool ApplyEnemyContactDamage(
		Vector2 sourcePosition,
		int quarters,
		RingDamageSource source,
		int invincibilityFrames = EnemyInvincibilityFrames,
		int knockbackFrames = EnemyKnockbackFrames,
		bool allowZeroDamage = false)
	{
		// The native collision caller has already checked target XY/Z.
		// Ordinary Feather air does not disable Link's collision flag.
		if (_braceletLiftCollisionsDisabled || !PatchCollisionsEnabled ||
			IsDying || _enemyInvincibilityFrames != 0.0f ||
			_enemyKnockbackFrames > 0.0f || quarters < 0 || quarters == 0 && !allowZeroDamage)
			return false;
		if (source == RingDamageSource.OwnBomb)
		{
			// bombs.s explicitly calls linkApplyDamage during the item pass.
			if (quarters != 0 && !ApplyDamage(quarters, source)) return false;
		}
		else
		{
			int raw = RingEffects.ContactDamageRaw(_inventory, quarters, source);
			if (quarters != 0 && raw == 0) return false;
			_pendingContactDamageRaw = raw;
			_nativeContactSignal = true;
		}

		// LINKDMG_00/_04 select SND_DAMAGE_LINK ($5f) when the collision is
		// accepted. Rejected contacts during Link's invincibility do not
		// enqueue another request.
		if (source != RingDamageSource.OwnBomb)
			_world.PlaySound(SoundId.SndDamageLink);
		_enemyInvincibilityFrames = invincibilityFrames;
		_enemyKnockbackFrames = source == RingDamageSource.OwnBomb
			? RingEffects.KnockbackFrames(_inventory, knockbackFrames) : knockbackFrames;
		// collisionEffects.s retains all five angle bits. Converting through
		// an input vector quantizes oblique native recoil to eight directions.
		_enemyKnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(sourcePosition, EnemyContactPosition);
		_walking = false;

		if (source == RingDamageSource.OwnBomb)
			InterruptCarriedItems(discard: false);
		QueueRedraw();
		return true;
	}

	/// <summary>
	/// COLLISIONEFFECT_SHIELD_BUMP writes LINKDMG_$10/$14 directly to Link:
	/// no health damage, a negative non-flashing invincibility counter, and
	/// recoil away from the enemy. linkApplyDamage still applies Steadfast.
	/// </summary>
	internal void ApplyShieldCollisionRecoil(
		Vector2 sourcePosition,
		int invincibilityFrames,
		int knockbackFrames)
	{
		if (!CanAcceptShieldCollision)
		{
			throw new InvalidOperationException(
				"Link accepted shield recoil while collision-disabled.");
		}
		// The shield branch of enemyCheckCollisions supplies wShieldY/X to
		// @handleCollision. Recoil is directed toward that contact point,
		// whose one-pixel sideways offset can change the quantized angle.
		ApplyCollisionRecoil(sourcePosition, invincibilityFrames, knockbackFrames,ShieldCollisionBounds.GetCenter());
	}

	internal bool TryApplyHarmlessContactRecoil(Vector2 sourcePosition)
	{
		if (_braceletLiftCollisionsDisabled || !PatchCollisionsEnabled || IsDying ||
			_enemyInvincibilityFrames != 0 || _enemyKnockbackFrames != 0) return false;
		ApplyCollisionRecoil(sourcePosition, 0x0f, 0x13);
		return true;
	}

	private void ApplyCollisionRecoil(Vector2 sourcePosition, int invincibilityFrames, int knockbackFrames,Vector2? collisionTarget=null)
	{
		if (invincibilityFrames < 0)
			throw new ArgumentOutOfRangeException(nameof(invincibilityFrames));
		if (knockbackFrames <= 0)
			throw new ArgumentOutOfRangeException(nameof(knockbackFrames));

		_enemyInvincibilityFrames = -invincibilityFrames;
		_enemyKnockbackFrames = knockbackFrames;
		_nativeContactSignal = true;
		int angle = OracleObjectMovement.Shared.RelativeAngle(
			sourcePosition,
			collisionTarget??Position);
		_enemyKnockbackAngle = angle;
		_walking = false;

		QueueRedraw();
	}

	/// <summary>
	/// Maple writes Link's knockback counter/angle directly without damage,
	/// invincibility, or Steadfast Ring adjustment.
	/// </summary>
	internal bool ApplyMapleKnockback(Vector2 sourcePosition)
	{
		if (_braceletLiftCollisionsDisabled || IsDying)
			return false;

		int angle = sourcePosition == Position
			? ((OracleObjectMovement.Shared.RelativeAngle(
				Vector2.Zero, -(Vector2)FacingVector)) & ObjectAngle.CardinalMask)
			: (OracleObjectMovement.Shared.RelativeAngle(
				sourcePosition, Position) & ObjectAngle.CardinalMask);
		_enemyKnockbackFrames = 0x18;
		_enemyKnockbackAngle = angle;
		_walking = false;

		InterruptCarriedItems(discard: false);
		ClearShieldParent();
		CancelSwordAttack();
		CancelShovelAction();
		QueueRedraw();
		return true;
	}

	/// <summary>
	/// COLLISIONEFFECT_$15-$17 writes Link recoil to ITEM_SWORD. The sword's
	/// next item update transfers the counter and angle to Link, after Link's
	/// physics update for that application frame has already completed.
	/// </summary>
	internal void QueueSwordCollisionKnockback(
		Vector2 sourcePosition,
		int frames)
	{
		if (frames <= 0)
			return;

		_pendingSwordKnockbackFrames = frames;
		int angle = OracleObjectMovement.Shared.RelativeAngle(
			sourcePosition,
			Position);
		_pendingSwordKnockbackAngle = angle;
	}

	internal void TransferSwordCollisionKnockback()
	{
		if (_pendingSwordKnockbackFrames == 0)
			return;

		int frames = _pendingSwordKnockbackFrames;
		int angle = _pendingSwordKnockbackAngle;
		_pendingSwordKnockbackFrames = 0;
		_pendingSwordKnockbackAngle = 0;

		// itemTransferKnockbackToLink preserves a longer live counter but
		// always replaces Link's knockback angle.
		if (frames >= _enemyKnockbackFrames)
			_enemyKnockbackFrames = frames;
		_enemyKnockbackAngle = angle;
	}

	private void AdvanceKnockbackAfterItemParents(
		double delta, bool recoilItemImmobilized, Vector2 recoilMovementStart)
	{
		float frameDelta = (float)delta * 60.0f;
		// The raft already decremented Link's counter and moved itself at
		// SPEED_100. Link still runs his own SPEED_140 recoil here; func_410d
		// replaces his position only after items/interactions have read it.
		// Preserve that second decrement and the phase-local scratch writes.
		if (_world.SideScrolling)
		{
			int decrement = _sideScrollAirborne ? 2 : 1;
			float nextCounter = _enemyKnockbackFrames - decrement;
			if (nextCounter >= 0.0f)
			{
				_enemyKnockbackFrames = nextCounter;
				ApplySideScrollVelocity(
					_world.SideScrollParameters.KnockbackSpeed,
					SideScrollHorizontalAngle(
						_enemyKnockbackAngle),
					allowWallSlide: false);
			}
			else
			{
				_enemyKnockbackFrames = 0.0f;
			}

			Vector2 sideInput = Input.GetVector(
				"move_left", "move_right", "move_up", "move_down");
			if (_world.MovementDisabled)
				sideInput = Vector2.Zero;
			_lastMovementInput = sideInput;
			UpdateSideScrollMovement(
				delta,
				sideInput,
				// linkState01_sidescroll applies recoil first, then
				// state 2 still swims unless wLinkImmobilized is set.
				movementAllowed: _sideScrollAirborne || SideScrollSwimming ||
					_enemyKnockbackFrames == 0 && !recoilItemImmobilized &&
					!IsUsingBoomerang && (!IsUsingItem || SwordAllowsMovement));
		}
		else
		{
			// linkUpdateKnockback calls the native collision helper,
			// including tile-edge adjustment and its scratch writes.
			AdvanceTopDownKnockback(delta);
			if (_topDownAirborne)
			{
				// linkState01 always runs linkUpdateInAir after recoil.
				// Retained jump momentum adds its own planar step, and
				// landing resumes ground input only after recoil clears.
				Position = OracleObjectMath.ToPixelPosition(_precisePosition);
				UpdateTopDownAirMovement(delta, _lastMovementInput,
					!recoilItemImmobilized && !IsUsingBoomerang && (!IsUsingItem || SwordAllowsMovement),
					recoilMovementStart);
				return;
			}
		}
		// linkUpdateKnockback still moves at counter zero. The normal
		// handler then resumes grounded movement in this same update;
		// checkUseItems has already run and must not run a second time.
		if (!_world.SideScrolling && !_topDownAirborne && !TopDownSwimming &&
			_enemyKnockbackFrames == 0 &&
			!recoilItemImmobilized)
		{
			AdvanceMovementAfterItemParents(delta, _lastMovementInput, recoilMovementStart);
			return;
		}
		_walking = false;
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		if (_world.SideScrolling && !_world.CheckTileWarp(this))
			_world.CheckRoomExit(this);
		else if (!_world.SideScrolling && !_topDownAirborne)
			// linkUpdateKnockback returns from Link's handler, not from
			// cutscene01: func_60e9 still runs after the object passes.
			_world.CheckTileWarp(this);
		QueueRedraw();
	}

	private void AdvanceTopDownKnockback(double delta)
	{
		float frameDelta = (float)delta * 60.0f;
		float nextCounter = _enemyKnockbackFrames - (_topDownAirborne ? 2 : 1) * frameDelta;
		// A borrow clears recoil without another step; zero still moves.
		if (nextCounter >= 0)
			_precisePosition += _world.ResolveNativeMovement(_precisePosition,0x32,
				_enemyKnockbackAngle,allowWallSlide:true,adjacentWalls:NativeAdjacentWalls) * frameDelta;
		_enemyKnockbackFrames = Mathf.Max(0,nextCounter);
	}

	internal void ResetEnemyInvincibility()
	{
		_enemyInvincibilityFrames = 0.0f;
	}

	internal void ResetLinkInvincibility()
	{
		// resetLinkInvincibility clears the whole collision/damage/recoil
		// block. The portal's IPlayerRestriction owns collision eligibility.
		ClearInteractionKnockback(clearInvincibility: true);
		ClearContactDamage();
		_enemyKnockbackAngle = 0;
		_pendingSwordKnockbackFrames = 0;
		_pendingSwordKnockbackAngle = 0;
	}
}
