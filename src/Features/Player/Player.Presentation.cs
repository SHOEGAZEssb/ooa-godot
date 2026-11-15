namespace Oracles;

public partial class Player
{
	internal ulong SeedShooterLinkTextureHash(int angle, int variant = 0) =>
		OracleGraphicsCache.PixelHash(
			Sprites.SeedShooterLinkTextures[
				CheckedSeedShooterVariant(variant),
				CheckedSeedShooterAngle(angle)].GetImage());
	internal ulong SeedShooterWeaponTextureHash(int angle) =>
		OracleGraphicsCache.PixelHash(
			Sprites.SeedShooterWeaponTextures[CheckedSeedShooterAngle(angle)].GetImage());
	internal int SeedShooterLinkOpaquePixels(int angle, int variant = 0) =>
		CountOpaquePixels(Sprites.SeedShooterLinkTextures[
			CheckedSeedShooterVariant(variant),
			CheckedSeedShooterAngle(angle)].GetImage());
	internal int SeedShooterWeaponOpaquePixels(int angle)
		=> CountOpaquePixels(Sprites.SeedShooterWeaponTextures[
			CheckedSeedShooterAngle(angle)].GetImage());

	private static int CountOpaquePixels(Image image)
	{
		int count = 0;
		for (int y = 0; y < image.GetHeight(); y++)
		for (int x = 0; x < image.GetWidth(); x++)
		{
			if (image.GetPixel(x, y).A > 0.1f)
				count++;
		}
		return count;
	}
	internal Image SeedShooterPoseImage(int angle, int variant = 0) =>
		Sprites.SeedShooterPoseTextures[
			CheckedSeedShooterVariant(variant),
			CheckedSeedShooterAngle(angle)].GetImage();
	internal Vector2 SeedShooterPoseOffset(int angle, int variant = 0) =>
		Sprites.SeedShooterPoseOffsets[
			CheckedSeedShooterVariant(variant),
			CheckedSeedShooterAngle(angle)];
	internal ulong BraceletActionPixelHash(int pose, int direction)
	{
		if (pose is < 0 or >= 3)
			throw new ArgumentOutOfRangeException(nameof(pose));
		if (direction is < 0 or >= 4)
			throw new ArgumentOutOfRangeException(nameof(direction));
		return OracleGraphicsCache.PixelHash(
			Sprites.BraceletActionTextures[pose, direction].GetImage());
	}

	internal System.Collections.Generic.IEnumerable<bool> PrepareInitialPresentation(string assetRoot)
	{
		if (Sprites is null)
		{
			AssetRoot = assetRoot;
			Sprites = new PlayerSpriteLibrary(assetRoot);
		}
		else if (AssetRoot != assetRoot)
			throw new InvalidOperationException("Prepared Link resources cannot change game packs.");
		if (_initialPresentationPrepared) yield break;
		Sprites.PrepareWalkTexture();
		yield return false;
		TerrainShadowDefinition terrainShadow = TerrainShadow.Load(AssetRoot);
		_terrainShadowTexture = terrainShadow.Texture;
		_terrainShadowOffset = terrainShadow.Offset;
		yield return false;
		_ = _terrainEffects;
		yield return false;
		_initialPresentationPrepared = true;
		yield return false;
	}

	internal void PrepareTransformationPresentation() => _ = _transformedLink;

	internal void SetAlternateTextboxPalettePriority(bool active)
	{
		// drawAllSpritesUnconditionally queues Link again after every other
		// object for TEXTBOXFLAG_ALTPALETTE1. The higher world Z reproduces
		// that one textbox-mode priority without changing ordinary relative
		// Y priority.
		ZIndex = active
			? AlternateTextboxPaletteZIndex
			: _forcedState08LowPriority
				? ObjectDrawPriority.BehindLinkZIndex
			: TopDownDiving
				? DivingZIndex
				: NormalZIndex;
	}

	public override void _Draw()
	{
		if (_world is null) return;
		_electricShockMaterial?.SetShaderParameter("damage_palette", DamagePaletteActive);
		if (LedgeShadowDrawn ||
			_topDownAirborne && (_world.FrameCounter & 1) != 0)
			DrawTexture(_terrainShadowTexture, _terrainShadowOffset);

		foreach (PlayerGroundDrawPass pass in GroundDrawOrder)
		{
			switch (pass)
			{
				case PlayerGroundDrawPass.Body:
					float bodyZ = _activeTransformation != 0 ? unchecked((sbyte)ObjectZHigh) : _cutsceneDrawZFixed / 256.0f;
					DrawSetTransform(new Vector2(0, bodyZ + _world.RaisedFloorOffset));
					DrawBody();
					DrawSetTransform(Vector2.Zero);
					break;
				case PlayerGroundDrawPass.TerrainEffect:
					DrawTerrainEffect();
					break;
				default:
					throw new InvalidOperationException(
						$"Unsupported Link ground draw pass {pass}.");
			}
		}
	}

	private void DrawBody()
	{
		if (CollapsedActive)
		{
			if(_collapseParameter==0)DrawTexture(ScriptedLinkAnimationTexture(2,false),NormalSpriteOrigin+Vector2.Down*EnemyContactZ);
			else DrawWalkLinkBody(_linkWalkAnimationFrame,Vector2.Down*EnemyContactZ);
			return;
		}
		if (_deathAnimationActive)
		{
			DrawTextureRectRegion(
				Sprites.DeathTexture,
				new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
				new Rect2(_deathAnimationFrame * 16, 0, 16, 16));
		}
		else if (_roomWarpFallActive && _roomWarpFallCollapsed)
		{
			DrawTextureRectRegion(
				Sprites.DeathTexture,
				new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
				new Rect2(4 * 16, 0, 16, 16));
		}
		else if (_roomWarpFallActive &&
			_newGameFallRenderer is not null &&
			_newGameFallFrames is not null)
		{
			_newGameFallRenderer.DrawRelativeFrame(
				this,
				_newGameFallFrames[_roomWarpFallFrame],
				_roomWarpFallZFixed >> 8);
		}
		else if (_newGameSlowFalling &&
			_newGameFallRenderer is not null && _newGameFallFrames is not null)
		{
			_newGameFallRenderer.DrawRelativeFrame(
				this,
				_newGameFallFrames[_newGameFallFrame],
				_newGameFallZFixed >> 8);
		}
		else if (_cutsceneSpriteFrame is ObjectSpriteFrame cutsceneFrame &&
			_cutsceneSpriteRenderer is not null)
		{
			_cutsceneSpriteRenderer.DrawRelativeFrame(
				this, cutsceneFrame, z: 0);
		}
		else if (_cutsceneIdleFacing is Facing idleFacing)
		{
			DrawTextureRectRegion(Sprites.WalkTexture,
				new Rect2(NormalSpriteOrigin, new Vector2(16, 16)), GetFrame(idleFacing, 0));
		}
		else if (_harpPoseActive && !IsUsingHarp &&
			_harpRenderer is not null && _harpFrames is not null)
		{
			_harpRenderer.DrawRelativeFrame(
				this,
				_harpFrames[_harpFrame],
				z: 0);
		}
		else if (SubrosianDanceActive)
		{
			var frame = NativeLinkBodyFrame;
			DrawTexture(frame.Texture, frame.Offset);
		}
		else if (_scriptedLinkAnimationMode.HasValue && _publishedParentPage == 0xd0)
		{
			DrawScriptedLinkAnimation(_scriptedLinkAnimationMode.Value);
		}
		else if (_companionRideControlled &&
			_companionRideTexture is not null &&
			_damageCompanionRideTexture is not null)
		{
			// SPECIALOBJECT_LINK_RIDING_ANIMAL selects graphics solely from
			// the companion's animParameter. No ordinary Link item or
			// airborne pose can supersede this frame while mounted.
			DrawTexture(
				DamagePaletteActive
					? _damageCompanionRideTexture
					: _companionRideTexture,
				CompanionRideDrawOffset);
		}
		else if (_publishedParentPage != 0xd0)
		{
			if (IsAttacking) DrawSword();
			DrawPublishedParentBody();
		}
		else if (_drowning && !_drownRespawning)
		{
			int frame = GetDrownAnimationFrame();
			DrawTextureRectRegion(
				DamagePaletteActive ? Sprites.DamageDrownTexture : Sprites.DrownTexture,
				new Rect2(DrownSpriteOrigin, new Vector2(16, 16)),
				new Rect2(frame * 16, (int)_facing * 16, 16, 16));
		}
		else if (_fallingInHole && !_fallInHoleInitializePending && !_fallInHoleRespawning)
		{
			int frame = GetFallInHoleFrame();
			DrawTextureRectRegion(
				DamagePaletteActive ? Sprites.DamageFallInHoleTexture : Sprites.FallInHoleTexture,
				new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
				new Rect2(frame * 16, 0, 16, 16));
		}
		else if (TopDownSwimming)
		{
			DrawTextureRectRegion(
				TopDownDiving
					? DamagePaletteActive
						? Sprites.DamageTopDownDiveTexture
						: Sprites.TopDownDiveTexture
					: DamagePaletteActive
						? Sprites.DamageTopDownSwimTexture
						: Sprites.TopDownSwimTexture,
				new Rect2(DrownSpriteOrigin, new Vector2(16, 16)),
				new Rect2(
					_topDownSwimAnimationFrame * 16,
					TopDownDiving ? 0 : (int)_facing * 16,
					16,
					16));
		}
		else if (UsesSideScrollSwimmingSwordPose)
		{
			DrawSwordPose();
		}
		else if (SideScrollSwimming)
		{
			DrawSideScrollSwimmingBody(Vector2.Zero);
		}
		else if (_squishAnimation is { State: > 0 } squish)
		{
			DrawTexture(squish.Current.Texture,new Vector2(-16,-16));
		}
		else if (UsesAirborneSwordPose)
		{
			DrawSwordPose();
		}
		else if (_sideScrollAirborne)
		{
			DrawAirborneLinkBody(Vector2.Zero);
		}
		else if (_topDownAirborne && !IsUsingSwitchHook)
		{
			DrawAirborneLinkBody(new Vector2(0, _topDownAirZFixed >> 8));
		}
		else if (_ledgeJumpState != LedgeJumpState.None)
		{
			DrawTextureRectRegion(
				DamagePaletteActive
					? Sprites.DamageLedgeJumpTexture
					: Sprites.LedgeJumpTexture,
				new Rect2(
					NormalSpriteOrigin + new Vector2(0, _ledgeZFixed >> 8),
					new Vector2(16, 16)),
				new Rect2(
					_ledgeAnimationPhase * 16,
					(int)_facing * 16,
					16,
					16));
		}
		else if (_getItemTwoHandPose)
		{
			DrawTexture(
				DamagePaletteActive
					? Sprites.DamageGetItemTwoHandTexture
					: Sprites.GetItemTwoHandTexture,
				NormalSpriteOrigin);
		}
		else if (_getItemOneHandPose)
		{
			DrawTexture(
				DamagePaletteActive
					? Sprites.DamageGetItemOneHandTexture
					: Sprites.GetItemOneHandTexture,
				NormalSpriteOrigin);
		}
		else if (_braceletActionPose is BraceletActionPose braceletPose)
		{
			if (braceletPose == BraceletActionPose.LiftHigh)
				DrawTextureRectRegion(DamagePaletteActive ? Sprites.DamageCarriedObjectTexture : Sprites.CarriedObjectTexture,
					new Rect2(NormalSpriteOrigin, new Vector2(16, 16)), new Rect2(0, (int)_facing * 16, 16, 16));
			else DrawTexture(
				(DamagePaletteActive
					? Sprites.DamageBraceletActionTextures
					: Sprites.BraceletActionTextures)[(int)braceletPose, (int)_facing],
				new Vector2(-16, -16));
		}
		else if (_carriedObjectPose)
		{
			int frame = GetWalkAnimationFrame();
			DrawTextureRectRegion(
				DamagePaletteActive
					? Sprites.DamageCarriedObjectTexture
					: Sprites.CarriedObjectTexture,
				new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
				new Rect2(frame * 16, (int)_facing * 16, 16, 16));
		}
		else if (_activeTransformation != 0)
		{
			DrawTexture(
				_transformedLink.Texture(
					_activeTransformation,
					(int)_facing,
					_transformationFrame,
					DamagePaletteActive),
				new Vector2(-16, -16));
		}
		else if (IsAttacking)
		{
			DrawSwordPose();
		}
		else
		{
			DrawWalkLinkBody(GetWalkAnimationFrame(), Vector2.Zero);
		}
	}

	private void DrawTerrainEffect()
	{
		LinkTerrainEffectFrame? terrainEffect = GetCurrentTerrainEffect();
		if (terrainEffect is not null)
		{
			// The original queues terrain sprites before Link, giving their
			// lower OAM indices priority where pixels overlap. Godot gives the
			// later draw priority, so the equivalent composition is drawn last.
			DrawTexture(terrainEffect.Texture, terrainEffect.Offset);
		}
	}

	private void DrawScriptedLinkAnimation(int mode)
	{
		if (mode == 0x18)
		{
			var frame = NativeLinkBodyFrame;
			DrawTexture(frame.Texture, frame.Offset);
			return;
		}
		Texture2D texture = ScriptedLinkAnimationTexture(
			mode, DamagePaletteActive);
		DrawTexture(
			texture,
			mode is 0x06 or 0x07
				? new Vector2(-16, -16)
				: mode == 0x0a ? DrownSpriteOrigin : NormalSpriteOrigin);
	}

	private Texture2D ScriptedLinkAnimationTexture(
		int mode,
		bool damagePalette)
	{
		return mode switch
		{
			0x02 => _scriptedCollapsedTexture ??= new AtlasTexture
				{ Atlas = Sprites.DeathTexture, Region = new Rect2(4 * 16, 0, 16, 16) },
			0x06 => Sprites.SideScrollSquishXTexture,
			0x07 => Sprites.SideScrollSquishYTexture,
			0x08 => damagePalette
				? Sprites.DamageFunnyJokeDanceLeftTexture
				: Sprites.FunnyJokeDanceLeftTexture,
			0x09 => damagePalette
				? Sprites.DamageFunnyJokeDanceRightTexture
				: Sprites.FunnyJokeDanceRightTexture,
			0x0a => _scriptedDrownTextures[GetDrownAnimationFrame() * 8 + (int)_facing * 2 + (damagePalette ? 1 : 0)] ??= new AtlasTexture
				{ Atlas = damagePalette ? Sprites.DamageDrownTexture : Sprites.DrownTexture,
					Region = new Rect2(GetDrownAnimationFrame() * 16, (int)_facing * 16, 16, 16) },
			0x0d => _scriptedFallTextures[GetFallInHoleFrame() * 2 + (damagePalette ? 1 : 0)] ??= new AtlasTexture
				{ Atlas = damagePalette ? Sprites.DamageFallInHoleTexture : Sprites.FallInHoleTexture,
					Region = new Rect2(GetFallInHoleFrame() * 16, 0, 16, 16) },
			0x0e => damagePalette
				? Sprites.DamageGetItemOneHandTexture
				: Sprites.GetItemOneHandTexture,
			0x0f => damagePalette
				? Sprites.DamageGetItemTwoHandTexture
				: Sprites.GetItemTwoHandTexture,
			0x10 => ScriptedWalkingTexture(damagePalette),
			0x18 => Sprites.NativeFrame(_inventory.RuntimeState.ReadWramByte(0xd031), (int)_facing, damagePalette).Texture,
			0x1c => damagePalette
				? Sprites.DamageGetItemOneHandRightTexture
				: Sprites.GetItemOneHandRightTexture,
			_ => throw new InvalidOperationException(
				$"Unsupported scripted Link animation mode ${mode:x2}.")
		};
	}

	private LinkTerrainEffectFrame? GetCurrentTerrainEffect()
	{
		// _drawObjectTerrainEffects rejects side-view tilesets, grounded
		// screen-scroll state, and negative Z. Ground effects otherwise remain
		// present while standing, using an item, or riding an ordinary moving
		// platform; they are not gated by the walk animation.
		if (!Visible || _minecartRideControlled || _companionRideControlled ||
			_world.ScreenScrolling || _world.SideScrolling ||
			!IsGroundedForFloorButton)
		{
			return null;
		}

		TerrainInfo terrain = _world.GetActiveTerrain(Position).Terrain;
		return _terrainEffects.FrameFor(
			terrain.Tile, Position, _world.FrameCounter);
	}

	private static Rect2 GetFrame(Facing facing, int frame)
	{
		return new Rect2(frame * 16, (int)facing * 16, 16, 16);
	}

	private void DrawSwordPose()
	{
		// Parent-owned pictures were already published at the graphics tail;
		// a retained weapon can also accompany Link's own animation.
		Vector2 drawOffset = SwordDrawOffset;
		DrawSword();
		if (SideScrollSwimming) DrawSideScrollSwimmingBody(drawOffset);
		else if (_sideScrollAirborne || _topDownAirborne) DrawAirborneLinkBody(drawOffset);
		else if (_ledgeJumpState != LedgeJumpState.None) DrawWalkLinkBody(_linkWalkAnimationFrame, drawOffset);
		else DrawWalkLinkBody(GetWalkAnimationFrame(), drawOffset);
	}

	private void DrawSideScrollSwimmingBody(Vector2 drawOffset)
	{
		Texture2D swimTexture = _sideScrollSwimMermaidAnimation
			? DamagePaletteActive
				? Sprites.DamageSideScrollMermaidTexture
				: Sprites.SideScrollMermaidTexture
			: DamagePaletteActive
				? Sprites.DamageSideScrollSwimTexture
				: Sprites.SideScrollSwimTexture;
		DrawTextureRectRegion(
			swimTexture,
			new Rect2(
				NormalSpriteOrigin + drawOffset,
				new Vector2(16, 16)),
			new Rect2(
				_sideScrollSwimAnimationFrame * 16,
				(int)_facing * 16,
				16,
				16));
	}

	private void DrawAirborneLinkBody(Vector2 drawOffset)
	{
		if (_airborneLinkAnimationMode == AirborneLinkAnimationMode.SideScrollSwim)
		{
			DrawSideScrollSwimmingBody(drawOffset);
			return;
		}
		if (_airborneLinkAnimationMode == AirborneLinkAnimationMode.Walk)
		{
			DrawWalkLinkBody(_linkWalkAnimationFrame, drawOffset);
			return;
		}

		var frame = NativeLinkBodyFrame;
		DrawTexture(frame.Texture, frame.Offset + drawOffset);
	}

	private void DrawWalkLinkBody(int frame, Vector2 drawOffset)
	{
		var body = SelectWalkBodyFrame(frame);
		DrawTextureRectRegion(
			body.Texture,
			new Rect2(NormalSpriteOrigin + drawOffset, new Vector2(16, 16)),
			body.Region);
	}

	private (Texture2D Texture, Rect2 Region) SelectWalkBodyFrame(int frame, bool? damagePalette = null)
	{
		bool damage = damagePalette ?? DamagePaletteActive;
		int offset = _inventory.RuntimeState.ReadWramByte(0xd034);
		Rect2 region = new(frame * 16, (int)_facing * 16, 16, 16);
		if (offset == 0x28)
		{
			// specialObjectAnimationsAndDamage.s:@getLinkWalkingAnimation
			// selects underwater var34=$28 before cart, pushing and shield.
			// WALK $54/$80 therefore use graphics $7c/$a8, the same imported
			// atlas cells as LINK_ANIM_MODE_MERMAID, with WALK's own clock.
			return (damage ? Sprites.DamageSideScrollMermaidTexture : Sprites.SideScrollMermaidTexture, region);
		}
		if (offset == 4)
			return (damage ? Sprites.DamageMinecartLinkTexture : Sprites.MinecartLinkTexture, region);
		if (offset == 8)
			return (damage ? Sprites.DamageCarriedObjectTexture : Sprites.CarriedObjectTexture, region);
		if (offset == 0x10)
			return (damage ? Sprites.DamagePushTexture : Sprites.PushTexture, region);
		if (offset is 0x14 or 0x18 or 0x1c or 0x20)
		{
			int variant = (offset - 0x14) / 4;
			region.Position += new Vector2(variant * 32, 0);
			return (
				damage
					? Sprites.DamageShieldLinkTexture
					: Sprites.ShieldLinkTexture,
				region);
		}
		if (offset != 0) throw new NotSupportedException($"func_4553 WALK var34=${offset:x2}: selected graphics have no bound Link presenter.");
		return (damage ? Sprites.DamageTexture : Sprites.WalkTexture, GetFrame(_facing, frame));
	}

	private void DrawSword()
	{
		int animation = _swordState == SwordActionState.Spin
			? GetSwordArcIndex() - 16
			: _linkItems.SwordAnimation(
				(int)_facing,
				GetSwordPosePhase());
		DrawTextureRectRegion(
			SwordUsesChargedPalette ? Sprites.ChargedSwordTexture : Sprites.SwordTexture,
			new Rect2(
				ActiveSwordSpritePosition - new Vector2(16, 16),
				new Vector2(32, 32)),
			new Rect2(animation * 32, 0, 32, 32));
	}

	internal static Vector2 GetSwordSpritePositionForValidation(int arcIndex, string assetRoot) =>
		GetSwordSpritePosition(arcIndex, LinkItemDatabase.For(assetRoot));

	private static Vector2 GetSwordSpritePosition(int arcIndex, LinkItemDatabase items)
	{
		SwordArc arc = items.SwordArc(arcIndex);
		// itemInitializeFromLinkPosition uses the table offset for yh, then
		// gives the child sword zh = Link.zh - 2. Apply that visual height
		// separately; its collision center remains at the table's raw Y/X.
		return new Vector2(arc.OffsetX, arc.OffsetY - 2);
	}

	private static int CheckedSeedShooterAngle(int angle) =>
		angle is >= 0 and < 8
			? angle
			: throw new ArgumentOutOfRangeException(
				nameof(angle), angle, "ITEM_SHOOTER angle must be 0-7.");

	private static int CheckedSeedShooterVariant(int variant) =>
		variant is >= 0 and < 3
			? variant
			: throw new ArgumentOutOfRangeException(
				nameof(variant), variant,
				"ITEM_SHOOTER graphics variant must be normal, minecart, or underwater.");
}
