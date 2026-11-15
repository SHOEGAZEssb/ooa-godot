namespace Oracles;

public partial class Player
{
	private bool Started(int id, bool active) => active && (_startedParentItemAnimations & (1u << id)) != 0;
	private bool Signals(int id,bool active) => active && _linkItems.ParentAnimationSignalsItemUse(id);
	internal void NotifyParentItemAnimationStarted(int id, int slot = 2)
	{
		ParentItemAnimationState.Begin(_inventory.RuntimeState, slot, id, _linkItems.ParentAnimationPriority(id));
		if (_linkItems.ParentAnimationSignalsItemUse(id))
			_startedParentItemAnimations |= 1u << id;
	}

	private void AdvanceItems(double delta, bool swordInitialized = false)
	{
		if (GaleActive) return;
		if (_enemyInvincibilityFrames != 0.0f)
		{
			float frameDelta = (float)delta * 60.0f;
			_enemyInvincibilityFrames = _enemyInvincibilityFrames > 0.0f
				? Mathf.Max(
					0.0f, _enemyInvincibilityFrames - frameDelta)
				: Mathf.Min(
					0.0f, _enemyInvincibilityFrames + frameDelta);
			QueueRedraw();
		}

		if (ElectricShockActive) return;
		// updateLinkInvincibilityCounter is outside Link's $81 state gate;
		// item parents below it remain frozen without being cancelled.
		if (_world.PlayerUpdatesFrozen || _world.LinkDisabled) return;

		if (_enemyGrabUpdated || _collapseUpdated || EnemyGrabActive || WallmasterGrabActive || CollapsedActive)
		{
			return;
		}

		if (IsDying)
			return;

		// updateItems skips initialized items while wScrollMode is $08. Room
		// warps cancel the sword synchronously in BeginRoomWarpTransition.
		if (_world.IsTransitioning)
			return;

		if (_getItemStatePhase >= 2 || _forcedRespawnPhase == 2 || _squishAnimation is not null ||
			RespawnWaiting || RespawnRecovering)
		{
			// Link's forced state owns the parent pass. Physical item children
			// still run later, after a native held-item drop.
			return;
		}

		if (_nativeForcedMovementSelected || _forcedState08Phase != 0)
		{
			// Forced walk/state08 skip checkUseItems. Existing ITEM children
			// still advance; a consumed walk request retains its parents until
			// state0b initialization on the following Link update.
			return;
		}

		TransferSwordCollisionKnockback();
		if (_swordChildInitializationPending)
		{
			_swordChildInitializationPending = false;
			InitializeSwordChild();
		}
		if (_shovelChildContactPending)
		{
			_shovelChildContactPending = false;
			_world.DigWithShovel(_precisePosition + ShovelChildOffset, FacingVector);
		}

		bool itemParentsFrozen = _world.DialogueOpen || _world.NativePaletteChanging || _world.RoomExitPending || _ledgeUpdatedInPhysics || _ringUpdatedInPhysics;
		if (!itemParentsFrozen && _world.SwordDisabled)
		{
			// clearParentItemIfCantUseSword belongs to sword parents.
			// wccd8 blocks new item allocation; an existing shovel continues.
			CancelSwordAttack();
		}

		if (_drowning || _fallingInHole || RespawnRecovering)
		{
			CancelSwordAttack();
			CancelShovelAction();
			return;
		}

		// linkState01 returns before checkUseItems during text. Initialized
		// sword parents retain their state; the unconditional child post pass
		// still publishes the existing geometry for collision resolution.
		if (IsAttacking && (swordInitialized || itemParentsFrozen || _swordUpdatedInPhysics)) ApplySwordCollision();
		if (IsAttacking && !_swordUpdatedInPhysics && !swordInitialized && !itemParentsFrozen)
		{
			_swordFrameAccumulator += delta * 60.0;
			while (_swordFrameAccumulator + 0.000001 >= 1.0 && IsAttacking)
			{
				_swordFrameAccumulator -= 1.0;
				AdvanceSwordFrame(IsSwordButtonHeld(), _lastMovementInput);
			}
			QueueRedraw();
		}
		if (!itemParentsFrozen && !_lowerParentsUpdatedInPhysics)
			AdvanceShovelAndSatchelParents(delta);
		if (IsUsingPunch && !itemParentsFrozen && !Started(0x02, IsUsingPunch))
		{
			_punchFrameAccumulator += delta * 60.0;
			while (_punchFrameAccumulator + 0.000001 >= 1.0 && IsUsingPunch)
			{
				_punchFrameAccumulator -= 1.0;
				AdvancePunchFrame();
			}
			QueueRedraw();
		}
	}

	private void AdvanceShovelAndSatchelParents(double delta)
	{
		if (IsUsingShovel && !Started(TreasureId.Shovel, IsUsingShovel))
		{
			_shovelFrameAccumulator += delta * 60.0;
			while (_shovelFrameAccumulator + 0.000001 >= 1.0 && IsUsingShovel)
			{
				_shovelFrameAccumulator -= 1.0;
				AdvanceShovelFrame();
			}
			QueueRedraw();
		}
		if (IsUsingSeedSatchel && !Started(TreasureId.SeedSatchel, IsUsingSeedSatchel))
		{
			_seedSatchelFrameAccumulator += delta * 60.0;
			while (_seedSatchelFrameAccumulator + 0.000001 >= 1.0 &&
				IsUsingSeedSatchel)
			{
				_seedSatchelFrameAccumulator -= 1.0;
				AdvanceSeedSatchelFrame();
			}
			QueueRedraw();
		}
	}

	private bool ProcessItemInput(ref Vector2 input, bool checkTileWarps = true)
	{
		AdvanceSwordJinxForItemUse();
		bool immobilized = ProcessItemParents(input, checkTileWarps);
		if (!Started(TreasureId.Boomerang, IsUsingBoomerang) && !_world.ItemUsageDisabled &&
			!_companionRideControlled && _world.UpdateBoomerangParent(this))
			input = _lastMovementInput = Vector2.Zero;
		if(!Started(TreasureId.BiggoronSword,IsUsingBiggoron))
			_world.UpdateBiggoronParent(_world.SwordDisabled || _world.SwordJinxed || _world.Underwater ||
				_world.SideScrolling && HasMermaidSuit &&
				(_world.SideScrollActiveTiles.ActiveType&SideScrollTileType.Water)!=0);
		if (!Started(TreasureId.RodOfSeasons, IsUsingRodOfSeasons))
			_world.UpdateRodOfSeasonsParent();
		// ParentItem5 follows lower parents within checkUseItems, before
		// Link movement/standing animation can publish Pegasus dust cues.
		AdvanceSwordParentBeforeMovement(input);
		AdvanceShovelAndSatchelParents(_linkPhysicsDelta);
		_lowerParentsUpdatedInPhysics = true;
		if (InstrumentParentActive && !IsUsingHarp)
			StartHarpAction(_inventory.RuntimeState.ReadWramByte(0xd501) == TreasureId.Flute);
		// ParentItem5 publishes before Link can enter movement or standing animation.
		if (IsUsingHarp)
		{
			_harpFrameAccumulator += _linkPhysicsDelta * 60.0;
			while (_harpFrameAccumulator + 0.000001 >= 1.0 && IsUsingHarp)
			{
				_harpFrameAccumulator -= 1.0;
				AdvanceHarpAction();
			}
			QueueRedraw();
		}
		UpdateShieldState(Input.IsActionPressed("attack"), Input.IsActionPressed("item"));
		// linkUpdateMovement still calls animateLinkStanding when a
		// grounded parent disables displacement. This can publish the
		// Pegasus pulse even while Bomb/Bracelet owns the action pose.
		if (immobilized && _world.PlayingInstrument == 0 && !_topDownAirborne && !_world.SideScrolling &&
			!_world.RidingObject && !_world.DialogueOpen)
			ResetLinkWalkAnimation();
		return immobilized;
	}

	private bool ProcessItemParents(Vector2 input, bool checkTileWarps)
	{
		_bombchuParentInitializedThisUpdate = false;
		// checkUseItems samples grab state before allocating A, then B, before any parent updates.
		_itemAllocationBlockedThisUpdate |= NativeMovementSuppressed || _carriedObjectPose ||
			_braceletActionPose is BraceletActionPose.Pull or BraceletActionPose.PullStrain or BraceletActionPose.LiftHigh;
		bool primarySuppressed = _world.SideScrolling && !_world.Underwater &&
			(_world.SideScrollActiveTiles.ActiveType & SideScrollTileType.Water) != 0 &&
			!HasMermaidSuit;
		AdmitParentItem5(primarySuppressed);
		bool eligible = !_itemAllocationBlockedThisUpdate && !_world.ItemUsageDisabled && _activeTransformation == 0 &&
			!IsCarryingObject && !IsHoldingItemOneHand && !IsHoldingItemTwoHands && !IsUsingHarp;
		if (eligible && !primarySuppressed && Input.IsActionJustPressed("attack") && _inventory.EquippedA == TreasureId.Bombchus)
			ReserveBombchuParent();
		bool immobilized = ProcessOtherItemParents(input, checkTileWarps);
		if (eligible && !SecondaryItemAllocationSuppressed &&
			Input.IsActionJustPressed("item") && _inventory.EquippedB == TreasureId.Bombchus)
			ReserveBombchuParent();
		InitializeBombchuParentForUpdate();
		if (!_bombchuParentInitializedThisUpdate) _world.UpdateBombchuParent();
		return immobilized;
	}

	private void InitializeBombchuParentForUpdate() =>
		_bombchuParentInitializedThisUpdate |= _world.InitializeBombchuParent(this);

	private void CollectFreeItemParents(Span<ParentItemSlotState> slots)
	{
		slots.Clear();
		if (IsUsingRodOfSeasons) slots[0] = new(_inventory.RuntimeState.ReadWramByte(0xd200), TreasureId.RodOfSeasons);
		if (IsUsingSeedSatchel) slots[_seedSatchelParentSlot - 2] = new(1, TreasureId.SeedSatchel);
		if (IsUsingBoomerang) slots[_world.BoomerangParentSlot - 2] = new(1, TreasureId.Boomerang);
		if (_world.BombchuParentAllocated) slots[_world.BombchuParentSlot - 2] = new(1, TreasureId.Bombchus);
		for (int index = 0; index < _capeParents.Length; index++)
			if (_capeParents[index].HasValue) slots[index + 1] = new(1,TreasureId.Feather);
	}

	private void ReserveBombchuParent()
	{
		Span<ParentItemSlotState> slots = stackalloc ParentItemSlotState[4];
		CollectFreeItemParents(slots);
		int slot = _parentItemUsage.ChooseSlot(TreasureId.Bombchus, slots);
		if (slot >= 0) _world.TryBeginBombchu(this, slot);
	}

	private bool ProcessOtherItemParents(Vector2 input, bool checkTileWarps)
	{
		bool primaryItemInputSuppressed =
			_world.SideScrolling &&
			!_world.Underwater &&
			(_world.SideScrollActiveTiles.ActiveType &
				SideScrollTileType.Water) != 0 &&
			!HasMermaidSuit;
		bool primaryPressed =
			Input.IsActionPressed("attack") &&
			!primaryItemInputSuppressed;
		bool secondaryPressed = Input.IsActionPressed("item");
		bool itemButtonJustPressed =
			(Input.IsActionJustPressed("attack") &&
				!primaryItemInputSuppressed) ||
			Input.IsActionJustPressed("item");
		// Satchel selector 2 can allocate beside an existing ParentItem2 or
		// Boomerang. Its $22 state0 clears itself without changing Link's
		// animation or movement locks, so it remains usable during those
		// actions. Sample allocation eligibility before their parent updates.
		if (!_itemAllocationBlockedThisUpdate && !_world.ItemUsageDisabled &&
			_activeTransformation == 0 && !_raftRideControlled && !IsUsingHarp &&
			!IsUsingSeedSatchel && _inventory.SatchelSelectedSeeds == 2 &&
			(!primaryItemInputSuppressed && Input.IsActionJustPressed("attack") &&
				_inventory.EquippedA == TreasureId.SeedSatchel ||
			 !SecondaryItemAllocationSuppressed && Input.IsActionJustPressed("item") && _inventory.EquippedB == TreasureId.SeedSatchel))
			_world.TryUseSeedSatchel(this);
		// chooseParentItemSlot precedes the Bracelet parent's release check.
		// Keep that input-phase observation even if UpdateBracelet clears it.
		bool braceletParentAtInput = _world.BraceletParentActive;
		if(!_itemAllocationBlockedThisUpdate && !_world.ItemUsageDisabled && _activeTransformation==0 &&
			(!primaryItemInputSuppressed && Input.IsActionJustPressed("attack") && _inventory.EquippedA==TreasureId.BiggoronSword ||
			 !SecondaryItemAllocationSuppressed && Input.IsActionJustPressed("item") && _inventory.EquippedB==TreasureId.BiggoronSword))
			StartBiggoronAction(input);
		if (_world.UpdateBomb(this, input, itemButtonJustPressed))
		{
			if (_raftRideControlled && input.LengthSquared() > 0.01f)
				UpdateFacing(input);
			_walking = false;

			Position = OracleObjectMath.ToPixelPosition(_precisePosition);
			// Item-owned movement does not suppress func_60e9. Releasing
			// a held item clears wLinkGrabState before its post-object check.
			if (checkTileWarps && !_topDownAirborne) _world.CheckTileWarp(this);
			QueueRedraw();
			return true;
		}
		if (!_raftRideControlled && _world.UpdateBracelet(
				this,
				input,
				primaryPressed,
				secondaryPressed,
				itemButtonJustPressed))
		{
			_walking = false;

			Position = OracleObjectMath.ToPixelPosition(_precisePosition);
			if (checkTileWarps && !_topDownAirborne) _world.CheckTileWarp(this);
			QueueRedraw();
			return true;
		}
		if (IsUsingSwitchHook)
		{
			AdvanceSwitchHookAirState();
			_walking = false;

			if (checkTileWarps && !_topDownAirborne) _world.CheckTileWarp(this);
			QueueRedraw();
			return true;
		}
		if (_world.SeedShooterActive && _world.UpdateSeedShooter(
				this, input, primaryPressed, secondaryPressed,
				DirectionalInputJustPressed()))
		{
			if (_raftRideControlled && input.LengthSquared() > 0.01f)
				UpdateFacing(input);
			_walking = false;

			Position = OracleObjectMath.ToPixelPosition(_precisePosition);
			// Shooter immobilization only owns movement; func_60e9 still
			// checks the final standing tile after the object passes.
			if (checkTileWarps && !_topDownAirborne) _world.CheckTileWarp(this);
			QueueRedraw();
			// linkState01 updates the aiming parent before linkUpdateInAir.
			// Immobilization blocks ground movement, while a Feather jump
			// still integrates gravity and its airborne planar velocity.
			return !_topDownAirborne;
		}

		// checkUseItems allocates A, then B, before updating ParentItem2.
		// A same/higher-priority B reservation replaces A even if B's own
		// state0 will fail later. Do not run A's handler or spend its ammo.
		if (_itemAllocationBlockedThisUpdate) return false;
		ParentItemUsage primaryUsage = _inventory.EquippedA is > 0 and < 0x20
			? _parentItemUsage.Item(_inventory.EquippedA) : default;
		ParentItemUsage secondaryUsage = _inventory.EquippedB is > 0 and < 0x20
			? _parentItemUsage.Item(_inventory.EquippedB) : default;
		bool secondaryReplacesPrimary = !primaryItemInputSuppressed &&
			!SecondaryItemAllocationSuppressed &&
			Input.IsActionJustPressed("attack") && Input.IsActionJustPressed("item") &&
			primaryUsage.Selector == 3 && secondaryUsage.Selector == 3 &&
			secondaryUsage.Enabled >= primaryUsage.Enabled;
		if (_activeTransformation == 0 &&
			!secondaryReplacesPrimary &&
			Input.IsActionJustPressed("attack") && !_world.SwordDisabled && AdmitItemOverRod(_inventory.EquippedA))
		{
			if (!IsUsingItem || IsUsingSomaria)
			{
				if (!primaryItemInputSuppressed &&
					!_world.ItemUsageDisabled &&
					!_minecartRideControlled &&
					_inventory.EquippedA == TreasureId.Bombs &&
					_world.TryUseBomb(this))
					return true;
				if (!primaryItemInputSuppressed &&
					!_world.ItemUsageDisabled &&
					!_minecartRideControlled && !_raftRideControlled &&
					_inventory.EquippedA == TreasureId.Bracelet &&
					_world.TryUseBracelet(this, primaryButton: true))
					return true;
				if (!primaryItemInputSuppressed &&
					!_world.ItemUsageDisabled &&
					!_minecartRideControlled && !_raftRideControlled &&
					RingEffects.CanPunch(
					_inventory,
					_inventory.EquippedA == TreasureId.None &&
					_inventory.EquippedB == TreasureId.None))
				{
					StartPunchAction(input);
					return true;
				}
			}
			if (primaryItemInputSuppressed)
			{
				// linkUpdateFlippersSpeed reads BTN_A directly. While Link is
				// swimming with Flippers, checkUseItems checks only B,
				// including when A holds ITEM_SWORD. In side-view swimming,
				// var2f bit 7 enables both buttons for the Mermaid Suit.
			}
			else if (_world.ItemUsageDisabled)
			{
				// wInShop routes A/B to checkShopInput instead of updating
				// either equipped parent item. Interaction remains available.
			}
			else if (_inventory.EquippedA == TreasureId.Sword)
				StartSwordAttack("attack", input);
			else if (_inventory.EquippedA == TreasureId.RodOfSeasons)
				StartRodOfSeasonsAction(input, primaryButton: true);
			else if (_inventory.EquippedA == TreasureId.CaneOfSomaria)
				StartSomariaAction(input, braceletParentAtInput: braceletParentAtInput);
			else if (_inventory.EquippedA == TreasureId.Boomerang)
				StartBoomerangAction(input, primaryButton: true);
			else if ((!IsUsingItem || IsUsingSomaria) && _inventory.EquippedA == TreasureId.SwitchHook &&
				_world.TryBeginSwitchHook(this, input))
			{
				if (checkTileWarps) _world.CheckTileWarp(this);
				return true;
			}
			else if (!_minecartRideControlled && !_raftRideControlled &&
				_inventory.EquippedA == TreasureId.Shovel)
				StartShovelAction();
			else if (!_minecartRideControlled && !_raftRideControlled &&
				_inventory.EquippedA == TreasureId.Feather)
				TryStartFeatherJump("attack");
			else if (!_raftRideControlled &&
				_inventory.EquippedA == TreasureId.SeedSatchel)
					StartSeedSatchelAction();
			else if (_inventory.EquippedA == TreasureId.Shooter &&
				_world.TryBeginSeedShooter(this, primaryButton: true, input))
			{
				if (checkTileWarps && !_topDownAirborne) _world.CheckTileWarp(this);
				return !_topDownAirborne;
			}
		}
		if (_activeTransformation == 0 && !SecondaryItemAllocationSuppressed &&
			Input.IsActionJustPressed("item") && !_world.SwordDisabled && AdmitItemOverRod(_inventory.EquippedB))
		{
			if (!_minecartRideControlled && !_raftRideControlled &&
				!IsUsingItem &&
				_world.TrySecondaryInteract(this))
			{
				return true;
			}
			if (_world.ItemUsageDisabled)
			{
				// The secondary button can still lift or return shop stock.
			}
			else if ((!IsUsingItem || IsUsingSomaria) &&
				!_minecartRideControlled &&
				_inventory.EquippedB == TreasureId.Bombs)
			{
				if (_world.TryUseBomb(this))
					return true;
			}
			else if (!IsUsingItem &&
				!_minecartRideControlled && !_raftRideControlled &&
				RingEffects.CanPunch(
				_inventory,
				_inventory.EquippedA == TreasureId.None &&
				_inventory.EquippedB == TreasureId.None))
			{
				StartPunchAction(input);
			}
			else if ((!IsUsingItem || IsUsingSomaria) &&
				!_minecartRideControlled && !_raftRideControlled &&
				_inventory.EquippedB == TreasureId.Bracelet)
			{
				if (_world.TryUseBracelet(this, primaryButton: false))
					return true;
			}
			else if ((!IsUsingItem || IsUsingSomaria) && _inventory.EquippedB == TreasureId.SwitchHook &&
				_world.TryBeginSwitchHook(this, input))
			{
				if (checkTileWarps) _world.CheckTileWarp(this);
				return true;
			}
			else if (_inventory.EquippedB == TreasureId.Sword)
			{
				StartSwordAttack("item", input);
			}
			else if (_inventory.EquippedB == TreasureId.RodOfSeasons)
				StartRodOfSeasonsAction(input, primaryButton: false);
			else if (_inventory.EquippedB == TreasureId.CaneOfSomaria)
				StartSomariaAction(input, primaryItemInputSuppressed, braceletParentAtInput);
			else if (_inventory.EquippedB == TreasureId.Boomerang)
				StartBoomerangAction(input, primaryButton: false);
			else if (!_minecartRideControlled && !_raftRideControlled &&
				_inventory.EquippedB == TreasureId.Shovel)
			{
				StartShovelAction();
			}
			else if (!_minecartRideControlled && !_raftRideControlled &&
				_inventory.EquippedB == TreasureId.Feather)
			{
				TryStartFeatherJump("item");
			}
			else if (!_raftRideControlled &&
				_inventory.EquippedB == TreasureId.SeedSatchel)
			{
				StartSeedSatchelAction();
			}
			else if (_inventory.EquippedB == TreasureId.Shooter &&
				_world.TryBeginSeedShooter(this, primaryButton: false, input))
			{
				if (checkTileWarps && !_topDownAirborne) _world.CheckTileWarp(this);
				return !_topDownAirborne;
			}
		}

		return false;
	}
}
