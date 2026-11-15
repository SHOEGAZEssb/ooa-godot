namespace Oracles;

public partial class Player
{
	internal void ClearNativeItemParents()
	{
		CancelNativeItemUsage();
	}

	internal void DisableInstruments(int frames) => _inventory.RuntimeState.SetField("wInstrumentsDisabledCounter", checked((byte)frames));

	private void InterruptCarriedItems(bool discard, bool preserveSeedShooter = false, bool preserveBoomerang = false)
	{
		if (_world is null)
			return;
		_world.InterruptBomb(this, discard);
		_world.InterruptBracelet(this, discard);
		if (!preserveSeedShooter) _world.InterruptSeedShooter();
		_world.InterruptSwitchHook(discard);
		_world.CancelSomaria();
		_world.CancelBiggoron();
		if (!preserveBoomerang) _world.ClearBoomerangParent();
		_world.ClearBombchuParent();
	}

	/// <summary>
	/// Selects LINK_ANIM_MODE_HARP_2 ($1e). The imported frames include the
	/// first frame entered after the fourth 52-update phrase, which remains
	/// visible until INTERAC_PLAY_HARP_SONG reaches state 6 on the next update.
	/// </summary>
	internal void BeginHarpPose()
	{
		_harpRenderer ??= new CutsceneSpriteRenderer(AssetRoot);
		_cutsceneHarpFrames ??=
			NewGameIntroDatabase.For(AssetRoot).SpriteFrames("link-harp");
		_harpFrames = _cutsceneHarpFrames;
		if (_harpFrames.Length != 13)
		{
			throw new InvalidOperationException(
				"Expected thirteen LINK_ANIM_MODE_HARP_2 presentation frames.");
		}

		_harpFrame = 0;
		_harpFrameTicks = _harpFrames[0].Duration;
		_harpPoseActive = true;
		_walking = false;

		QueueRedraw();
	}

	private void BeginPlayableHarpPose(bool flute = false)
	{
		_harpRenderer ??= new CutsceneSpriteRenderer(AssetRoot);
		_harpFrames = flute ? new FluteDatabase(AssetRoot).Frames :
			_itemHarpFrames ??= NewGameIntroDatabase.For(AssetRoot).SpriteFrames("link-harp-item");
		if (_harpFrames.Length != (flute ? 7 : 17))
		{
			throw new InvalidOperationException(
				$"Expected {(flute ? 7 : 17)} playable {(flute ? "flute" : "harp")} animation frames.");
		}

		_harpFrame = 0;
		_harpFrameTicks = _harpFrames[0].Duration;
		_harpPoseActive = true;
		_walking = false;

		QueueRedraw();
	}

	internal void AdvanceHarpPose()
	{
		if (!_harpPoseActive || _harpFrames is null)
			return;

		_harpFrameTicks--;
		if (_harpFrameTicks > 0)
			return;
		if (_harpFrame + 1 < _harpFrames.Length)
			_harpFrame++;
		_harpFrameTicks = _harpFrames[_harpFrame].Duration;
		QueueRedraw();
	}

	internal void EndHarpPose()
	{
		if (!_harpPoseActive)
			return;
		_harpPoseActive = false;
		_harpFrame = 0;
		_harpFrameTicks = 0;
		QueueRedraw();
	}

	private void AdvanceRocsCapeParent()
	{
		for (int slot = 0; slot < _capeParents.Length; slot++)
		{
			if (_capeParents[slot] is not { } parent) continue;
			if (parent) { _capeParents[slot] = false; continue; }
			if (!_capeJumpEligible || !(_sideScrollAirborne || _topDownAirborne) ||
				CapeGliding || !ParentItemButtons.IsHeld(_inventory.RuntimeState, slot + 3, Input.IsActionPressed("attack"), Input.IsActionPressed("item")))
			{
				_capeParents[slot] = null;
				ParentItemButtons.Set(_inventory.RuntimeState, slot + 3, 0);
				continue;
			}
			TopDownAirParameters parameters = TopDownAirDatabase.For(AssetRoot).Parameters;
			int speedZ = _world.SideScrolling ? _sideScrollSpeedZ : _topDownAirSpeedZ;
			// compareHlToBc returns $ff below BC; inc a / ret z rejects it.
			if (speedZ < parameters.CapeMinimumSpeedZ) continue;

			if (_world.SideScrolling)
			{
				_sideScrollSpeedZ = parameters.CapeSpeedZ;
				_sideScrollReducedGravity = true;
			}
			else
			{
				_topDownAirSpeedZ = parameters.CapeSpeedZ;
				_topDownReducedGravity = true;
			}
			_scriptedLinkAnimationMode = null;
			LinkNativeAnimationDatabase.For(AssetRoot).Set(_inventory.RuntimeState, 0x19);
			_airborneLinkAnimationMode = AirborneLinkAnimationMode.RocsCape;
			_capeParents[slot] = null;
			ParentItemButtons.Set(_inventory.RuntimeState, slot + 3, 0);
			_world.PlaySound(parameters.CapeSound);
		}
	}

	private void CancelNativeItemUsage()
	{
		ClearCapeParents();
		// linkCancelAllItemUsage drops held items and clears parent slots.
		// Physical ITEM children keep their own lifetime and update phase.
		_world.ClearItemParents(this);
		ClearShieldParent();
		CancelSwordAttack();
		CancelShovelAction();
		CancelPunchAction();
		CancelSeedSatchelAction();
		_startedParentItemAnimations = 0;
	}

	internal bool TryBlockWithShield(Rect2 targetBounds, int minimumLevel = 1)
	{
		if (!IsUsingShield || _inventory.ShieldLevel < minimumLevel)
			return false;

		if (!RoomEntityManager.ObjectCollisionXYOverlaps(targetBounds, ShieldCollisionBounds))
			return false;

		// Projectile modes $06/$07 select COLLISIONEFFECT_$1f for shield
		// collision types $01-$03. LINKDMG_$20 contributes only SND_CLINK2;
		// the projectile receives ENEMYDMG_$34 and enters its bounce state.
		_world.PlaySound(SoundId.SndClink2);
		return true;
	}

	internal void UpdateShieldForValidation(bool attackHeld, bool itemHeld)
	{
		if (!_itemAllocationBlockedThisUpdate && !IsUsingHarp)
		{
			if (attackHeld && _inventory.EquippedA == TreasureId.Shield) TryAllocateParentItem5(TreasureId.Shield, 1);
			if (itemHeld && _inventory.EquippedB == TreasureId.Shield) TryAllocateParentItem5(TreasureId.Shield, 2);
		}
		UpdateShieldState(attackHeld, itemHeld);
	}

	private void UpdateShieldState(bool attackHeld, bool itemHeld)
	{
		if (ShieldParentActive && !ParentItemButtons.IsHeld(_inventory.RuntimeState, 5, attackHeld, itemHeld))
		{
			ClearShieldParent();
		}

		bool shieldUsable = _activeTransformation == 0 &&
			// shieldParent.s checks swimming and underwater independently
			// of checkUseItems' A/B allocation gates.
			!TopDownSwimming && !SideScrollSwimming && !_world.Underwater &&
			!_world.ItemUsageDisabled && !_cutsceneControlled &&
			!_minecartRideControlled &&
			!_drowning && !_fallingInHole &&
			_ledgeJumpState == LedgeJumpState.None &&
			_inventory.ShieldLevel > 0;
		if (!shieldUsable)
		{
			ClearShieldParent();
			return;
		}

		bool next = ShieldParentActive &&
			!IsUsingItem &&
			!IsCarryingObject &&
			// checkNoOtherParentItemsInUse includes Bracelet state0 while
			// it seeks a wall, before it claims a pull/lift animation.
			!_world.BraceletParentActive &&
			!_world.BombParentActive;
		if (next && !ShieldParentInitialized)
		{
			ShieldParentInitialized = true;
			_world.PlaySound(_linkItems.Constants.ShieldSound);
		}
		byte shield = next ? checked((byte)_inventory.ShieldLevel) : (byte)0;
		if (_inventory.RuntimeState.ReadField("wUsingShield") == shield)
			return;
		_inventory.RuntimeState.SetField("wUsingShield", shield);
		QueueRedraw();
	}

	private void ClearShieldParent()
	{
		bool redraw = IsUsingShield;
		ClearParentItem5(TreasureId.Shield);
		_inventory.RuntimeState.SetField("wUsingShield", 0);
		if (redraw)
			QueueRedraw();
	}

	private void SuspendShield()
	{
		if (!IsUsingShield)
			return;
		_inventory.RuntimeState.SetField("wUsingShield", 0);
		QueueRedraw();
	}

	private void StartBiggoronAction(Vector2 input)
	{
		if(!_inventory.HasTreasure(TreasureId.BiggoronSword) || IsUsingHarp || IsCarryingObject) return;
		int current=IsUsingBiggoron?TreasureId.BiggoronSword:IsUsingPunch?TreasureId.Punch:
			IsUsingSwitchHook?TreasureId.SwitchHook:IsAttacking?TreasureId.Sword:
			IsUsingSeedShooter?TreasureId.Shooter:_world.BombParentActive?TreasureId.Bombs:
			IsUsingShovel?TreasureId.Shovel:_world.BraceletParentActive?TreasureId.Bracelet:IsUsingSomaria?TreasureId.CaneOfSomaria:0;
		Span<ParentItemSlotState> slots=stackalloc ParentItemSlotState[4]; slots.Clear();
		if(current!=0) slots[0]=new((byte)(current is TreasureId.BiggoronSword or TreasureId.Punch?0xff:
			_parentItemUsage.Item(current).Enabled),(byte)current);
		if(_parentItemUsage.ChooseSlot(TreasureId.BiggoronSword,slots)!=2) return;
		CancelSwordAttack(); CancelPunchAction();
		_usingShovel=false; _shovelFrame=0; _shovelFrameAccumulator=0; _shovelChildContactPending=false;
		if(_world.SwordDisabled || _world.SwordJinxed)
		{
			_world.CancelSomaria(); _world.PlaySound(SoundId.SndError); return;
		}
		bool underwater=_world.Underwater || _world.SideScrolling && HasMermaidSuit &&
			(_world.SideScrollActiveTiles.ActiveType&SideScrollTileType.Water)!=0;
		if(underwater) { _world.CancelSomaria(); return; }
		if(input.LengthSquared()>0.01f) UpdateFacing(input);
		_world.BeginBiggoron(this); _walking=false;
		QueueRedraw();
	}

	private void StartSomariaAction(Vector2 input, bool primaryInputSuppressed = false, bool braceletParentAtInput = false)
	{
		// A playing instrument sets wcc95 bit7 before checkUseItems, which
		// skips both input allocations while continuing existing parents.
		if (!_inventory.HasTreasure(TreasureId.CaneOfSomaria) || IsCarryingObject || IsUsingHarp || IsUsingBiggoron) return;
		int current=IsUsingSwitchHook?TreasureId.SwitchHook:
			IsAttacking?TreasureId.Sword:IsUsingSeedShooter?TreasureId.Shooter:
			_world.BombParentActive?TreasureId.Bombs:IsUsingShovel?TreasureId.Shovel:
			braceletParentAtInput?TreasureId.Bracelet:
			IsUsingPunch?TreasureId.Punch:IsUsingSomaria?TreasureId.CaneOfSomaria:0;
		Span<ParentItemSlotState> slots=stackalloc ParentItemSlotState[4];
		slots.Clear();
		if(current!=0) slots[0]=new((byte)_parentItemUsage.Item(current).Enabled,(byte)current);
		// checkUseItems allocates A and then B before either parent updates.
		// A's higher-priority reservation still rejects B-Cane when A's state0
		// subsequently clears itself (for example, an empty Bomb or Shooter).
		int primary = _inventory.EquippedA;
		if (!primaryInputSuppressed && _inventory.EquippedB == TreasureId.CaneOfSomaria &&
			primary is > 0 and < 0x20)
		{
			var usage = _parentItemUsage.Item(primary);
			bool pressed = usage.JustPressed ? Input.IsActionJustPressed("attack") : Input.IsActionPressed("attack");
			if (pressed && usage.Selector == 3 && usage.Enabled > slots[0].Enabled)
				slots[0] = new((byte)usage.Enabled, (byte)primary);
		}
		if(_parentItemUsage.ChooseSlot(TreasureId.CaneOfSomaria,slots)!=2) return;
		CancelPunchAction();
		if(input.LengthSquared()>0.01f) UpdateFacing(input);
		bool underwater=_world.Underwater || _world.SideScrolling &&
			HasMermaidSuit &&
			(_world.SideScrollActiveTiles.ActiveType&SideScrollTileType.Water)!=0;
		_world.BeginSomaria(this,underwater);
		_walking=false;
		QueueRedraw();
	}

	private void StartBoomerangAction(Vector2 input, bool primaryButton)
	{
		if (_inventory.BoomerangLevel == 0 || IsCarryingObject || IsHoldingItemOneHand ||
			IsHoldingItemTwoHands || IsUsingHarp) return;
		Span<ParentItemSlotState> slots = stackalloc ParentItemSlotState[4];
		CollectFreeItemParents(slots);
		int slot = _parentItemUsage.ChooseSlot(TreasureId.Boomerang, slots);
		if (slot < 0) return;
		// ParentItem3 dispatches before ParentItem4, including physical
		// allocation. An A-Bombchu reservation must create its child first.
		if (_world.BombchuParentAllocated && _world.BombchuParentSlot < slot)
			InitializeBombchuParentForUpdate();
		// boomerangParent state0 samples wLinkAngle for the child, but
		// parentItemLoadAnimationAndIncState disables Link's turning first.
		// Preserve his facing even when the launch angle differs from it.
		if (!_world.TryBeginBoomerang(this, slot, primaryButton)) return;
		_walking = false;

		QueueRedraw();
	}

	public void StartShovelAction()
	{
		_world.CancelSomaria();
		// shovelParent state0 uses checkLinkOnGround, including swimming,
		// underwater var2f bit 7 and wLinkObjectIndex's mounted bit.
		// chooseParentItemSlot's selector3 replaces an equal-priority shovel
		// parent on a fresh press. Its physical child has a separate lifetime.
		if (IsUsingItem && !IsUsingShovel || !IsOnGroundForBracelet)
			return;
		// shovelParent initializes its animation before Link's turning pass;
		// the parent disables turning and retains the preceding facing.
		_usingShovel = true;
		NotifyParentItemAnimationStarted(TreasureId.Shovel);
		_shovelFrame = 0;
		_shovelFrameAccumulator = 0.0;
		_walking = false;

		QueueRedraw();
	}

	private void CancelShovelAction()
	{
		if (IsUsingShovel) ParentItemAnimationState.Clear(_inventory.RuntimeState, 2, TreasureId.Shovel);
		bool changed = IsUsingShovel;
		_usingShovel = false;
		_shovelFrame = 0;
		_shovelFrameAccumulator = 0.0;
		if (changed)
			QueueRedraw();
		CancelSeedSatchelAction();
		CancelPunchAction();
		CancelHarpAction();
	}

	internal void StartHarpActionForValidation() => StartHarpAction();

	private void StartHarpAction(bool flute = false)
	{
		if (IsUsingHarp) return;
		int id = flute ? TreasureId.Flute : TreasureId.Harp;
		if (!InstrumentParentActive && !TryAllocateParentItem5(id, _inventory.EquippedA == id ? 1 : 2)) return;
		if (_inventory.RuntimeState.ReadField("wInstrumentsDisabledCounter") != 0 || _raftRideControlled || ShieldParentActive ||
			IsUsingItem || _world.BombchuParentAllocated || !IsOnGroundForBracelet ||
			_pullingIntoHole || _drowning || _fallingInHole)
		{
			ClearParentItem5(id);
			return;
		}
		int frames = flute ? _world.BeginFlute(this) : _world.BeginHarp(this);
		if (frames <= 0)
		{
			ClearParentItem5(id);
			return;
		}
		_inventory.RuntimeState.SetWramByte(0xd504, 1);
		NotifyParentItemAnimationStarted(id, 5);
		_harpActionUpdate = 0;
		_harpActionFrames = frames;
		_harpFrameAccumulator = 0.0;
		BeginPlayableHarpPose(flute);
	}

	private void AdvanceHarpAction()
	{
		_harpActionUpdate++;
		_world.AdvanceHarp(this, _harpActionUpdate);
		AdvanceHarpPose();
		if (_harpActionUpdate < _harpActionFrames)
			return;

		int completedSong = _inventory.RuntimeState.ReadWramByte(0xd501)==TreasureId.Flute ? 0xff : _inventory.SelectedHarpSong;
		if (completedSong==0xff) _world.CompleteHarp(this,completedSong);
		ClearParentItem5(TreasureId.Harp);
		ClearParentItem5(TreasureId.Flute);
		_harpActionUpdate = 0;
		_harpActionFrames = 0;
		_harpFrameAccumulator = 0.0;
		EndHarpPose();
		if (completedSong!=0xff) _world.CompleteHarp(this, completedSong);
	}

	private void CancelHarpAction()
	{
		bool changed = IsUsingHarp;
		ClearParentItem5(TreasureId.Harp);
		ClearParentItem5(TreasureId.Flute);
		_harpActionUpdate = 0;
		_harpActionFrames = 0;
		_harpFrameAccumulator = 0.0;
		if (changed)
		{
			_world.CancelHarp();
			EndHarpPose();
		}
	}

	internal void AdvanceHarpForValidation(int frames)
	{
		for (int frame = 0; frame < frames && IsUsingHarp; frame++)
			AdvanceHarpAction();
		QueueRedraw();
	}

	internal void StartPunchActionForValidation(Vector2 facingInput) =>
		StartPunchAction(facingInput);

	private void StartPunchAction(Vector2 facingInput)
	{
		if (IsUsingItem || !RingEffects.CanPunch(
			_inventory,
			_inventory.EquippedA == TreasureId.None &&
			_inventory.EquippedB == TreasureId.None))
		{
			return;
		}
		if (facingInput.LengthSquared() > 0.01f)
			UpdateFacing(facingInput);
		_usingPunch = true;
		NotifyParentItemAnimationStarted(0x02);
		_expertPunch = RingEffects.UsesExpertPunch(_inventory);
		_punchFrame = 0;
		_punchDamage = _expertPunch ? 4 : 1;
		_punchFrameAccumulator = 0.0;
		_walking = false;

		if (_expertPunch)
			_world.ApplyExpertsRingTileHit(this, (int)_facing * 2);
		_world.PlaySound(_expertPunch
			? SoundId.SndExplosion
			: SoundId.SndStrike);
		ApplyPunchCollision();
		QueueRedraw();
	}

	private void AdvancePunchFrame()
	{
		_punchFrame++;
		if (_punchFrame < PunchCollisionFrames)
			ApplyPunchCollision();
		int duration = _expertPunch ? ExpertPunchFrames : FistPunchFrames;
		// parentItemCode_punch@state1 checks the preceding parameter before
		// advancing it, retaining the newly published terminal pose one update.
		if (_punchFrame > duration)
			CancelPunchAction();
	}

	private void ApplyPunchCollision()
	{
		if (!IsUsingPunch || _punchFrame >= PunchCollisionFrames)
			return;
		_world.ApplySwordHit(
			this, GetSwordHitbox(Position, 24 + (int)_facing, _linkItems));
	}

	private void CancelPunchAction()
	{
		if (IsUsingPunch) ParentItemAnimationState.Clear(_inventory.RuntimeState, 2, 0x02);
		bool changed = IsUsingPunch;
		_usingPunch = false;
		_expertPunch = false;
		_punchFrame = 0;
		_punchDamage = 0;
		_punchFrameAccumulator = 0.0;
		if (changed)
			QueueRedraw();
	}

	internal void AdvancePunchForValidation(int frames)
	{
		for (int frame = 0; frame < frames && IsUsingPunch; frame++)
			AdvancePunchFrame();
		QueueRedraw();
	}

	internal void StartSeedSatchelAction()
	{
		// Satchel selector2 uses ParentItem3/4; Cane owns ParentItem2.
		// Retain the satchel's own single-parent cap during that overlap.
		if (IsUsingItem && !IsUsingSomaria && !IsUsingBiggoron && !IsUsingRodOfSeasons && !IsUsingBombchu && !IsUsingBoomerang || IsUsingSeedSatchel)
			return;

		Span<ParentItemSlotState> slots = stackalloc ParentItemSlotState[4];
		CollectFreeItemParents(slots);
		int slot = _parentItemUsage.ChooseSlot(TreasureId.SeedSatchel, slots);
		if (slot < 0) return;
		// Lower throw parents initialize before the later Satchel parent.
		if (_world.BombchuParentAllocated && _world.BombchuParentSlot < slot)
			InitializeBombchuParentForUpdate();
		// parentItemCode_satchel retains Link.direction. Its ordinary seed
		// branch locks turning before the later Link movement handler.
		int actionFrames = _world.TryUseSeedSatchel(this);
		if (actionFrames <= 0)
			return;
		_usingSeedSatchel = true;
		_seedSatchelParentSlot = slot;
		NotifyParentItemAnimationStarted(TreasureId.SeedSatchel, slot);
		_seedSatchelFrame = 0;
		_seedSatchelActionFrames = actionFrames;
		_seedSatchelFrameAccumulator = 0.0;
		_walking = false;

		QueueRedraw();
	}

	private void AdvanceSeedSatchelFrame()
	{
		_seedSatchelFrame++;
		// parentItemGenericState1 checks the retained animation terminator
		// before animating, so deletion follows the final animation update.
		if (_seedSatchelFrame > _seedSatchelActionFrames)
			CancelSeedSatchelAction();
	}

	private void CancelSeedSatchelAction()
	{
		if (IsUsingSeedSatchel) ParentItemAnimationState.Clear(_inventory.RuntimeState, _seedSatchelParentSlot, TreasureId.SeedSatchel);
		bool changed = IsUsingSeedSatchel;
		_usingSeedSatchel = false;
		_seedSatchelFrame = 0;
		_seedSatchelActionFrames = 0;
		_seedSatchelFrameAccumulator = 0.0;
		if (changed)
			QueueRedraw();
	}

	internal void AdvanceSeedSatchelForValidation(int frames)
	{
		for (int frame = 0; frame < frames && IsUsingSeedSatchel; frame++)
			AdvanceSeedSatchelFrame();
		QueueRedraw();
	}

	private void AdvanceShovelFrame()
	{
		_shovelFrame++;
		if (_shovelFrame == _linkItems.Constants.ShovelDigFrame)
		{
			// shovelParent creates its child before movement; itemCode15
			// performs the tile contact in the following updateItems pass.
			if (_linkPhysicsActive)
				_shovelChildContactPending = true;
			else
				_world.DigWithShovel(_precisePosition + ShovelChildOffset, FacingVector);
		}
		if (_shovelFrame >= _linkItems.Constants.ShovelActionFrames)
		{
			CancelShovelAction();
			return;
		}
		QueueRedraw();
	}
}
