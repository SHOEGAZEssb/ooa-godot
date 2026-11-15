namespace Oracles;

public partial class Player
{
	public Rect2 GetSwordHitbox()
	{
		if (!IsAttacking || _swordState == SwordActionState.Poke && !_swordPokeCollisionEnabled)
			return new Rect2(Position, Vector2.Zero);
		return GetSwordHitbox(Position, GetSwordArcIndex(), _linkItems);
	}

	internal static Rect2 GetSwordHitboxForValidation(Vector2 position, int arcIndex, string assetRoot) =>
		GetSwordHitbox(position, arcIndex, LinkItemDatabase.For(assetRoot));

	private static Rect2 GetSwordHitbox(Vector2 position, int arcIndex, LinkItemDatabase items)
	{
		SwordArc arc = items.SwordArc(arcIndex);
		Vector2 center = position + new Vector2(arc.OffsetX, arc.OffsetY);
		return new Rect2(
			center - new Vector2(arc.RadiusX, arc.RadiusY),
			new Vector2(arc.RadiusX * 2, arc.RadiusY * 2));
	}

	public void StartSwordAttack() => StartSwordAttack(null, Vector2.Zero);

	internal void StartSwordAttackForValidation(Vector2 facingInput) =>
		StartSwordAttack(null, facingInput);

	private void StartSwordAttack(string? buttonAction, Vector2 facingInput)
	{
		if (!AdmitItemOverRod(TreasureId.Sword)) return;
		// ITEM_SWORD can allocate w1ParentItem2 while the Harp occupies
		// w1ParentItem5, but clearParentItemIfCantUseSword observes the
		// Harp's wcc95 bit 7 and deletes the sword before state 0 can run.
		// Preserve that instrument-specific exclusion before state 0 can
		// consume RNG, play a slash sound, or create the weapon child.
		if (IsUsingShovel || IsUsingSeedSatchel || IsUsingHarp || IsUsingBiggoron)
			return;
		if (IsAttacking && !SwordCanRestart)
			return;
		if (_world.SwordJinxed)
		{
			_world.CancelSomaria();
			_world.PlaySound(SoundId.SndError);
			return;
		}
		_world.CancelSomaria();
		if (facingInput.LengthSquared() > 0.01f)
			UpdateFacing(facingInput);
		_swordState = SwordActionState.Swing;
		_inventory.RuntimeState.SetField("wcc63", 0);
		NotifyParentItemAnimationStarted(TreasureId.Sword);
		// updateSpecialObjects seeds var2f bit 7 from the tileset;
		// linkState01_sidescroll also sets it for Mermaid Suit water.
		// parentItemLoadAnimationAndIncState latches mode $22/$2d here.
		_swordUnderwaterAnimation = !_raftRideControlled &&
			(_world.Underwater || _world.SideScrolling &&
				HasMermaidSuit &&
				(_world.SideScrollActiveTiles.ActiveType &
					SideScrollTileType.Water) != 0);
		_swordMinecartAnimation = _minecartRideControlled && !_swordUnderwaterAnimation;
		_swordStateFrame = 0;
		_swordChargeCounter = _linkItems.Constants.SwordChargeCounter;
		_swordFrameAccumulator = 0.0;
		ParentItemButtons.SetAction(_inventory.RuntimeState, 2, buttonAction);
		_swordPokeReturnsToHeld = false;
		_walking = false;
		_swordPokeCollisionEnabled = false;
		// swordParent state0 creates the child; itemCode05 state0 consumes
		// RNG and plays its cue in updateItems, after Link's Pegasus animation.
		if (_linkPhysicsActive)
			_swordChildInitializationPending = true;
		else
			InitializeSwordChild();
		QueueRedraw();
	}

	private void InitializeSwordChild()
	{
		int sound = _linkItems.SwordSlashSound(
			_random.Next().Value & 0x07);
		_world.PlaySound(sound);
		byte whimsicalRoll = 0xff;
		if (_inventory.IsRingActive(RingId.Whimsical))
		{
			whimsicalRoll = _random.Next().Value;
			if (whimsicalRoll == 0)
				_world.PlaySound(SoundId.SndLightning);
		}
		_currentSwordDamage = RingEffects.SwordDamage(
			_inventory, _inventory.SwordLevel, whimsicalRoll);
		_doubleEdgedDamagePending =
			_inventory.IsRingActive(RingId.DoubleEdged) &&
			_inventory.HealthQuarters >= 5;
		QueueRedraw();
	}

	private void CancelSwordAttack()
	{
		ParentItemAnimationState.Clear(_inventory.RuntimeState, 2, TreasureId.Sword);
		_swordChildInitializationPending = false;
		_pendingSwordEnemyContact = false;
		_forcedSwordParentInitializing = false;
		_inventory.RuntimeState.SetField("wcc63", 0);
		bool changed = IsAttacking;
		_swordState = SwordActionState.None;
		_swordUnderwaterAnimation = false;
		_swordMinecartAnimation = false;
		_swordStateFrame = 0;
		_swordChargeCounter = 0;
		_swordFrameAccumulator = 0.0;
		_swordPokeReturnsToHeld = false;
		_currentSwordDamage = 0;
		_doubleEdgedDamagePending = false;
		_swordPokeCollisionEnabled = false;
		if (changed)
			QueueRedraw();
	}

	private bool IsSwordButtonHeld() =>
		ParentItemButtons.IsHeld(_inventory.RuntimeState, 2, Input.IsActionPressed("attack"), Input.IsActionPressed("item"));

	private void AdvanceSwordParentBeforeMovement(Vector2 input)
	{
		if (_swordUpdatedInPhysics || _world.PlayerUpdatesFrozen || _world.LinkDisabled || _world.NativePaletteChanging) return;
		AdvanceSwordJinxForItemUse();
		_swordUpdatedInPhysics = true;
		if (_world.SwordDisabled || _world.SwordJinxed)
			CancelSwordAttack();
		else if (IsAttacking && !Started(TreasureId.Sword, IsAttacking))
		{
			// checkUseItems advances existing parents before Link reads
			// wLinkImmobilized. Bit 7 of wLinkInAir skips allocation only.
			// Current child collision is published after movement; the parent
			// consumes the preceding post-object contact signal instead.
			_swordParentBeforeMovement = true;
			try { AdvanceSwordFrame(IsSwordButtonHeld(), input); }
			finally { _swordParentBeforeMovement = false; }
		}
	}

	private void AdvanceSwordJinxForItemUse()
	{
		if (_swordJinxUpdatedInPhysics || _world.IsTransitioning || _world.DialogueOpen ||
			_world.PlayerUpdatesFrozen || _world.LinkDisabled || _world.NativePaletteChanging) return;
		_swordJinxUpdatedInPhysics = true;
		_world.AdvanceSwordJinxCounter();
	}

	internal void AdvanceSwordForValidation(
		int frames,
		bool buttonHeld,
		Vector2 movementInput = default)
	{
		_lastMovementInput = movementInput;
		for (int frame = 0; frame < frames && IsAttacking; frame++)
			AdvanceSwordFrame(buttonHeld, movementInput);
		QueueRedraw();
	}

	private void AdvanceSwordFrame(bool buttonHeld, Vector2 movementInput)
	{
		// swordParent state 2 checks the button before wall/enemy signals.
		// State 3 deliberately keeps the opposite order before spin release.
		if (_swordState == SwordActionState.Held &&
			(!buttonHeld || _minecartRideControlled || _raftRideControlled))
		{
			CancelSwordAttack();
			return;
		}
		bool previousContact = _pendingSwordEnemyContact;
		_pendingSwordEnemyContact = false;
		if (previousContact)
		{
			// ITEM_SWORD.var2a survives the child updates. A swung sword
			// consumes its enemy contact in swordParent state 6 at the end
			// of the swing, including the one-shot Double-Edged penalty.
			if (_swordState == SwordActionState.Swing)
				_pendingSwordEnemyContact = true;
			else
				ApplySwordContactRingDamage();
			if (_swordState == SwordActionState.Poke && _swordPokeReturnsToHeld &&
				_swordStateFrame >= _linkItems.Constants.SwordPokeFrames)
			{
				CancelSwordAttack();
				return;
			}
			if (_swordState is SwordActionState.Held or SwordActionState.Charged)
			{
				TriggerSwordPoke(returnsToHeld: false);
				return;
			}
		}
		switch (_swordState)
		{
			case SwordActionState.Swing:
				_swordStateFrame++;
				if (_swordStateFrame ==
					_linkItems.Constants.SwordTileHitFrame)
				{
					if (!_swordMinecartAnimation)
						_world.ApplySwordTileHit(this, (int)_facing * 2, swordPoke: false);
					TryCreateSwordBeamFromSwing();
				}
				if (_swordStateFrame >=
					(_swordMinecartAnimation
						? _linkItems.Constants.MinecartSwordSwingFrames
						: _linkItems.Constants.SwordSwingFrames))
				{
					// swordParent state 6 deletes the sword when Link's main
					// object is the minecart or raft. The swing is usable,
					// but cannot be held or charged during the ride.
					if (_pendingSwordEnemyContact || !buttonHeld || _minecartRideControlled || _raftRideControlled)
					{
						if (_pendingSwordEnemyContact) ApplySwordContactRingDamage();
						CancelSwordAttack();
						return;
					}
					EnterSwordHeldState();
				}
				ApplySwordCollision();
				break;

			case SwordActionState.Held:
				if (_raftRideControlled)
				{
					CancelSwordAttack();
					return;
				}
				if (ApplySwordCollision())
				{
					TriggerSwordPoke(returnsToHeld: false);
					return;
				}
				if (CheckSwordPoke(movementInput))
					return;
				if (!buttonHeld)
				{
					CancelSwordAttack();
					return;
				}
				_swordChargeCounter -= RingEffects.SwordChargeStep(_inventory);
				if (_swordChargeCounter < 0)
				{
					if (RingEffects.EnergyBeamOnCharge(_inventory))
					{
						_world.TryCreateSwordBeam(this, (int)_facing);
						TriggerEnergySwordPoke();
						break;
					}
					_swordState = SwordActionState.Charged;
					_swordStateFrame = 0;
					_world.PlaySound(SoundId.SndChargeSword);
				}
				break;

			case SwordActionState.Charged:
				if (ApplySwordCollision())
				{
					TriggerSwordPoke(returnsToHeld: false);
					return;
				}
				if (CheckSwordPoke(movementInput))
					return;
				if (!buttonHeld)
					BeginSwordSpin();
				else
					_swordStateFrame++;
				break;

			case SwordActionState.Poke:
				_swordStateFrame++;
				if (_swordStateFrame <
					// A wall poke's terminal animation update selects state
					// $06; the following parent update returns to held/deletes.
					_linkItems.Constants.SwordPokeFrames + (_swordPokeReturnsToHeld ? 1 : 0))
				{
					if (_swordPokeReturnsToHeld && _swordStateFrame == _linkItems.Constants.SwordPokeFrames)
					{
						// Parent state $05 selects child state $06; the live
						// child pass reinitializes normal level collision and
						// base damage before parent state $06 runs next time.
						_swordPokeCollisionEnabled = true;
						_currentSwordDamage = RingEffects.SwordDamage(
							_inventory, _inventory.SwordLevel, 0xff, initialSwing: false);
					}
					if (_swordPokeCollisionEnabled) ApplySwordCollision();
					break;
				}
				if (_swordPokeReturnsToHeld && buttonHeld && !_raftRideControlled)
				{
					EnterSwordHeldState();
					ApplySwordCollision();
				}
				else
					CancelSwordAttack();
				break;

			case SwordActionState.Spin:
				int previousPhase = GetSpinArcPhase();
				_swordStateFrame++;
				if (_swordStateFrame >= RingEffects.SwordSpinFrames(
					_inventory, _linkItems.Constants.SwordSpinFrames,
					_linkItems.Constants.SpinPhaseStarts[^1]))
				{
					_world.ApplySwordTileHit(this, 8, swordPoke: false);
					CancelSwordAttack();
					return;
				}
				int phase = GetSpinArcPhase();
				if (phase != previousPhase)
					_world.ApplySwordTileHit(
						this, ((int)_facing * 2 + phase) & 7, swordPoke: false);
				ApplySwordCollision();
				break;
		}
		QueueRedraw();
	}

	private void EnterSwordHeldState()
	{
		_inventory.RuntimeState.SetWramByte(0xd23f, 0);
		_inventory.RuntimeState.SetField("wcc63", 1);
		_swordState = SwordActionState.Held;
		_swordStateFrame = 0;
		_swordChargeCounter = _linkItems.Constants.SwordChargeCounter;
		_swordPokeReturnsToHeld = false;
		// swordParent state 6 clears its Double-Edged modifier; child state 2
		// restores the level's base damage from var31, including Whimsical.
		_doubleEdgedDamagePending = false;
		_currentSwordDamage = RingEffects.SwordDamage(
			_inventory, _inventory.SwordLevel, 0xff, initialSwing: false);
	}

	private bool CheckSwordPoke(Vector2 movementInput)
	{
		// checkUseItems runs before this update's adjacent-wall publication.
		if (!RoomCollision.IsPushingAgainstWall(NativeAdjacentWalls, FacingVector, movementInput))
			return false;

		TriggerSwordPoke(returnsToHeld: true);
		_world.ApplySwordTileHit(this, (int)_facing * 2, swordPoke: true);
		return true;
	}

	private void TriggerEnergySwordPoke()
	{
		// ENERGY_RING branches directly to @triggerSwordPoke after attempting
		// to allocate ITEM_SWORD_BEAM. It does so even when the one-beam
		// object cap prevents allocation, and does not play the charge sound.
		TriggerSwordPoke(returnsToHeld: false);
		// ENERGY_RING jumps below the normal poke's collisionType clear.
		// The child stays in state 2 with ITEMCOLLISION_SWORD_HELD ($09).
		_swordPokeCollisionEnabled = true;
		ApplySwordCollision();
	}

	private void TriggerSwordPoke(bool returnsToHeld)
	{
		_inventory.RuntimeState.SetWramByte(0xd23f, 8);
		_swordState = SwordActionState.Poke;
		_swordStateFrame = 0;
		_swordPokeReturnsToHeld = returnsToHeld;
		_swordPokeCollisionEnabled = false;
		_walking = false;
	}

	private void TryCreateSwordBeamFromSwing()
	{
		if (_inventory.SwordLevel < 2)
			return;
		int missingHealth = _inventory.MaxHealthQuarters -
			_inventory.HealthQuarters;
		if (missingHealth <=
			RingEffects.SwordBeamMaximumMissingQuarters(_inventory))
		{
			_world.TryCreateSwordBeam(this, (int)_facing);
		}
	}

	private void BeginSwordSpin()
	{
		_inventory.RuntimeState.SetWramByte(0xd23f, 0x0f);
		_inventory.RuntimeState.SetField("wcc63", 0x02);
		_swordState = SwordActionState.Spin;
		_swordStateFrame = 0;
		// sword.s state 4 doubles var3a before the post pass applies rings.
		_currentSwordDamage = RingEffects.SwordDamage(
			_inventory, _inventory.SwordLevel, 0xff, initialSwing: false, baseMultiplier: 2);
		_walking = false;
		_world.PlaySound(SoundId.SndSwordSpin);
		_world.ApplySwordTileHit(this, (int)_facing * 2, swordPoke: false);
		ApplySwordCollision();
	}

	private bool ApplySwordCollision()
	{
		if (_swordParentBeforeMovement) return false;
		Rect2 hitbox = GetSwordHitbox();
		if (hitbox.Size == Vector2.Zero ||
			!_world.ApplySwordHit(this, hitbox))
		{
			return false;
		}
		ApplySwordContactRingDamage();
		return true;
	}

	internal void QueueSwordEnemyContact() => _pendingSwordEnemyContact = true;

	private void ApplySwordContactRingDamage()
	{
		if (!_doubleEdgedDamagePending) return;
		// swordParent.s applies $f8 (four quarter-hearts) once after the first
		// accepted enemy contact, and clears var3a so later overlap frames do
		// not hurt Link again. The health >= $05 check occurs at swing start.
		ApplyUnmodifiedDamage(4);
		_doubleEdgedDamagePending = false;
	}

	private int GetSwordPosePhase()
	{
		return _swordState switch
		{
			SwordActionState.Swing =>
				_linkItems.SwingPhase(_swordStateFrame),
			SwordActionState.Poke =>
				_swordStateFrame <
					_linkItems.Constants.SwordTileHitFrame
					? 2
					: 3,
			_ => 3
		};
	}

	private Facing GetSwordPoseFacing() => _swordState == SwordActionState.Spin
		? (Facing)((((int)_facing * 2 + GetSpinArcPhase()) & 7) >> 1)
		: _facing;

	private int GetSwordArcIndex()
	{
		if (_swordState == SwordActionState.Spin)
			return 16 + (((int)_facing * 2 + GetSpinArcPhase()) & 7);
		if (_swordState == SwordActionState.Swing && _swordMinecartAnimation)
			return (int)_facing + _linkItems.Constants.MinecartSwingArcPhases[
				_linkItems.SwingPhase(_swordStateFrame)] * 4;
		return (int)_facing + GetSwordPosePhase() * 4;
	}

	private int GetSpinArcPhase()
	{
		int phase = _linkItems.SpinPhase(_swordStateFrame);
		return phase == 8 ? 0 : phase;
	}
}
