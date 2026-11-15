
namespace Oracles;

public partial class Player : Node2D
{
	internal string AssetRoot { get; private set; } = string.Empty;

	private bool _enemyGrabRequested;
	private int _enemyGrabSubstate = -1;
	private bool _enemyGrabCollisionDisabled;
	private bool _enemyGrabUpdated;

	internal bool EnemyGrabActive => _enemyGrabSubstate >= 0;
	internal bool EnemyGrabPending => _enemyGrabRequested;
	internal int EnemyGrabSubstate => _enemyGrabSubstate;

	internal bool RequestLikeLikeGrab()
	{
		// collisionEffect3d follows the caller's ordinary collision gates.
		// Transformed/riding Link has a nonzero special-object ID.
		if (!PatchCollisionsEnabled || _enemyInvincibilityFrames != 0 ||
			_activeTransformation != 0 || _companionRideControlled ||
			_minecartRideControlled || _raftRideControlled ||
			_world.NativeWarpsDisabled || _enemyGrabRequested) return false;
		_enemyGrabRequested = true;
		var damage = EnemyBehaviorTables.For(AssetRoot).LikeLike.LinkDamage;
		_enemyInvincibilityFrames = unchecked((sbyte)damage[1].Value);
		// LINKDMG_2c plays damage audio without subtracting any health.
		_world.PlaySound(SoundId.SndDamageLink);
		return true;
	}

	internal void CopyLikeLikePosition(Vector2 position)
	{
		// objectCopyPosition copies only YH/XH/ZH. Do not WarpTo: that would
		// clear fractions, active parents, invincibility and capture state.
		SetScriptedCoordinateHigh(false, OracleObjectPosition.HighByte(position.Y));
		SetScriptedCoordinateHigh(true, OracleObjectPosition.HighByte(position.X));
		_topDownAirZFixed &= 0xff;
		_enemyGrabCollisionDisabled = true;
		QueueRedraw();
	}

	internal void ReleaseLikeLikeGrab()
	{
		// likelike_releaseLink restores collision in the enemy pass. Link's
		// next dispatch installs the release invincibility and normal state.
		_enemyGrabSubstate = 4;
		_enemyGrabCollisionDisabled = false;
	}

	private bool AdvanceEnemyGrab()
	{
		if (_enemyGrabRequested)
		{
			_enemyGrabRequested = false;
			_enemyGrabSubstate = 0;
		}
		if (!EnemyGrabActive) return false;
		_enemyGrabUpdated = true; // wcc92 bit7 lasts through the entire update.
		if (_enemyGrabSubstate == 0)
		{
			_enemyGrabSubstate = 1;
			_world.SetNativeWarpsDisabled(true);
			_world.ClearItemParents(this);
			ClearShieldParent();
			CancelSwordAttack();
			CancelShovelAction();
			_startedParentItemAnimations = 0;
		}
		else if (_enemyGrabSubstate == 2) StartGrabRelease();
		else if (_enemyGrabSubstate == 3) AdvanceGrabRelease();
		else if (_enemyGrabSubstate == 4)
		{
			_enemyInvincibilityFrames = EnemyBehaviorTables.For(AssetRoot).LikeLike.ReleaseInvincibility;
			_world.SetNativeWarpsDisabled(false);
			_enemyGrabSubstate = -1;
			_walking = false;

			ResetLinkWalkAnimation();
			Visible = true;
		}
		QueueRedraw();
		return true;
	}

	private void CancelEnemyGrab()
	{
		if (EnemyGrabActive) _world.SetNativeWarpsDisabled(false);
		_enemyGrabRequested = false;
		_enemyGrabSubstate = -1;
		_enemyGrabCollisionDisabled = false;
		_enemyGrabUpdated = false;
		_enemyGrabReleaseCounter = 0;
		_collapseUpdated = false;
	}

	internal const int NormalZIndex = 10;
	internal const int DivingZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
	internal const int AlternateTextboxPaletteZIndex = 13;
	private const int NormalTopDownSpeed = 0x28;
	private int _electricShockCounter;
	private bool _electricShockPending;
	private ShaderMaterial? _electricShockMaterial;
	internal bool ElectricShockActive => _electricShockPending || _electricShockCounter != 0;
	internal int ElectricShockCounter => _electricShockCounter;
	private const int GrassTopDownSpeed = 0x1e;
	private const int StairsTopDownSpeed = 0x14;
	private static readonly Vector2 NormalSpriteOrigin = new(-8, -8);
	private const int PunchCollisionFrames = 4;
	// specialObjectAnimationData.s: modes$21/$34 contain 8 and 3+8+3
	// eligible animation advances before their terminal parameter$86.
	private const int FistPunchFrames = 8;
	private const int ExpertPunchFrames = 14;
	private const int EnemyInvincibilityFrames = 0x22;
	private const int EnemyKnockbackFrames = 0x0f;
	private const int DeathCollapsedFrames = 0x4c;
	private const int NewGameSlowFallGravity = 0x0c;
	private const int RoomWarpFallInitialSpeedZ = 0x0020;
	private const int RoomWarpFallGravity = 0x20;
	private const int RoomWarpFallCollapsedFrames = 0x1e;
	private static readonly Vector2 DrownSpriteOrigin = new(-8, -4);
	private const int TerrainHazardDamageQuarters = 2;
	private IPlayerWorld _world = null!;
	internal IPlayerWorld World => _world;
	private InventoryState _inventory = null!;
	private OracleRandom _random = null!;
	internal PlayerSpriteLibrary Sprites { get; private set; } = null!;
	private Texture2D? _companionRideTexture;
	private Texture2D? _damageCompanionRideTexture;
	private Vector2 _companionRideTextureOffset;
	private int _companionRideZFixed;
	private Texture2D _terrainShadowTexture = null!;
	private Vector2 _terrainShadowOffset;
	private LinkItemDatabase? _linkItemsCache;
	private LinkItemDatabase _linkItems => _linkItemsCache ??= LinkItemDatabase.For(AssetRoot);
	private SideScrollPlayerDatabase? _sideScrollPlayerDataCache;
	private SideScrollPlayerDatabase _sideScrollPlayerData => _sideScrollPlayerDataCache ??= SideScrollPlayerDatabase.For(AssetRoot);
	private TopDownSwimmingDatabase? _topDownSwimmingDataCache;
	private TopDownSwimmingDatabase _topDownSwimmingData => _topDownSwimmingDataCache ??= TopDownSwimmingDatabase.For(AssetRoot);
	private MermaidSwimmingDatabase? _mermaidSwimmingDataCache;
	private MermaidSwimmingParameters MermaidSwimming => (_mermaidSwimmingDataCache ??= MermaidSwimmingDatabase.For(AssetRoot)).Parameters;
	private LinkFloorBreakDatabase? _floorBreakCache;
	private LinkFloorBreakDatabase FloorBreaks => _floorBreakCache ??= new(AssetRoot);
	private LinkTerrainEffectDatabase? _terrainEffectsCache;
	private LinkTerrainEffectDatabase _terrainEffects => _terrainEffectsCache ??= LinkTerrainEffectDatabase.For(AssetRoot);
	private TransformedLinkDatabase? _transformedLinkCache;
	private TransformedLinkDatabase _transformedLink => _transformedLinkCache ??= new TransformedLinkDatabase(AssetRoot + "metadata/transformed_link.tsv");
	private Vector2 _precisePosition;
	private Vector2 _minecartMainObjectPosition;
	private Vector2 _lastSafePosition;
	private HazardType _drowningHazard;
	private Facing _facing
	{
		get => _inventory is null ? Facing.Down : (Facing)_inventory.RuntimeState.ReadWramByte(0xd008);
		set => _inventory.RuntimeState.SetWramByte(0xd008, (byte)value);
	}
	private Facing _localRespawnFacing = Facing.Down;
	private int _linkWalkAnimationFrame => _inventory.RuntimeState.ReadWramByte(0xd031) == 0x54 ? 0 : 1;
	private int _linkWalkAnimationCounter => _inventory.RuntimeState.ReadWramByte(0xd020);
	private AirborneLinkAnimationMode _airborneLinkAnimationMode;
	private double _swordFrameAccumulator;
	private double _shovelFrameAccumulator;
	private double _seedSatchelFrameAccumulator;
	private double _harpFrameAccumulator;
	private double _punchFrameAccumulator;
	private bool _fallInHoleInitializePending => _fallingInHole && _forcedRespawnPhase==2;
	private double _ledgeUpdateAccumulator;
	private double _sideScrollUpdateAccumulator;
	private double _topDownAirUpdateAccumulator;
	private bool _minecartJumpControlled;
	private bool _minecartRideControlled;
	private bool _companionJumpControlled;
	private bool _companionDismountJump;
	private bool _companionJumpAnimationDeferred;
	private bool _companionRideControlled;
	private bool _raftRideControlled;
	private bool NativeMovementSuppressed => (_inventory.RuntimeState.ReadField("wcc95") & 0x80) != 0;
	private int _minecartJumpAngle = 0xff;
	private int _companionJumpAngle = 0xff;
	private int _ledgeGroundYFixed;
	private int _ledgeGroundXFixed;
	private int _ledgeZFixed;
	private int _ledgeSpeedZ;
	private int _ledgeSpeedRaw;
	private int _ledgeGravity;
	private int _ledgeLandSound;
	private int _ledgeCliffLength;
	private int _ledgeAnimationPhase;
	private int _ledgeAnimationCounter;
	private int[] _ledgeAnimationDurations = [];
	private Vector2I _ledgeDirection;
	private LedgeJumpState _ledgeJumpState;
	private bool _ledgeCrossedScreen;
	private bool _ledgeUpdatedInPhysics;
	private int _sideScrollYFixed;
	private int _sideScrollSpeedZ;
	private int _sideScrollAngle = 0xff;
	private int _sideScrollSpeedRaw;
	private int _sideScrollTargetSpeedRaw;
	private int _sideScrollVelocityCounter;
	private int _sideScrollVelocityInterval;
	private int _sideScrollTerrainMode = -1;
	private int _sideScrollForceIcePhysics;
	private int _sideScrollSwimmingState;
	private int _sideScrollSwimBurstState;
	private int _sideScrollSwimBurstCounter;
	private int _sideScrollSwimAnimationFrame;
	private int _sideScrollSwimAnimationCounter;
	private bool _sideScrollSwimMermaidAnimation;
	private int _sideScrollMermaidImpulseCounter;
	private int _sideScrollBubbleCounter;
	private bool _swordUnderwaterAnimation;
	private bool _swordMinecartAnimation;
	private bool _swordUpdatedInPhysics;
	private bool _linkPhysicsActive;
	private double _linkPhysicsDelta;
	private bool _lowerParentsUpdatedInPhysics;
	private bool _bombchuParentInitializedThisUpdate;
	private bool _shovelChildContactPending;
	private bool _swordChildInitializationPending;
	private bool _swordParentBeforeMovement;
	private readonly bool?[] _capeParents = new bool?[2];
	private bool _capeJumpEligible;
	private bool _topDownReducedGravity;
	internal bool CapeGliding => _topDownReducedGravity || _sideScrollReducedGravity;
	internal int CapeParentSlotMask => (_capeParents[0].HasValue ? 1 : 0) | (_capeParents[1].HasValue ? 2 : 0);
	internal bool CapeParentActive => CapeParentSlotMask != 0;
	private bool _sideScrollReducedGravity;
	// Shared link.s handlers use the same live substate/counter1 bytes.
	private byte NativeLinkSubstate
	{
		get => _inventory.RuntimeState.ReadWramByte(0xd005);
		set => _inventory.RuntimeState.SetWramByte(0xd005,value);
	}
	private byte NativeLinkCounter
	{
		get => _inventory.RuntimeState.ReadWramByte(0xd006);
		set => _inventory.RuntimeState.SetWramByte(0xd006,value);
	}
	private bool RespawnWaiting => _forcedRespawnPhase==3 && NativeLinkSubstate==2;
	private bool RespawnRecovering => _forcedRespawnPhase==3 && NativeLinkSubstate==3;
	private bool _instantRespawnCollisionDisabled;
	private bool _sideScrollSquishPending;
	private bool _sideScrollSquishVertical;
	private LinkSquishAnimation? _squishAnimation;
	internal LinkSquishAnimation? SquishAnimation => _squishAnimation;
	private int _sideScrollAnimationPhase => _sideScrollAirborne && !_sideScrollJumpSoundPending ? NativeAirAnimationFrame : 0;
	private bool _sideScrollAirborne;
	private bool _sideScrollJumpSoundPending;
	private int _topDownAirZFixed;
	private int _topDownAirSpeedZ;
	private int _topDownMovementAngle = 0xff;
	private int _topDownMovementSpeedRaw;
	private int _topDownMovementTargetSpeedRaw;
	private int _topDownMovementTerrainMode;
	private int _topDownMovementVelocityCounter;
	private int _topDownMovementVelocityInterval;
	private int _topDownAirAnimationPhase => (_topDownAirborne && !_topDownJumpSoundPending || _scriptedLinkAnimationMode == 0x18)
		? NativeAirAnimationFrame : 0;
	private int NativeAirAnimationFrame => LinkNativeAnimationDatabase.For(AssetRoot).FrameIndex(
		_inventory.RuntimeState, _airborneLinkAnimationMode == AirborneLinkAnimationMode.RocsCape ? 0x19 : 0x18,
		_airborneLinkAnimationMode == AirborneLinkAnimationMode.RocsCape ? 3 : 4);
	private bool _topDownAirborne;
	private bool _topDownJumpSoundPending;
	private bool _topDownAirIgnoresWalls;
	private Vector2? _topDownAirLandingTarget;
	private int _topDownSwimmingState;
	private int _topDownSwimmingEntryCounter;
	private int _topDownSwimAngle = 0xff;
	private int _topDownSwimSpeedRaw;
	private int _topDownSwimTargetSpeedRaw;
	private int _topDownSwimVelocityCounter;
	private bool _topDownMermaidVelocityInitialized;
	private int _topDownMermaidImpulseCounter;
	private int _topDownSwimBurstState;
	private int _topDownSwimBurstCounter;
	private int _topDownSwimAnimationFrame;
	private int _topDownSwimAnimationCounter;
	private bool _topDownDiving;
	private int _topDownDiveCounter;
	private float _enemyInvincibilityFrames;
	// w1Link.health is a live fractional damage accumulator, initialized to $01.
	// It survives healing, ring changes and room movement; it is not save health.
	private int _damageAccumulator = 1;
	private float _enemyKnockbackFrames;
	private int _enemyKnockbackAngle;
	private int _pendingSwordKnockbackFrames;
	private int _pendingSwordKnockbackAngle;
	private double _deathUpdateAccumulator;
	private bool _deathPending;
	private bool _deathAnimationActive;
	private bool _deathSlowFadeRequested;
	private bool _gameOverRequested;
	private int _deathAnimationFrame;
	private int _deathAnimationCounter;
	private int _deathSequenceIndex;
	private int _deathSpinLoopsRemaining;
	private Vector2 _holePullCenter;
	private int _holePullCounter
	{
		get => _inventory.RuntimeState.ReadField("wStandingOnTileCounter");
		set => _inventory.RuntimeState.SetField("wStandingOnTileCounter",unchecked((byte)value));
	}
	private int _holePullPackedPosition = -1;
	private bool _fallInHoleWarpPending;
	private SwordActionState _swordState;
	private int _swordStateFrame;
	private int _swordChargeCounter;
	private int _currentSwordDamage;
	private bool _swordPokeReturnsToHeld;
	private bool _doubleEdgedDamagePending;
	private bool _swordPokeCollisionEnabled;
	private bool _pendingSwordEnemyContact;
	private int _heartRingDistanceFixed;
	private int _activeTransformation;
	private int _transformationFrame;
	private int _transformationTicks;
	private int _shovelFrame;
	private bool _usingShovel;
	private bool _usingSeedSatchel;
	private int _harpActionUpdate;
	private int _harpActionFrames;
	private int _seedSatchelFrame;
	private int _seedSatchelActionFrames;
	private bool _usingPunch;
	private bool _expertPunch;
	private int _punchFrame;
	private int _punchDamage;
	private bool _itemAllocationBlockedThisUpdate;
	private bool _swordJinxUpdatedInPhysics;
	private Vector2 _lastMovementInput;
	private bool _screenTransitionTerrainMotion;
	private bool _walking;

	private bool _pullingIntoHole;
	private bool _drowning;
	private bool _drownRespawning;
	private int _topDownDrownPhase;
	private bool _fallingInHole;
	private bool _fallInHoleRespawning;
	private enum ScriptControl { None, Cutscene, SimulatedInput, ForcedState08 }
	private ScriptControl _scriptControl;
	private bool _cutsceneControlled => _scriptControl != ScriptControl.None;
	private int _forcedState08Phase;
	private bool _forcedState08LowPriority;
	private object? _cutsceneControlOwner;
	private bool _getItemOneHandPose;
	private bool _getItemTwoHandPose;
	private bool _forcedSwordParentInitializing;
	private int? _scriptedLinkAnimationMode;
	private Texture2D? _scriptedCollapsedTexture;
	private readonly Texture2D?[] _scriptedWalkTextures = new Texture2D?[11 * 8];
	private readonly Texture2D?[] _scriptedFallTextures = new Texture2D?[6];
	private readonly Texture2D?[] _scriptedDrownTextures = new Texture2D?[16];
	private int _cutsceneDrawZFixed;
	private bool _carriedObjectPose;
	private BraceletActionPose? _braceletActionPose;
	private bool _braceletLiftCollisionsDisabled;
	private CutsceneSpriteRenderer? _newGameFallRenderer;
	private CutsceneSpriteRenderer? _cutsceneSpriteRenderer;
	private ObjectSpriteFrame? _cutsceneSpriteFrame;
	private Facing? _cutsceneIdleFacing;
	private ObjectSpriteFrame[]? _newGameFallFrames;
	private int _newGameFallFrame;
	private int _newGameFallFrameTicks;
	private int _newGameFallZFixed;
	private int _newGameFallSpeedZ;
	private bool _newGameSlowFalling;
	private int _roomWarpFallFrame;
	private int _roomWarpFallFrameTicks;
	private int _roomWarpFallZFixed;
	private int _roomWarpFallSpeedZ;
	private int _roomWarpFallCollapsedCounter;
	private bool _roomWarpFallActive;
	private bool _roomWarpFallCollapsed;
	private CutsceneSpriteRenderer? _harpRenderer;
	private ObjectSpriteFrame[]? _harpFrames;
	private ObjectSpriteFrame[]? _cutsceneHarpFrames;
	private ObjectSpriteFrame[]? _itemHarpFrames;
	private int _harpFrame;
	private int _harpFrameTicks;
	private bool _harpPoseActive;
	// Active state02 phases; pending requests live in the native shared byte.
	private int _forcedRespawnPhase;
	private bool ForcedRespawnRequested => _inventory.RuntimeState.ReadField("wLinkForceState")==2;
	private bool _nativeInitializationPending;
	// A native room load clears w1Link. Re-enabling it dispatches state00
	// once before state01 movement, even when text/$81 would block state01.
	internal void RequestNativeLinkInitialization()=>_nativeInitializationPending=true;

	public int HealthQuarters => _inventory.HealthQuarters;
	public int Rupees => _inventory.Rupees;
	public int MaxHealthQuarters => _inventory.MaxHealthQuarters;
	public bool IsDying => _deathPending || _deathAnimationActive;
	internal bool BraceletLiftCollisionsDisabled =>
		_braceletLiftCollisionsDisabled;
	public InventoryState Inventory => _inventory;
	// ROM_SEASONS uses treasure $4a for the Pirate's Bell, not Mermaid movement.
	private bool HasMermaidSuit => _inventory.RuntimeState.Game == OracleGame.Ages && _inventory.HasTreasure(TreasureId.MermaidSuit);
	public bool IsPullingIntoHole => _pullingIntoHole;
	public bool IsDrowning => _drowning;
	public bool IsFallingInHole => _fallingInHole;

	public Vector2I FacingVector => FacingVectorFor(_facing);
	public bool IsAttacking => _swordState != SwordActionState.None;
	public bool IsUsingShovel => _usingShovel;
	public bool IsUsingSeedSatchel => _usingSeedSatchel;
	public bool IsUsingSwitchHook => _world is not null && _world.SwitchHookActive;
	internal bool IsUsingSomaria => _world is not null && _world.SomariaActive;
	internal bool IsUsingBiggoron => _world is not null && _world.BiggoronActive;
	internal bool IsUsingRodOfSeasons => _world is not null && _world.RodOfSeasonsActive;
	internal bool IsUsingBoomerang => _world is not null && _world.BoomerangParentActive;
	internal bool IsUsingBombchu => _world is not null && _world.BombchuParentActive;
	private int _seedSatchelParentSlot = 3;
	private ParentItemUsageDatabase? _parentItemUsageSourceCache;
	private ParentItemUsageDatabase _parentItemUsage => _parentItemUsageSourceCache ??= new(AssetRoot);
	public bool IsUsingSeedShooter =>
		_world is not null && _world.SeedShooterActive;
	public bool IsUsingHarp => InstrumentParentActive && _inventory.RuntimeState.ReadWramByte(0xd504) != 0;
	public bool IsUsingShield => _inventory.RuntimeState.ReadField("wUsingShield") != 0;
	internal bool IsUsingPunch => _usingPunch;
	internal bool IsUsingExpertPunch => _usingPunch && _expertPunch;
	private uint _startedParentItemAnimations;
	// wLinkUsingItem1's high nibble is written only by parent animation
	// initialization and cleared by checkUseItems. Existing parent state
	// supplies cancellation; this mask records only starts in that update.
	internal bool StartedItemAnimationThisUpdate =>
		Started(TreasureId.Sword, IsAttacking) ||
		Started(TreasureId.Shovel, IsUsingShovel) ||
		Started(TreasureId.SeedSatchel, IsUsingSeedSatchel) ||
		Started(TreasureId.Shooter, IsUsingSeedShooter) ||
		Started(TreasureId.SwitchHook, IsUsingSwitchHook) ||
		Started(TreasureId.CaneOfSomaria, IsUsingSomaria) ||
		Started(TreasureId.BiggoronSword, IsUsingBiggoron) ||
		Started(TreasureId.RodOfSeasons, IsUsingRodOfSeasons) ||
		Started(TreasureId.Boomerang, IsUsingBoomerang) ||
		Started(TreasureId.Bombchus, true) ||
		Started(0x02, IsUsingPunch);
	// setLinkUsingItem1's low bit lasts until clearParentItem; its paired
	// high pulse cannot outlive that parent. Sample animation parent owners,
	// including Switch Hook's controller rather than its finished child.
	internal bool NativeItemUseActive =>
		Signals(TreasureId.Sword,IsAttacking) || Signals(TreasureId.Shovel,IsUsingShovel) ||
		Signals(TreasureId.SeedSatchel,IsUsingSeedSatchel) || Signals(TreasureId.Shooter,IsUsingSeedShooter) ||
		Signals(TreasureId.SwitchHook,IsUsingSwitchHook) || Signals(TreasureId.CaneOfSomaria,IsUsingSomaria) ||
		Signals(TreasureId.BiggoronSword,IsUsingBiggoron) || Signals(TreasureId.Boomerang,IsUsingBoomerang) ||
		Signals(TreasureId.RodOfSeasons,IsUsingRodOfSeasons) ||
		Signals(TreasureId.Punch,IsUsingPunch);
	private bool IsUsingItem => IsAttacking || OtherItemAnimationActive;
	private bool OtherItemAnimationActive =>
		IsUsingShovel || IsUsingSeedSatchel ||
		IsUsingSeedShooter || IsUsingHarp || IsUsingPunch || IsUsingSwitchHook || IsUsingSomaria || IsUsingBiggoron || IsUsingBoomerang ||
		IsUsingRodOfSeasons || IsUsingBombchu || Started(TreasureId.Bombchus, true);
	internal bool IsPushing => _world.TilePushingDirection != 0xff && _inventory.RuntimeState.ReadWramByte(0xd034) == 0x10;
	internal SwordActionState SwordState => _swordState;
	internal SwordActionState SwordCollisionState =>
		_swordState == SwordActionState.Poke && _swordPokeCollisionEnabled
			? _swordPokeReturnsToHeld ? SwordActionState.Swing : SwordActionState.Held
			: _swordState;
	internal int SwordStateFrame => _swordStateFrame;
	internal int SwordDamage => IsUsingPunch ? _punchDamage : _currentSwordDamage;
	internal EnemyKnockbackStrength SwordKnockbackStrength =>
		IsUsingPunch
			? (_expertPunch
				? EnemyKnockbackStrength.High
				: EnemyKnockbackStrength.Low)
			: _swordState switch
			{
				SwordActionState.Spin => EnemyKnockbackStrength.High,
				SwordActionState.Held or SwordActionState.Charged =>
					EnemyKnockbackStrength.Low,
				_ when _inventory.SwordLevel <= 1 =>
					EnemyKnockbackStrength.Low,
				_ => EnemyKnockbackStrength.Normal
			};
	// itemInitializeFromLinkPosition gives ITEM_SWORD and ITEM_PUNCH the
	// current Link.zh minus two, including a retained weapon on a cliff jump.
	internal int MeleeItemZ => unchecked((sbyte)ObjectZHigh) - 2;
	internal bool UsesAirborneSwordPose =>
		IsAttacking && (_sideScrollAirborne || _topDownAirborne || _ledgeJumpState != LedgeJumpState.None);
	internal bool UsesSideScrollSwimmingSwordPose =>
		IsAttacking && SideScrollSwimming;
	internal bool SwordUsesUnderwaterAnimation =>
		_swordState == SwordActionState.Swing && _swordUnderwaterAnimation;
	internal bool AirborneLinkUsesJumpAnimation =>
		_airborneLinkAnimationMode == AirborneLinkAnimationMode.Jump;
	internal int AirborneLinkBodyFrame => _airborneLinkAnimationMode switch
	{
		AirborneLinkAnimationMode.Walk => _linkWalkAnimationFrame,
		AirborneLinkAnimationMode.Jump or AirborneLinkAnimationMode.RocsCape when _topDownAirborne =>
			_topDownAirAnimationPhase,
		AirborneLinkAnimationMode.Jump or AirborneLinkAnimationMode.RocsCape when _sideScrollAirborne =>
			_sideScrollAnimationPhase,
		_ => 0
	};
	internal int LinkAnimationCounter => _inventory.RuntimeState.ReadWramByte(0xd020);
	internal Vector2 SwordDrawOffset =>
		new(0, _ledgeJumpState != LedgeJumpState.None ? LedgeZ : _topDownAirborne ? TopDownAirZ : 0);
	internal Vector2 ActiveSwordSpritePosition =>
		SwordSpritePosition + SwordDrawOffset;
	internal int SwordArcIndex => IsAttacking ? GetSwordArcIndex() : -1;
	internal bool SwordAllowsMovement =>
		_swordState is SwordActionState.Held or SwordActionState.Charged;
	internal bool SwordCanRestart => _swordState switch
	{
		SwordActionState.Swing =>
			_swordStateFrame >= _linkItems.Constants.SwordRestartFrame,
		SwordActionState.Held or SwordActionState.Poke => true,
		_ => false
	};
	internal bool SwordUsesChargedPalette =>
		_swordState == SwordActionState.Charged && (_swordStateFrame & 0x04) != 0;
	internal float InvincibilityFrames => _enemyInvincibilityFrames;
	internal float KnockbackFrames => _enemyKnockbackFrames;
	internal bool DamagePaletteActive =>
		_enemyInvincibilityFrames > 0.0f &&
		(_world.FrameCounter & 0x04) == 0;
	internal bool DeathAnimationActive => _deathAnimationActive;
	internal int DeathAnimationFrame => _deathAnimationFrame;
	internal int DeathAnimationCounter => _deathAnimationCounter;
	internal int DeathSpinLoopsRemaining => _deathSpinLoopsRemaining;
	internal ulong LinkAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.WalkTexture.GetImage());
	internal ulong DamageLinkAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.DamageTexture.GetImage());
	internal bool IsNewGameSlowFalling => _newGameSlowFalling;
	internal bool IsRoomWarpFalling => _roomWarpFallActive;
	internal bool RoomWarpFallCollapsed => _roomWarpFallCollapsed;
	internal int RoomWarpFallZ => _roomWarpFallZFixed >> 8;
	// w1Link.zh is independent of wLinkInAir and collision-enable flags.
	// Read the existing owner of each native movement state, without creating
	// a second height value for contact consumers. Minecart state0 clears Link.z.
	internal byte ObjectZHigh => unchecked((byte)((_roomWarpFallActive ? _roomWarpFallZFixed :
		_newGameSlowFalling ? _newGameFallZFixed :
		_ledgeJumpState != LedgeJumpState.None ? _ledgeZFixed :
		_companionRideControlled ? _companionRideZFixed :
		_minecartRideControlled ? 0 : _topDownAirZFixed) >> 8));
	internal bool IsGroundedForFloorButton =>
		_ledgeJumpState == LedgeJumpState.None && !_newGameSlowFalling &&
		!_sideScrollAirborne && !_topDownAirborne &&
		!TopDownSwimming && !_drowning && !_fallingInHole;
	internal bool IsOnGroundForBracelet => IsGroundedForFloorButton &&
		!_world.Underwater && !SideScrollSwimming && !_companionRideControlled &&
		!_minecartRideControlled && !_raftRideControlled;
	internal bool AcceptsRoomEntityContact =>
		!_instantRespawnCollisionDisabled && !_enemyGrabCollisionDisabled && !GaleActive && !_world.PlayerContactDisabled && !ElectricShockActive && _ledgeJumpState == LedgeJumpState.None && !_topDownAirborne &&
		!TopDownDiving && !SideScrollDrowningCollisionsDisabled && !IsUsingHarp;
	// checkLinkCollisionsEnabled has no Z test for an ordinary feather jump.
	internal bool PatchCollisionsEnabled => !_enemyGrabCollisionDisabled && !IsDying && !GaleActive && !_world.PlayerContactDisabled &&
		!ElectricShockActive && !NativeMovementSuppressed && _ledgeJumpState == LedgeJumpState.None && !TopDownDiving &&
		!SideScrollDrowningCollisionsDisabled && !IsUsingHarp && !_braceletLiftCollisionsDisabled &&
		_squishAnimation is not { State: > 0 } && !_instantRespawnCollisionDisabled;
	// bombUpgradeFairy.s checks Link.zh==0 before checkLinkVulnerable.
	internal bool NativeObjectVulnerable => PatchCollisionsEnabled &&
		!_nativeContactSignal &&
		_enemyInvincibilityFrames == 0 && _enemyKnockbackFrames == 0 &&
		!_drowning && !_fallingInHole && !_pullingIntoHole && !CutsceneControlled;
	internal bool BombFairyVulnerable => NativeObjectVulnerable &&
		(!_topDownAirborne || TopDownAirZ == 0);
	internal void ClearInteractionKnockback(bool clearInvincibility = false)
	{
		_enemyKnockbackFrames = 0;
		if (clearInvincibility) _enemyInvincibilityFrames = 0;
	}
	private bool SideScrollDrowningCollisionsDisabled =>
		_sideScrollSwimmingState == 3 && _instantRespawnCollisionDisabled;
	// objectCheckCollidedWithLink accepts signed Z in [-7,6]. Ordinary feather
	// jumps do not set the high wLinkInAir bit in checkLinkCollisionsEnabled.
	internal bool AcceptsGroundInteractionContact =>
		!_instantRespawnCollisionDisabled && !_enemyGrabCollisionDisabled && !GaleActive && !_world.PlayerContactDisabled && !ElectricShockActive && _ledgeJumpState == LedgeJumpState.None &&
		!TopDownDiving && !SideScrollDrowningCollisionsDisabled && !IsUsingHarp &&
		(!_topDownAirborne || ((TopDownAirZ + 7) & 0xff) < 14);
	internal bool OverlapsTimePortalHeight => !IsDying && !IsCarryingObject &&
		!_braceletLiftCollisionsDisabled &&
		_braceletActionPose is not (BraceletActionPose.Pull or BraceletActionPose.PullStrain or BraceletActionPose.LiftHigh) &&
		((!_topDownAirborne && _ledgeJumpState == LedgeJumpState.None) ||
			_topDownAirborne && ((TopDownAirZ + 7) & 0xff) < 14);
	internal bool AcceptsTimePortalContact => AcceptsGroundInteractionContact &&
		OverlapsTimePortalHeight && !_world.RidingObject;
	internal bool PassesNpcs => _world.PassesNpcs;
	internal bool NativeInteractionCollisionsEnabled =>
		PatchCollisionsEnabled && !_world.InteractionMenusDisabled && !NativeMovementSuppressed;
	internal bool NativePuzzleResetVulnerable => NativeInteractionCollisionsEnabled &&
		_enemyInvincibilityFrames == 0 && _enemyKnockbackFrames == 0;
	internal bool CanEnterMinibossPortal => NativeInteractionCollisionsEnabled &&
		!_companionRideControlled && !_minecartRideControlled && !_raftRideControlled;

	internal bool OverlapsMinibossPortal(Rect2 bounds) =>
		// wLinkGrabState & $be permits wall-grab $41, but excludes lifting
		// $c2, carrying $83, and the lever's held state.
		!IsDying && !_braceletLiftCollisionsDisabled && !IsCarryingObject &&
		RoomEntityManager.ObjectCollisionZOverlaps(EnemyContactZ, 0, 7) &&
		EnemyCollisionOverlaps(EnemyContactPosition, bounds);
	// collisionEffects.s:@checkHitLink uses wLinkObjectIndex ($d1 while
	// mounted), not the offset riding-Link sprite at w1Link ($d0).
	internal Vector2 EnemyContactPosition => _companionRideControlled
		? _world.MountedCompanionPosition ?? throw new InvalidOperationException(
			"collisionEffects.s:@checkHitLink requires the mounted wLinkObjectIndex=$d1 companion owner.")
		: _raftRideControlled
			? _world.MountedRaftPosition ?? throw new InvalidOperationException(
				"SPECIALOBJECT_RAFT $13 requires the mounted wLinkObjectIndex=$d1 owner.")
			: Position;

	internal bool EnemyContactHeightOverlaps(int enemyZ) =>
		RoomEntityManager.ObjectCollisionZOverlaps(enemyZ, EnemyContactZ, 7);

	internal int EnemyContactZ => _companionRideControlled || _raftRideControlled
		? _companionRideZFixed >> 8 : _topDownAirborne ? TopDownAirZ : 0;

	internal bool OverlapsEnemyCollision(Rect2 bounds, int enemyZ = 0)
	{
		if (!PatchCollisionsEnabled || !EnemyContactHeightOverlaps(enemyZ))
			return false;
		return EnemyCollisionOverlaps(EnemyContactPosition, bounds);
	}

	internal static bool EnemyCollisionOverlaps(Vector2 position, Rect2 bounds)
	{
		// bank0.s:checkObjectsCollidedFromVariables adds the radii and
		// compares unsigned bytes: the negative edge is included, positive excluded.
		return RoomEntityManager.ObjectCollisionXYOverlaps(bounds,
			new Rect2(position - Vector2.One * 6, Vector2.One * 12));
	}
	// commonCode.s:companionTryToMount requires ordinary, vulnerable Link,
	// with no swimming, grabbing or airborne state.
	internal bool CanMountCompanion =>
		_activeTransformation == 0 && !IsDying && IsGroundedForFloorButton &&
		AcceptsRoomEntityContact && !IsCarryingObject && !_braceletLiftCollisionsDisabled &&
		_enemyInvincibilityFrames == 0 && _enemyKnockbackFrames == 0 && !_world.RidingObject &&
		NativeNormalStateForInteraction;
	internal bool CompanionMountInterrupted => IsCarryingObject || _braceletLiftCollisionsDisabled ||
		_deathAnimationActive || EnemyGrabActive || WallmasterGrabActive || _fallingInHole || _drowning && _topDownDrownPhase >= 2 ||
		_getItemStatePhase >= 2 || _forcedRespawnPhase >= 2 || _forcedState08Phase >= 2;
	internal bool CanAcceptShieldCollision =>
		IsUsingShield && PatchCollisionsEnabled &&
		!_braceletLiftCollisionsDisabled && !IsDying &&
		_enemyInvincibilityFrames == 0.0f &&
		_enemyKnockbackFrames == 0.0f;
	internal int LedgeZ => _ledgeZFixed >> 8;
	internal int LedgeSpeedZ => _ledgeSpeedZ;
	internal int LedgeSpeedRaw => _ledgeSpeedRaw;
	internal int LedgeCliffLength => _ledgeCliffLength;
	internal bool CanStartTerrainJump => !GrabStateActive;
	internal bool CanTurnForTerrainJump => !IsUsingItem;
	internal int LedgeAnimationPhase => _ledgeAnimationPhase;
	internal bool SideScrollAirborne => _sideScrollAirborne;
	internal bool SideScrollClimbing
	{
		get => _inventory.RuntimeState.ReadField("wLinkClimbingVine") != 0;
		private set => _inventory.RuntimeState.SetField("wLinkClimbingVine", value ? (byte)1 : (byte)0);
	}
	internal int SideScrollSpeedZ => _sideScrollSpeedZ;
	internal int SideScrollYFixed => _sideScrollYFixed;
	internal int SideScrollAngle => _sideScrollAngle;
	internal int LinkMovementAngle => _world.SideScrolling
		? _sideScrollAngle
		: TopDownSwimming
			? _topDownSwimAngle
			: AngleForVector(_lastMovementInput);
	internal int SideScrollSpeedRaw => _sideScrollSpeedRaw;
	internal int SideScrollAnimationPhase => _sideScrollAnimationPhase;
	internal bool SideScrollSwimming => _sideScrollSwimmingState != 0;
	internal int SideScrollSwimmingState => _sideScrollSwimmingState;
	internal int SideScrollSwimBurstState => _sideScrollSwimBurstState;
	internal int SideScrollSwimBurstCounter => _sideScrollSwimBurstCounter;
	internal int SideScrollSwimAnimationFrame =>
		_sideScrollSwimAnimationFrame;
	internal int SideScrollSwimAnimationCounter =>
		_sideScrollSwimAnimationCounter;
	internal bool SideScrollSwimMermaidAnimation =>
		_sideScrollSwimMermaidAnimation;
	internal bool TopDownSwimming => _topDownSwimmingState != 0;
	internal int TopDownSwimmingState => _topDownSwimmingState;
	internal int TopDownSwimmingEntryCounter => _topDownSwimmingEntryCounter;
	internal int TopDownSwimAngle => _topDownSwimAngle;
	internal int TopDownSwimSpeedRaw => _topDownSwimSpeedRaw;
	internal int TopDownSwimTargetSpeedRaw => _topDownSwimTargetSpeedRaw;
	internal int TopDownSwimBurstState => _topDownSwimBurstState;
	internal int TopDownSwimBurstCounter => _topDownSwimBurstCounter;
	internal int TopDownSwimAnimationFrame => _topDownSwimAnimationFrame;
	internal int TopDownSwimAnimationCounter => _topDownSwimAnimationCounter;
	internal bool TopDownDiving => TopDownSwimming && _topDownDiving;
	internal int TopDownDiveCounter => _topDownDiveCounter;
	internal ulong TopDownSwimAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.TopDownSwimTexture.GetImage());
	internal ulong TopDownDiveAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.TopDownDiveTexture.GetImage());
	internal ulong SideScrollSwimAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.SideScrollSwimTexture.GetImage());
	internal ulong SideScrollMermaidAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.SideScrollMermaidTexture.GetImage());
	internal bool SideScrollSquished =>
		_sideScrollSquishPending || _squishAnimation is not null;
	internal bool DelaysOrdinaryScreenTransition =>
		_sideScrollAirborne || _topDownAirborne;
	internal bool RejectsOrdinaryScreenTransition =>
		_enemyGrabUpdated || _enemyKnockbackFrames > 0.0f || _drowning || _fallingInHole || _pullingIntoHole ||
		RespawnWaiting || RespawnRecovering || SideScrollSquished;
	internal bool ScreenTransitionTerrainMotion => _screenTransitionTerrainMotion;
	internal bool IsMovingTowardScreenEdge(Vector2I direction)
	{
		int angle = _world.SideScrolling
			? _sideScrollAngle
			: TopDownSwimming
				? _topDownSwimAngle
				: AngleForVector(_lastMovementInput);
		if (angle >= 0x80)
			return false;
		return direction == Vector2I.Up
			? angle is >= 0x1c or <= 0x04
			: direction == Vector2I.Right
			? angle is >= 0x04 and <= 0x0c
			: direction == Vector2I.Down
			? angle is >= 0x0c and <= 0x14
			: direction == Vector2I.Left &&
				angle is >= 0x14 and <= 0x1c;
	}
	internal bool TopDownAirborne => _topDownAirborne;
	internal int TopDownAirZ => _topDownAirZFixed >> 8;
	internal int SwitchHookZFixed => _topDownAirZFixed;
	internal int ItemCreationZFixed => _topDownAirZFixed;
	internal void ClearSwitchHookZ() { _topDownAirZFixed = 0; QueueRedraw(); }
	internal void SetSwitchHookPosition(Vector2 position, int zFixed)
	{
		_precisePosition = position;
		Position = OracleObjectMath.ToPixelPosition(position);
		_topDownAirZFixed = zFixed;
		QueueRedraw();
	}
	internal int TopDownAirSpeedZ => _topDownAirSpeedZ;
	internal bool MinecartJumpActive => _minecartJumpControlled;
	internal bool MinecartRideActive => _minecartRideControlled;
	internal bool CompanionJumpActive => _companionJumpControlled;
	internal bool CompanionRideActive => _companionRideControlled;
	internal bool RaftRideActive => _raftRideControlled;
	internal Vector2 ActiveLinkObjectPosition => _minecartRideControlled ? _minecartMainObjectPosition : EnemyContactPosition;
	internal bool RaftRespawning => _drowning || _fallingInHole || _forcedRespawnPhase != 0 || ForcedRespawnRequested;
	// func_410d permits the mounted presentation once state02 reaches
	// substate3, although the companion handler still sees state02.
	internal bool RaftRespawnPreventsSynchronization => RaftRespawning &&
		!RespawnRecovering;
	internal ObjectSpriteFrame? CutsceneSpriteFrame => _cutsceneSpriteFrame;
	internal bool RaftMovementImmobilized => _world.MovementDisabled || _braceletActionPose.HasValue ||
		IsUsingBoomerang || IsUsingItem && !SwordAllowsMovement;
	internal Vector2 CompanionRideTextureOffset =>
		_companionRideTextureOffset;
	internal int CompanionRideZFixed => _companionRideZFixed;
	internal Vector2 CompanionRideDrawOffset =>
		_companionRideTextureOffset +
		new Vector2(0, _companionRideZFixed >> 8);
	internal Vector2I CompanionRideTextureSize =>
		_companionRideTexture is null
			? Vector2I.Zero
			: new Vector2I(
				_companionRideTexture.GetWidth(),
				_companionRideTexture.GetHeight());
	internal ulong CompanionRideTexturePixelHash =>
		_companionRideTexture is null
			? 0
			: OracleGraphicsCache.PixelHash(
				_companionRideTexture.GetImage());
	internal bool CompanionJumpReadyToRide =>
		_companionJumpControlled &&
		_topDownAirSpeedZ >= 0 &&
		TopDownAirZ >= -4;
	internal Vector2 MinecartMainObjectPosition =>
		_minecartRideControlled ? _minecartMainObjectPosition : _precisePosition;
	internal int MinecartJumpAngle => _minecartJumpAngle;
	internal bool MinecartJumpReadyToRide =>
		_minecartJumpControlled &&
		_topDownAirSpeedZ >= 0 &&
		TopDownAirZ >= -6;
	internal LedgeJumpState LedgeJumpPhase => _ledgeJumpState;
	internal bool LedgeShadowDrawn =>
		_ledgeZFixed < 0 &&
		_ledgeJumpState is
			LedgeJumpState.Airborne or
			LedgeJumpState.AirborneAfterScroll &&
		(_world.FrameCounter & 1) != 0;
	internal LinkTerrainEffectFrame? CurrentTerrainEffect =>
		GetCurrentTerrainEffect();
	internal bool IsFloorDoorRespawning =>
		_forcedRespawnPhase != 0 || ForcedRespawnRequested;
	internal int FloorDoorRespawnCounter =>
		RespawnWaiting ? NativeLinkCounter : 0;
	internal Vector2 LocalRespawnPosition => _lastSafePosition;
	internal Vector2I LocalRespawnFacingVector =>
		FacingVectorFor(_localRespawnFacing);

	internal void SetLocalRespawnPosition(Vector2 position)
	{
		_lastSafePosition = position;
		_localRespawnFacing = _facing;
	}

	internal void SetLocalRespawnPosition(Vector2 position, Vector2I direction)
	{
		_lastSafePosition = position;
		_localRespawnFacing = direction == Vector2I.Up ? Facing.Up :
			direction == Vector2I.Right ? Facing.Right :
			direction == Vector2I.Down ? Facing.Down : Facing.Left;
	}

	/// <summary>
	/// Writes wLinkLocalRespawnY/X without changing wLinkLocalRespawnDir.
	/// companionRespawn uses this narrower write when its first position is
	/// invalid and it falls back to wLastAnimalMountPointY/X.
	/// </summary>
	internal void SetLocalRespawnCoordinates(Vector2 position) =>
		_lastSafePosition = position;
	internal int NewGameSlowFallFrame => _newGameFallFrame;
	internal int NewGameSlowFallZ => _newGameFallZFixed >> 8;
	internal bool HarpPoseActive => _harpPoseActive;
	internal int HarpPoseFrame => _harpFrame;
	internal bool IsHoldingItemOneHand => _getItemOneHandPose || _scriptedLinkAnimationMode == 0x0e;
	internal bool IsHoldingItemTwoHands => _getItemTwoHandPose;
	internal int? ScriptedLinkAnimationMode => _scriptedLinkAnimationMode;
	internal ulong ScriptedLinkAnimationPixelHash =>
		_scriptedLinkAnimationMode is int mode
			? OracleGraphicsCache.PixelHash(
				ScriptedLinkAnimationTexture(mode, damagePalette: false).GetImage())
			: 0;
	internal bool IsCarryingObject => _carriedObjectPose;
	// bombsBraceletParent writes $41 for a wall grab, $c2 while lifting,
	// and $83 while carrying/pulling a lever; throwing clears the byte.
	internal bool GrabStateActive => _carriedObjectPose ||
		_braceletLiftCollisionsDisabled ||
		_braceletActionPose is BraceletActionPose.Pull or BraceletActionPose.PullStrain;
	internal Vector2 PrecisePosition => _precisePosition;
	internal int CarriedObjectAnimationFrame => GetWalkAnimationFrame();
	internal Vector2I? BraceletEntityOffset { get; private set; }
	internal int BraceletObjectWeight { get; set; }
	internal int ShovelFrame => _shovelFrame;
	internal bool ShovelChildActive => _world.ShovelChildActive;
	internal Vector2 ShovelChildOffset =>
		_linkItems.ShovelOffset((int)_facing);
	internal int ActiveTransformation => _activeTransformation;
	internal int TransformationFrame => _transformationFrame;
	internal int PunchFrame => _punchFrame;
	internal bool IsShieldEquipped => _inventory.ShieldLevel > 0 &&
		(_inventory.EquippedA == TreasureId.Shield ||
		 _inventory.EquippedB == TreasureId.Shield);
	internal int ShieldGraphicsIndex
	{
		get
		{
			if (!IsShieldEquipped)
				return -1;
			int variant = (IsUsingShield ? 2 : 0) +
				(_inventory.ShieldLevel >= 2 ? 1 : 0);
			return _linkItems.Graphic(
				"shield",
				variant,
				GetWalkAnimationFrame(),
				(int)_facing).GraphicsIndex;
		}
	}
	internal Rect2 ShieldCollisionBounds
	{
		get
		{
			Vector2 center = OracleObjectMath.ToPixelPosition(Position) +
				_linkItems.ShieldCenterOffset((int)_facing);
			Vector2 radius =
				_linkItems.ShieldCollisionRadius((int)_facing);
			return new Rect2(center - radius, radius * 2.0f);
		}
	}
	internal ulong ShieldAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.ShieldLinkTexture.GetImage());
	internal ulong AttackAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.AttackTexture.GetImage());
	internal ulong UnderwaterAttackAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.UnderwaterAttackTexture.GetImage());
	internal ulong ShovelAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.ShovelLinkTexture.GetImage());
	internal ulong MinecartLinkAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.MinecartLinkTexture.GetImage());
	internal ulong MinecartAttackAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.MinecartAttackTexture.GetImage());
	internal ulong SwordAtlasPixelHash =>
		OracleGraphicsCache.PixelHash(Sprites.SwordTexture.GetImage());

	public event Action? GameOverRequested;
	internal bool ApplicationUpdateOwned { get; set; }

	private bool _initialPresentationPrepared;

	internal void Initialize(
		IPlayerWorld world,
		InventoryState inventory,
		Vector2 spawn,
		OracleRandom random)
	{
		Facing facing = _facing;
		_world = world;
		_inventory = inventory;
		_facing = facing;
		_random = random;
		foreach (bool _ in PrepareInitialPresentation(inventory.AssetRoot)) { }
		// A later initialization still refreshes presentation as before.
		_initialPresentationPrepared = false;
		EndNewGameSlowFall();
		EndRoomWarpFall();
		_precisePosition = spawn;
		CancelHoleWarp();
		CancelSubrosiaWarp();
		_lastSafePosition = spawn;
		_localRespawnFacing = _facing;
		ResetLinkWalkAnimation();
		Position = OracleObjectMath.ToPixelPosition(spawn);
		_activeTransformation = _ringTransformationRequested = 0;
		_transformationFrame = _transformationTicks = 0;
		QueueRedraw();
	}

	public void WarpTo(
		Vector2 position,
		bool recordSafe = true,
		bool preserveSword = false,
		bool preserveShield = false,
		bool preserveLedgeJump = false,
		bool preserveTopDownSwimming = false,
		bool preserveSideScrollSwimming = false)
	{
		_nativeInitializationPending=false;
		CancelCreditsLink();
		ClearBabyTransformation();
		_activeTransformation = _ringTransformationRequested = 0;
		_transformationFrame = _transformationTicks = 0;
		CancelWallmasterGrab();
		CancelCollapse();
		CancelPalaceToss();
		CancelAmbiUnpossessedCutscene();
		CancelEnemyGrab();
		_screenTransitionTerrainMotion = false;
		if (GaleActive) EndGale();
		if (ElectricShockActive)
		{
			_electricShockPending = false;
			_electricShockCounter = 0;
			_world.UpdateElectricShockPresentation(0);
			Material = null;
		}
		InterruptCarriedItems(discard: true);
		if (!preserveSword)
			CancelSwordAttack();
		CancelShovelAction();
		_shovelChildContactPending = false; // Full room replacement clears physical children.
		EndNewGameSlowFall();
		EndRoomWarpFall();
		EndHarpPose();
		CancelHoleWarp();
		CancelSubrosiaWarp();
		if (!preserveLedgeJump)
			ClearLedgeHop();
		_enemyInvincibilityFrames = 0.0f;
		_enemyKnockbackFrames = 0.0f;
		ClearContactDamage();
		_pendingSwordKnockbackFrames = 0;
		_pendingSwordKnockbackAngle = 0;
		_holePullCounter = 0;
		_holePullPackedPosition = -1;
		_fallInHoleWarpPending = false;
		_pullingIntoHole = false;
		_drowningHazard = HazardType.None;
		_drowning = false;
		_topDownDrownPhase = 0;
		_drownRespawning = false;
		_fallingInHole = false;
		_fallInHoleRespawning = false;
		_forcedRespawnPhase = 0;
		if(ForcedRespawnRequested)_inventory.RuntimeState.SetField("wLinkForceState",0);
		NativeLinkSubstate=0;NativeLinkCounter=0;
		_instantRespawnCollisionDisabled = false;
		_sideScrollSquishPending = false;
		_squishAnimation = null;
		CancelGetItemState();
		_getItemOneHandPose = false;
		_getItemTwoHandPose = false;
		_scriptedLinkAnimationMode = null;
		RetireState08Control(cancelRequest:true);
		_forcedState08LowPriority = false;
		SetAlternateTextboxPalettePriority(false);
		_cutsceneDrawZFixed = 0;
		// Destination room events are initialized while the room's objects
		// are parsed, before the transition places Link. Preserve a frame
		// already selected by that new cutscene owner through WarpTo.
		if (!_cutsceneControlled)
		{
			_cutsceneSpriteFrame = null;
			_cutsceneIdleFacing = null;
		}
		_carriedObjectPose = false;
		BraceletObjectWeight = 0;
		_braceletActionPose = null;
		_braceletLiftCollisionsDisabled = false;
		CancelNativeForcedMovement();
		if (preserveSideScrollSwimming)
		{
			// finishScrollingTransition adjusts Link's coordinate bytes but
			// leaves wLinkSwimmingState and the side-view swim state intact.
			// Keep the retained 8.8 Y coordinate synchronized with the new
			// room position before its first terrain update.
			_sideScrollYFixed = Mathf.FloorToInt(position.Y * 256.0f);
		}
		else
		{
			ClearSideScrollState(position);
		}
		ClearTopDownAirState();
		if (!preserveTopDownSwimming)
			ClearTopDownSwimmingState();
		if (!preserveTopDownSwimming && !preserveSideScrollSwimming && !preserveLedgeJump)
			NativeAdjacentWalls = 0;
		// Full room loading clears Object.angle to $00. Underwater velocity
		// retains that byte until its first convergence or input impulse;
		// ordinary ground movement overwrites it from wLinkAngle instead.
		if (_world.Underwater) _topDownMovementAngle = 0;
		if (preserveShield)
			SuspendShield();
		else
			ClearShieldParent();
		_precisePosition = position;
		if (recordSafe)
			SetLocalRespawnPosition(position);
		Position = OracleObjectMath.ToPixelPosition(position);
		Visible = true;
		QueueRedraw();
	}

	internal void BeginNewGameSlowFall(int initialZ)
	{
		_newGameFallRenderer ??= new CutsceneSpriteRenderer(AssetRoot);
		_newGameFallFrames ??= _linkItems.FallFrames;
		if (_newGameFallFrames.Length != 3)
			throw new InvalidOperationException("Expected three LINK_ANIM_MODE_FALL frames.");

		_newGameFallFrame = 0;
		_newGameFallFrameTicks = _newGameFallFrames[0].Duration;
		_newGameFallZFixed = initialZ << 8;
		_newGameFallSpeedZ = 0;
		_newGameSlowFalling = true;
		_facing = Facing.Down;
		_walking = false;
		Visible = true;
		QueueRedraw();
	}

	internal bool AdvanceNewGameSlowFall()
	{
		if (!_newGameSlowFalling || _newGameFallFrames is null)
			return false;

		// specialObjectAnimate runs before objectUpdateSpeedZ_paramC in
		// TRANSITION_DEST_SLOWFALL ($0b).
		_newGameFallFrameTicks--;
		if (_newGameFallFrameTicks <= 0)
		{
			_newGameFallFrame = (_newGameFallFrame + 1) % _newGameFallFrames.Length;
			_newGameFallFrameTicks = _newGameFallFrames[_newGameFallFrame].Duration;
		}
		if (OracleObjectMath.UpdateSpeedZ(
			ref _newGameFallZFixed,
			ref _newGameFallSpeedZ,
			NewGameSlowFallGravity))
		{
			// Ages warpTransitionB@warpVar1 animates Link standing and
			// requests SND_SPLASH as soon as the slow fall reaches zero.
			EndNewGameSlowFall();
			_world.PlaySound(SoundId.SndSplash);
			return true;
		}

		QueueRedraw();
		return false;
	}

	internal void EndNewGameSlowFall()
	{
		_newGameSlowFalling = false;
		_newGameFallFrame = 0;
		_newGameFallFrameTicks = 0;
		_newGameFallZFixed = 0;
		_newGameFallSpeedZ = 0;
		QueueRedraw();
	}

	/// <summary>
	/// Starts TRANSITION_DEST_FALL ($05). Unlike Roc's Feather, the original
	/// gives Link speedZ=$0020, places him immediately above the gameplay
	/// field, and owns him through the landing/collapsed sequence.
	/// </summary>
	internal void BeginRoomWarpFall(int initialZ)
	{
		_newGameFallRenderer ??= new CutsceneSpriteRenderer(AssetRoot);
		_newGameFallFrames ??= _linkItems.FallFrames;
		if (_newGameFallFrames.Length != 3)
			throw new InvalidOperationException(
				"Expected three LINK_ANIM_MODE_FALL frames.");

		EndNewGameSlowFall();
		_roomWarpFallFrame = 0;
		_roomWarpFallFrameTicks = _newGameFallFrames[0].Duration;
		_roomWarpFallZFixed = initialZ << 8;
		_roomWarpFallSpeedZ = RoomWarpFallInitialSpeedZ;
		_roomWarpFallCollapsedCounter = 0;
		_roomWarpFallActive = true;
		_roomWarpFallCollapsed = false;
		_facing = Facing.Down;
		_walking = false;

		Visible = true;
		QueueRedraw();
	}

	/// <returns>True once the fall and exact 30-update collapsed hold end.</returns>
	internal bool AdvanceRoomWarpFall()
	{
		if (!_roomWarpFallActive || _newGameFallFrames is null)
			return true;

		if (_roomWarpFallCollapsed)
		{
			_roomWarpFallCollapsedCounter--;
			if (_roomWarpFallCollapsedCounter == 0)
			{
				EndRoomWarpFall();
				return true;
			}
			QueueRedraw();
			return false;
		}

		// warpTransition5_01 animates before objectUpdateSpeedZ_paramC.
		_roomWarpFallFrameTicks--;
		if (_roomWarpFallFrameTicks <= 0)
		{
			_roomWarpFallFrame =
				(_roomWarpFallFrame + 1) % _newGameFallFrames.Length;
			_roomWarpFallFrameTicks =
				_newGameFallFrames[_roomWarpFallFrame].Duration;
		}
		if (OracleObjectMath.UpdateSpeedZ(
				ref _roomWarpFallZFixed,
				ref _roomWarpFallSpeedZ,
				RoomWarpFallGravity))
		{
			if (_linkItems.FallReadsLandingTile)
			{
				var terrain = _world.GetTerrainAtPosition(_precisePosition);
				if (terrain.Tile == _linkItems.FallTrampolineTile &&
					(_world.ActiveRoomGroup & _linkItems.FallTrampolineGroupMask) == _linkItems.FallTrampolineGroupValue)
					throw new NotSupportedException("link.s:warpTransition5_01@trampoline requires bounceLinkOffTrampolineAfterFalling, which is not bound.");
				if (terrain.NativeHazardFlags != 0)
				{
					EndRoomWarpFall();
					return true;
				}
			}
			// Clean-US Ages omits the tile read, so A=$00 always misses.
			_roomWarpFallCollapsed = true;
			_roomWarpFallCollapsedCounter = RoomWarpFallCollapsedFrames;
			_world.PlaySound(SoundId.SndSplash);
		}

		QueueRedraw();
		return false;
	}

	internal void EndRoomWarpFall()
	{
		_roomWarpFallFrame = 0;
		_roomWarpFallFrameTicks = 0;
		_roomWarpFallZFixed = 0;
		_roomWarpFallSpeedZ = 0;
		_roomWarpFallCollapsedCounter = 0;
		_roomWarpFallActive = false;
		_roomWarpFallCollapsed = false;
		QueueRedraw();
	}

	internal static int NewGameSlowFallInitialZ(int screenY) =>
		Math.Max(-0x80, -screenY - 8);

	internal static int RoomWarpFallInitialZ(int screenY) =>
		Math.Max(-0x80, -screenY - 8);

	internal static int NewGameSlowFallZForValidation(int screenY, int updates)
	{
		int z = NewGameSlowFallInitialZ(screenY) << 8;
		int speedZ = 0;
		for (int update = 0; update < updates && z < 0; update++)
			OracleObjectMath.UpdateSpeedZ(ref z, ref speedZ, NewGameSlowFallGravity);
		return z >> 8;
	}

	public void BeginScrollingTransition(Vector2 position)
	{
		_precisePosition = position;
		Position = OracleObjectMath.ToPixelPosition(position);
		// transitionUpdateScrollAndLinkPosition changes coordinates only.
		// Facing also survives conveyor/diagonal entries, independently of
		// the direction in which the screen itself scrolls.
		// linkState01 returns before checkUseItems for wScrollMode $08.
		// Its raised Shield byte and parent both survive, including release
		// edges consumed while the scroll handler owns Link.
		_walking = false;
		QueueRedraw();
	}

	public void SetScrollingTransitionPosition(Vector2 logicalPosition, Vector2 screenScroll)
	{
		_precisePosition = logicalPosition;
		Position = OracleObjectMath.ToPixelPosition(logicalPosition - screenScroll);
		QueueRedraw();
	}

	public void FinishScrollingTransition(Vector2 position)
	{
		bool resumeLedgeJump =
			_ledgeJumpState == LedgeJumpState.WaitingForScroll;
		// finishScrollingTransition offsets coordinate high bytes and writes
		// the local respawn. It does not perform a full warp reset: damage
		// invincibility, carried items, swimming and parent-item state survive.
		_precisePosition = position;
		Position = OracleObjectMath.ToPixelPosition(position);
		_sideScrollYFixed = Mathf.FloorToInt(position.Y * 256.0f);
		if (!resumeLedgeJump)
			SetLocalRespawnPosition(position);
		if (resumeLedgeJump)
			_world.ResumeLedgeHopAfterScroll(this);
		_walking = false;
		QueueRedraw();
	}

	public void BeginRoomWarpTransition()
	{
		if (GaleActive) EndGale();
		RetireState08Control(cancelRequest:true);
		_scriptControl = ScriptControl.None;
		_cutsceneControlOwner = null;
		_walking = false;
		ClearShieldParent();
		CancelSwordAttack();
		CancelShovelAction();
		QueueRedraw();
	}

	internal void ResetDeathAnimationForRoomLoad()
	{
		// Full loading clears Link's native object, but retains the global
		// wLinkDeathTrigger ($cdd5), including its consumed slow-fade request.
		ClearContactDamage();
		_deathPending = IsDying;
		_deathAnimationActive = false;
		_deathUpdateAccumulator = 0;
		_deathAnimationFrame = _deathAnimationCounter = 0;
		_deathSequenceIndex = _deathSpinLoopsRemaining = 0;
	}

	public void BeginTimeWarpTransition(Vector2 portalPosition, bool portalContact = true)
	{
		// interactionBeginTimewarp copies the portal position into w1Link and
		// writes DIR_DOWN before disabling Link. Clear pose state at the same
		// handoff so a pushing or item animation cannot survive underneath the
		// time-warp beam.
		Vector2 precisePosition = _precisePosition;
		float invincibility = _enemyInvincibilityFrames;
		int pendingDamage = _pendingContactDamageRaw;
		bool contactSignal = _nativeContactSignal;
		WarpTo(portalPosition, recordSafe: false);
		BeginRoomWarpTransition();
		if (portalContact)
			_facing = Facing.Down;
		else
		{
			// harpFluteParent calls putLinkOnGround, which selects the walk
			// animation without changing direction, XY fractions or damage state.
			_precisePosition = precisePosition;
			_enemyInvincibilityFrames = invincibility;
			_pendingContactDamageRaw = pendingDamage;
			_nativeContactSignal = contactSignal;
		}

		ResetLinkWalkAnimation();
		_lastMovementInput = Vector2.Zero;
		QueueRedraw();
	}

	public void BeginRoomWarpWalk(Vector2 position, Vector2I direction)
	{
		WarpTo(position, recordSafe: false);
		Face(direction);
		_walking = true;
		QueueRedraw();
	}

	public void SetRoomWarpWalkPosition(Vector2 position)
	{
		_precisePosition = position;
		Position = OracleObjectMath.ToPixelPosition(position);
		_walking = true;
		AdvanceLinkWalkAnimation();
		QueueRedraw();
	}

	public void FinishRoomWarpTransition(Vector2 position, bool freshlyLoadedBasicRoom = false)
	{
		WarpTo(position);
		if (freshlyLoadedBasicRoom) _topDownMovementAngle = 0; // clearScreenVariablesAndWramBank1.
		_walking = false;
		QueueRedraw();
	}

	public void TriggerHazard(ActiveTerrainInfo activeTerrain)
	{
		HazardType hazard = activeTerrain.Terrain.Hazard;
		// commonCode.s:@tileType_hole/@tileType_warpHole treat underwater
		// holes as normal floor. The raw WarpHole type still controls B travel.
		if (hazard == HazardType.Hole && _world.Underwater)
		{
			CancelHolePull();
			return;
		}
		if (_pullingIntoHole || _drowning || _fallingInHole)
			return;

		if (hazard == HazardType.Hole)
		{
			StartPullIntoHole(activeTerrain);
			return;
		}

		StartDrowning(hazard);
	}

	internal void ApplyElectricShock(Vector2 sourcePosition)
	{
		// collisionEffect36 writes these counters directly, even when the
		// Green Holy Ring prevents the four-quarter health subtraction.
		_pendingContactDamageRaw = RingEffects.ContactDamageRaw(_inventory, 4, RingDamageSource.Electric);
		_nativeContactSignal = true;
		_enemyInvincibilityFrames = 0x0c;
		_enemyKnockbackFrames = 8;
		_enemyKnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(sourcePosition, Position);
		CancelSwordAttack();
		ClearShieldParent();
		if (!ElectricShockActive) _electricShockPending = true;
	}

	internal void ApplyObjectInteractionGrace()
	{
		RingTransformationSuppression = 8;
		SetNativePushAnimationOverride(0x80);
		var memory = _inventory.RuntimeState;
		memory.SetField("wLinkTurningDisabled", (byte)(memory.ReadField("wLinkTurningDisabled") | 0x80));
		// linkInteractWithAButtonSensitiveObjects preserves the sign and
		// extends either kind of invincibility to at least four updates.
		_enemyInvincibilityFrames = _enemyInvincibilityFrames > 0
			? Mathf.Max(_enemyInvincibilityFrames, 4)
			: Mathf.Min(_enemyInvincibilityFrames, -4);

	}

	public bool Heal(int quarters)
	{
		if (quarters <= 0)
			return false;

		return _inventory.Heal(quarters);
	}

	public void RefillHealth()
	{
		_inventory.RefillHealth();
	}

	public void AddRupees(int amount)
	{
		_inventory.AddRupees(amount);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!ApplicationUpdateOwned)
			AdvancePhysics(delta);
	}

	internal void AdvanceApplicationUpdate(bool advanceElectricShock = true)
	{
		if (IsPhysicsProcessing())
			AdvancePhysics(ApplicationFixedUpdateScheduler.UpdateDelta, advanceElectricShock);
		if (IsProcessing())
			// Input already ran the new parent's state 0 during physics.
			// Its animation advances on the next parent pass; the child's
			// initialization and post-object collision still run this update.
			AdvanceItems(ApplicationFixedUpdateScheduler.UpdateDelta,
				swordInitialized: Started(TreasureId.Sword, IsAttacking));
	}

	internal void AdvanceElectricShock()
	{
		if (!ElectricShockActive) return;
		if (_electricShockPending)
		{
			_electricShockPending = false;
			_electricShockCounter = 0x2d;
			_world.PlaySound(SoundId.SndShock);
		}
		else
		{
			_electricShockCounter--;
		}
		_world.UpdateElectricShockPresentation(_electricShockCounter);
		if ((_electricShockCounter & 7) == 0)
		{
			Material = (_electricShockCounter & 8) != 0
				? _electricShockMaterial ??= ElectricShockPalette.For(AssetRoot).CreateLinkMaterial()
				: null;
			QueueRedraw();
		}
	}

	private void AdvancePhysics(double delta, bool advanceElectricShock = true)
	{

		if (!ApplicationUpdateOwned) AdvanceDeathPrelude();
		_linkPhysicsActive = true;
		_linkPhysicsDelta = delta;
		_lowerParentsUpdatedInPhysics = false;
		try { AdvanceLinkPhysics(delta, advanceElectricShock); }
		finally
		{
			// updateSpecialObjects clears var2a after every Link dispatch,
			// including a handler frozen before damage consumption.
			_nativeContactSignal = false;
			var memory = _inventory.RuntimeState;
			memory.SetField("wLinkImmobilized",(byte)(memory.ReadField("wLinkImmobilized") & 0x0f));
			_linkPhysicsActive = false;
		}
	}

	private void AdvanceLinkPhysics(double delta, bool advanceElectricShock)
	{
		_ringUpdatedInPhysics = false;
		if (AdvanceSubrosianDance()) return;
		if(AdvanceCreditsLink())return;
		if(_nativeInitializationPending&&!_world.LinkDisabled)
		{
			_nativeInitializationPending=false;ClearNativeItemParents();Visible=true;return;
		}
		RetireSupersededNativeRequests();
		ResetBabyForForcedState();
		if (InitializeBabyOverride()) return;
		if (InitializeRingOverride()) return;
		// GameRoot advances cutscene01's shock phase before menus. Standalone
		// player updates retain that phase without a second counter owner.
		if (advanceElectricShock) AdvanceElectricShock();
		_enemyGrabUpdated = false;
		_collapseUpdated = false;
		_itemAllocationBlockedThisUpdate = false;
		// updateSpecialObjects clears wLinkClimbingVine before Link,
		// including updates where text or object masks freeze state01.
		SideScrollClimbing = false;
		_swordUpdatedInPhysics = false;
		_ledgeUpdatedInPhysics = false;
		_swordJinxUpdatedInPhysics = false;
		// updateSpecialObjects clears wcc92 before this update's terrain
		// handler can publish a conveyor/current displacement.
		_screenTransitionTerrainMotion = false;
		// linkState01 returns on wScrollMode & $0e before damage and item
		// parents. A selected edge remains $04 while a toggle owns cutscene02.
		if(AdvanceNativeRespawnState())return;
		if (AdvanceNativeForcedMovement()) return;
		if(_forcedState08Phase>=2){AdvanceState08Control();return;}
		if (_world.RoomExitPending)
		{
			if (!_world.LinkDisabled) SetNativePushAnimationOverride(0x80);
			return;
		}
		if (AdvanceAmbiUnpossessedCutscene()) return;
		if ((_palaceTossRequested || _palaceTossSubstate >= 0) && AdvancePalaceToss()) return;
		if ((CollapsedActive || CollapsePending && !_world.IsTransitioning && !IsDying) && AdvanceCollapse()) return;
		if (ConsumeContactDamageBeforeStateDispatch()) return;
		if (AdvanceRingLink()) return;
		if (AdvanceBabyLink()) return;
		// updateSpecialObjects clears wForceLinkPushAnimation even when
		// DISABLE_LINK freezes Link's native state and item parents.
		// linkState01 checks forced state BEFORE text/$81 restrictions;
		// linkState0d itself has no such gate. Only an unconsumed request
		// still waits for the normal state's scrolling/palette gate.
		if ((WallmasterGrabActive || _wallmasterGrabRequested && !_world.IsTransitioning && !IsDying) && AdvanceWallmasterGrab())
			return;
		if ((EnemyGrabActive || _enemyGrabRequested && !_world.IsTransitioning && !IsDying) && AdvanceEnemyGrab())
			return;
		if (_squishAnimation is not null || _sideScrollSquishPending && !_world.IsTransitioning && !IsDying)
		{
			AdvanceSideScrollSquish();
			return;
		}
		if(_topDownDrownPhase==1 && _forcedRespawnPhase!=2 && (!ForcedRespawnRequested || _inventory.RuntimeState.ReadField("wLinkStateParameter")!=4))
		{
			_topDownDrownPhase=0;_drowning=false;_drowningHazard=HazardType.None;
		}
		if (AdvanceGetItemState()) return;
		if (AdvanceSubrosiaWarp()) return;
		if (AdvanceHoleWarp()) return;
		// state01 checks the shared request after palette/scroll/death, before text/$81.
		if (_inventory.RuntimeState.ReadField("wLinkForceState")!=0 && !IsDying &&
			(_raftRideControlled || NativeCutsceneControlled || !_companionRideControlled && NativeNormalStateForInteraction))
		{
			if(_world.IsTransitioning || _world.NativePaletteChanging ||
				_inventory.RuntimeState.ReadField("wPaletteThread_mode")!=0 ||
				(_inventory.RuntimeState.ReadField("wScrollMode")&0x0e)!=0)return;
			ConsumeNativeForcedState();
			return;
		}
		if ((_world.PlayerUpdatesFrozen || _world.LinkDisabled) && _forcedRespawnPhase != 3 && !IsDying)
		{
			QueueRedraw();
			return;
		}
		if (GaleActive)
		{
			AdvanceGale();
			return;
		}
		if (ElectricShockActive && _forcedRespawnPhase != 3)
			return;

		if (IsDying)
		{
			// Direct fades reach cutscene03 without asking state03 to consume
			// wLinkForceState. Destination state01 waits for the palette.
			// A merely requested explicit warp does not suspend death.
			if (_world.DeathUpdatesSuspendedByWarp)
				return;
			// The later physical item pass keeps a released Bracelet child
			// falling after Link's cancellation/held-item drop.
			UpdateDeath(delta);
			// LINK_STATE_DYING does not bypass cutscene01's post-object warp
			// check. Its remaining recoil can still carry Link onto a stair.
			if (!_topDownAirborne) _world.CheckTileWarp(this);
			return;
		}
		if (_cutsceneControlled)
			return;
		_startedParentItemAnimations = 0;
		if (_world.SwitchHookExchangeActive)
		{
			AdvanceSwordJinxForItemUse();
			_world.Pegasus?.AdvanceCounter();
			_world.UpdateSwitchHookParent(this);
			AdvanceSwitchHookAirState();
			if (!_topDownAirborne) _world.CheckTileWarp(this);
			_walking = false;
			QueueRedraw();
			return;
		}
		if (_drowning)
		{
			UpdateDrowning();
			return;
		}

		if (_fallingInHole)
		{
			UpdateFallInHole();
			return;
		}

		if (_ledgeJumpState != LedgeJumpState.None)
		{
			_ledgeUpdatedInPhysics = true;
			UpdateLedgeHop(delta);
			return;
		}

		// linkState01 decrements before item use and knockback. Text,
		// scrolling, forced states, and disabled Link do not reach this call.
		if (!_world.IsTransitioning && !_world.DialogueOpen)
			_world.Pegasus?.AdvanceCounter();
		_world.UpdateSwitchHookParent(this);
		if (!_world.IsTransitioning && !_world.DialogueOpen && !_world.ItemUsageDisabled && !_companionRideControlled)
		{
			// linkState01 returns before checkUseItems while text is active.
			// Both throw/swing parents retain their animation and terminal
			// marker, so Shield cannot resume early after that pause.
			_world.UpdateSomariaParent();
		}
		AdvanceNormalLinkMovement(delta);
	}

	private void AdvanceNormalLinkMovement(double delta)
	{
		SetNativePushAnimationOverride(0x80);
		if (_minecartJumpControlled)
		{
			// linkState01's text/palette gate precedes both checkUseItems and
			// linkUpdateInAir, including the locked boarding/dismount jump.
			if (_world.DialogueOpen || _world.IsTransitioning) return;
			SetNativePushAnimationOverride(0);
			_lastMovementInput = Input.GetVector(
				"move_left", "move_right", "move_up", "move_down");
			AdvanceSwordParentBeforeMovement(_lastMovementInput);
			// checkUseItems still updates existing parents with wLinkInAir
			// bit7 set; only fresh allocation is suppressed. The cart clears
			// var3f, preserving the Shooter's aim/release/post-shot state.
			if (_world.SeedShooterActive)
				_world.UpdateSeedShooter(this, _lastMovementInput,
					Input.IsActionPressed("attack"), Input.IsActionPressed("item"),
					DirectionalInputJustPressed());
			if (_world.UpdateBoomerangParent(this)) _lastMovementInput = Vector2.Zero;
			AdvanceMinecartJumpUpdate();
			return;
		}
		if (_companionJumpControlled)
		{
			if (!_world.DialogueOpen && !_world.IsTransitioning) SetNativePushAnimationOverride(0);
			if (!AdvanceCompanionJumpUpdate())
				return;
		}

		if (_world.IsTransitioning)
			return;

		if (_world.DialogueOpen)
		{
			// linkState01 returns before checkUseItems: the shield parent
			// and wUsingShield retain their preceding update during text.
			_walking = false;
			QueueRedraw();
			return;
		}

		Vector2 movementStart = _precisePosition;
		Vector2 input = Input.GetVector(
			"move_left", "move_right", "move_up", "move_down");
		if ((_raftRideControlled || _world.SideScrolling) &&
			(Input.IsActionPressed("move_left") && Input.IsActionPressed("move_right") ||
			 Input.IsActionPressed("move_up") && Input.IsActionPressed("move_down")))
			input = Vector2.Zero;
		if (_world.MovementDisabled &&
			!_minecartRideControlled && !_companionRideControlled &&
			!_raftRideControlled)
			input = Vector2.Zero;
		_lastMovementInput = input;

		if (_world.SideScrolling && SideScrollSwimming)
		{
			// checkUseItems updates the existing parent before swimming state
			// 2 reads wLinkImmobilized. The terminal sword update therefore
			// releases movement immediately; a newly initialized parent keeps
			// its first graphic for all three source updates.
			AdvanceSwordParentBeforeMovement(input);
		}

		if (_companionRideControlled)
		{
			// SPECIALOBJECT_LINK_RIDING_ANIMAL owns Link's update while
			// wLinkObjectIndex points at the companion. The companion reads
			// A/B itself; ordinary equipped parent items must not turn those
			// presses into Link sword, feather, shield, or other item poses.
			_walking = false;

			Position = OracleObjectMath.ToPixelPosition(_precisePosition);
			_world.CheckRoomExit(this);
			QueueRedraw();
			return;
		}

		// linkState01 checks A-sensitive objects/tiles before applying tile
		// types and checkAndUpdateLinkOnChest, then enters checkUseItems.
		// Its callers skip both interactions while air, knockback or the
		// preceding instrument publication owns Link's update.
		bool frontInteractionsEligible = !_minecartRideControlled && !_raftRideControlled &&
			!_topDownAirborne && !_sideScrollAirborne && _enemyKnockbackFrames == 0 &&
			_world.PlayingInstrument == 0;
		if (frontInteractionsEligible && _activeTransformation == 0 && Input.IsActionJustPressed("attack") &&
			(!GrabStateActive || _world.InShop) &&
			!_world.SwordDisabled && (!IsUsingItem || IsUsingSomaria) &&
			!IsUsingSomaria && !_minecartRideControlled && !_raftRideControlled &&
			_world.TryInteract(this))
		{
			// Chest/sign handlers return carry before ordinary Link motion.
			// Retained wall probes must not publish a new push on that return.
			SetNativePushAnimationOverride(0x80);
			return;
		}

		// linkState01 dispatches the front tile before terrain, swimming
		// and item parents. Instrument/air/knockback/grab bypass the handler
		// entirely, retaining its shared countdown until a later eligible pass.
		if (frontInteractionsEligible && !GrabStateActive)
		{
			if (_world.UpdatePushableBlocks(_precisePosition, FacingVector, input))
			{
				SetNativePushAnimationOverride(0x80);
				return;
			}
		}

		SetNativePushAnimationOverride(0);
		// linkState01 clears the preceding instrument publication after front-tile interactions.
		_inventory.RuntimeState.SetField("wLinkPlayingInstrument",0);
		ActiveTerrainInfo? terrainBeforeMotion = null;
		if (!_world.SideScrolling)
		{
			terrainBeforeMotion = ApplyTopDownTileTypes();
			_world.UpdateLinkOnChest();
			Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		}
		else _world.UpdateSideScrollActiveTile(_precisePosition);
		// Native checkUseItems precedes air/swimming dispatch. Some of those
		// paths return before the ordinary item-input convenience method.
		AdvanceSwordJinxForItemUse();
		if (_enemyKnockbackFrames > 0)
		{
			bool recoilItemImmobilized = !TopDownSwimming && ProcessItemInput(ref input,checkTileWarps:false);
			if (!_world.SideScrolling && _world.PlayingInstrument == 0)
				NativeAdjacentWalls = _world.GetAdjacentWallsBitset(_precisePosition);
			AdvanceKnockbackAfterItemParents(delta,recoilItemImmobilized,movementStart);
			return;
		}
		if (!_world.SideScrolling && !_topDownAirborne && TopDownAirZ < 0)
		{
			// linkUpdateInAir's @notInAir branch adopts negative object Z.
			// An item may leave that Z behind after clearing its parent in
			// the preceding post pass; swimming must not bypass gravity.
			_topDownAirborne = true;
			UpdateTopDownAirMovement(delta, input, movementAllowed: !IsUsingItem, movementStart);
			return;
		}
		if (!_world.SideScrolling &&
			TryAdvanceTopDownSwimming(
				input,
				_topDownMovementAngle,
				movementStart,
				Input.IsActionJustPressed("attack"),
				Input.IsActionJustPressed("item"),
				terrainBeforeMotion))
		{
			return;
		}

		bool itemImmobilized = ProcessItemInput(ref input);
		AdvanceRocsCapeParent();
		// linkState01 reads the parent's instrument publication immediately
		// after checkUseItems, before movement/standing can emit Pegasus dust.
		// The empty Harp's $00 byte still reaches that movement phase.
		if (_world.PlayingInstrument != 0) return;
		if (_enemyKnockbackFrames > 0)
		{
			NativeAdjacentWalls = _world.GetAdjacentWallsBitset(_precisePosition);
			AdvanceKnockbackAfterItemParents(delta, itemImmobilized, movementStart);
			return;
		}
		if (itemImmobilized)
		{
			// linkState01@notInAir dispatches swimming before testing the
			// movement lock. Side-view state2 skips displacement while an
			// aiming Shooter/Hook owns Link, but still ticks counter2/bubbles.
			if (_world.SideScrolling && SideScrollSwimming)
			{
				UpdateSideScrollMovement(delta, input, movementAllowed: false);
				Position = OracleObjectMath.ToPixelPosition(_precisePosition);
				QueueRedraw();
			}
			// linkState01 publishes adjacent walls after checkUseItems even
			// when a parent immobilizes Link. Later interaction movement
			// (including lever pulling) consumes these retained probes.
			NativeAdjacentWalls = _world.GetAdjacentWallsBitset(_precisePosition);
			RestoreNormalGroundFlags();
			if (!_topDownAirborne && TryRequestRingTransformation()) return;
			return;
		}
		AdvanceSwordParentBeforeMovement(input);
		AdvanceMovementAfterItemParents(delta, input, movementStart);
	}

	private void AdvanceMovementAfterItemParents(
		double delta, Vector2 input, Vector2 movementStart)
	{
		NativeAdjacentWalls = _world.GetAdjacentWallsBitset(_precisePosition);

		if (_minecartRideControlled)
		{
			// linkState01 still runs checkUseItems and @updateDirection while
			// wLinkObjectIndex points at SPECIALOBJECT_MINECART. The companion
			// copies its position over Link later in the update, so input
			// changes Link's facing without moving him independently.
			UpdateMinecartRideDirection(input);
			_walking = false;

			Position = OracleObjectMath.ToPixelPosition(_precisePosition);
			_world.CheckRoomExit(this);
			QueueRedraw();
			return;
		}
		if (_raftRideControlled)
		{
			if (_topDownAirborne)
			{
				// The final falling update can allocate the raft interaction.
				// Companion state0 does not cancel Link's remaining gravity;
				// the following Link dispatch still owns landing and sound.
				UpdateTopDownAirMovement(delta, input, movementAllowed: !IsUsingItem || SwordAllowsMovement, movementStart);
				return;
			}
			// Ages' mounted branch goes directly to @updateDirection,
			// bypassing the item parent's turning-disable byte.
			if (input.LengthSquared() > 0.01f)
				UpdateFacing(input);
			_walking = false;

			Position = OracleObjectMath.ToPixelPosition(_precisePosition);
			_world.CheckRoomExit(this);
			QueueRedraw();
			return;
		}
		// linkState01 checks underwater B travel after item parents and the
		// grounded/swimming/vehicle gates, before velocity or facing changes.
		if (!_world.SideScrolling && !_topDownAirborne && _world.Underwater &&
			Input.IsActionJustPressed("item") &&
			_world.RequestUnderwaterSurface(this, _world.GetActiveTerrain(Position)))
		{
			QueueRedraw();
			return;
		}
		// checkUseItems may immobilize even a failed Bombchu allocation, but
		// linkState01 still reaches linkUpdateInAir. Apply that first-update
		// lock to ground/side-view input without bypassing gravity or landing.
		bool movementAllowed = !_bombchuParentInitializedThisUpdate &&
			!IsUsingBoomerang && (!IsUsingItem || SwordAllowsMovement);
		if (_world.SideScrolling)
		{
			UpdateSideScrollMovement(delta, input, movementAllowed);


			Position = OracleObjectMath.ToPixelPosition(_precisePosition);
			if (!_world.CheckTileWarp(this))
				_world.CheckRoomExit(this);
			AdvanceTransformationAnimation(_walking);
			QueueRedraw();
			return;
		}

		if (!_topDownAirborne && movementAllowed && _world.TryStartTerrainJump(this,_precisePosition,input,_topDownMovementAngle) &&
			_ledgeJumpState != LedgeJumpState.None)
		{
			QueueRedraw();
			return;
		}
		if (_topDownAirborne)
		{
			UpdateTopDownAirMovement(
				delta, input, movementAllowed, movementStart);
			return;
		}

		RestoreNormalGroundFlags();
		if (TryRequestRingTransformation()) return;
		_walking = AdvanceTopDownInputMovement(input, movementAllowed);
		if (_ledgeJumpState != LedgeJumpState.None)
		{
			QueueRedraw();
			return;
		}
		if (_walking)
		{
			AdvanceLinkWalkAnimation();
		}
		else
		{
			ResetLinkWalkAnimation();
		}



		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		if (!_world.CheckTileWarp(this))
			_world.CheckRoomExit(this);
		if (!_world.IsTransitioning)
			ApplyTerrainAtFeet();
		AdvanceTransformationAnimation(_walking);
		QueueRedraw();
	}

	private void UpdateDeath(double delta)
	{
		if (delta < 0.0)
			throw new ArgumentOutOfRangeException(nameof(delta));

		_deathUpdateAccumulator += delta * 60.0;
		while (_deathUpdateAccumulator + 0.000001 >= 1.0 &&
			!_gameOverRequested)
		{
			_deathUpdateAccumulator -= 1.0;
			AdvanceDeathUpdate();
		}
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void AdvanceDeathPrelude()
	{
		if (IsDying && !_deathSlowFadeRequested)
		{
			// standardGameState replaces wLinkDeathTrigger $ff with $e7 and
			// starts the slow fade before the following object pass. A death
			// published during Link's pass cannot reach it until next update.
			_deathSlowFadeRequested = true;
			_world.PlaySound(SoundId.SndCtrlSlowFadeOut);
		}
	}

	private void AdvanceDeathUpdate()
	{
		if (_deathPending)
		{
			// LINK_STATE_DYING substate 0 retains ordinary knockback until its
			// counter reaches zero. The spin begins on the following update.
			if (_enemyKnockbackFrames > 0.0f)
			{
				ClearShieldParent();
				// linkState03 substate0 refreshes walls and jumps to
				// linkUpdateKnockback, including native scratch and sliding.
				_precisePosition += _world.ResolveNativeMovement(
					_precisePosition, 0x32,
					_enemyKnockbackAngle, allowWallSlide: true);
				_enemyKnockbackFrames = Mathf.Max(
					0.0f, _enemyKnockbackFrames - 1.0f);
				_walking = false;

				CancelSwordAttack();
				CancelShovelAction();
				return;
			}

			StartDeathAnimation();
			return;
		}

		if (!_deathAnimationActive)
			return;

		_enemyInvincibilityFrames = 0.0f;
		_deathAnimationCounter--;
		if (_deathAnimationCounter != 0)
			return;

		switch (_deathSequenceIndex)
		{
			case 0:
				SetDeathFrame(sequenceIndex: 1, frame: 1, duration: 8);
				break;
			case 1:
				SetDeathFrame(sequenceIndex: 2, frame: 0, duration: 8);
				break;
			case 2:
				SetDeathFrame(sequenceIndex: 3, frame: 3, duration: 8);
				break;
			case 3:
				// animationData19e7b holds graphics $02 for seven ordinary
				// updates before its visually identical one-update marker.
				SetDeathFrame(sequenceIndex: 4, frame: 2, duration: 7);
				break;
			case 4:
				_deathSpinLoopsRemaining--;
				if (_deathSpinLoopsRemaining == 0)
				{
					// LINK_ANIM_MODE_COLLAPSED holds gfx frame $04 for $4c
					// updates before its terminal $ff parameter.
					SetDeathFrame(
						sequenceIndex: 6,
						frame: 4,
						duration: DeathCollapsedFrames);
				}
				else
				{
					// The one-update $80 marker reuses spin frame $02.
					SetDeathFrame(sequenceIndex: 5, frame: 2, duration: 1);
				}
				break;
			case 5:
				// animationData19e7b loops to frame $01, not its initial
				// eight-update frame $02.
				SetDeathFrame(sequenceIndex: 1, frame: 1, duration: 8);
				break;
			case 6:
				_gameOverRequested = true;
				GameOverRequested?.Invoke();
				break;
			default:
				throw new InvalidOperationException(
					$"Invalid Link death animation sequence {_deathSequenceIndex}.");
		}
	}

	private void StartDeathAnimation()
	{
		_deathPending = false;
		_deathAnimationActive = true;
		// state03 substate0 keeps invincibility through spin initialization.
		// resetLinkInvincibility belongs to substate1 on the next update.
		_walking = false;

		InterruptCarriedItems(discard: true);
		ClearShieldParent();
		CancelSwordAttack();
		CancelShovelAction();
		_deathSpinLoopsRemaining = 4;
		SetDeathFrame(sequenceIndex: 0, frame: 2, duration: 8);
		_world.PlaySound(SoundId.SndLinkDead);
	}

	private void SetDeathFrame(int sequenceIndex, int frame, int duration)
	{
		_deathSequenceIndex = sequenceIndex;
		_deathAnimationFrame = frame;
		_deathAnimationCounter = duration;
	}

	internal void BeginCutsceneControl(bool interruptBracelet = true, object? owner = null)
	{
		RetireState08Control(cancelRequest:true);
		_forcedState08LowPriority = false;
		if (interruptBracelet)
			InterruptCarriedItems(discard: true);
		_scriptControl = ScriptControl.Cutscene;
		_cutsceneControlOwner = owner;
		ClearShieldParent();
		_walking = false;

		CancelSwordAttack();
		CancelShovelAction();
		QueueRedraw();
	}

	internal bool CutsceneControlled => _cutsceneControlled;
	internal bool NativeCutsceneControlled => _scriptControl == ScriptControl.Cutscene;
	internal void BeginSimulatedInputControl(object owner)
	{
		// wUseSimulatedInput changes the game-key source, retaining Link's
		// normal state and item parents. It is not SPECIALOBJECT_LINK_CUTSCENE.
		_scriptControl = ScriptControl.SimulatedInput;
		_cutsceneControlOwner = owner;
	}
	internal void ClearNativePegasusCounter()=>_world.Pegasus?.Clear();
	internal void RequestState08Control(object owner, byte parameter = 0)
	{
		// setLinkForceStateToState08_withParam queues state08/wcc50. Link
		// adopts it on his next dispatch and runs substate0 one update later.
		_inventory.RuntimeState.SetField("wcc50", parameter);
		_inventory.RuntimeState.SetField("wLinkForceState",8);
		_scriptControl = ScriptControl.ForcedState08;
		_cutsceneControlOwner = owner;
		_forcedState08LowPriority = true; // Portal writes visible=$82 immediately.
		SetAlternateTextboxPalettePriority(false);
		QueueRedraw();
	}
	internal void DropHeldItemsForScript() => InterruptCarriedItems(discard: false);
	internal bool IsCutsceneControlOwner(object owner) =>
		_cutsceneControlled && ReferenceEquals(_cutsceneControlOwner, owner);
	internal bool Walking => _walking;

	internal void PutOnGroundForScript()
	{
		// bank0.s:putLinkOnGround leaves a mounted special object untouched.
		if (_companionRideControlled || _minecartRideControlled || _raftRideControlled)
			return;
		ClearTopDownAirState();
		ClearLedgeHop();
		QueueRedraw();
	}

	internal void BeginGetItemOneHandPose()
	{
		_getItemOneHandPose = true;
		_walking = false;

		CancelSwordAttack();
		CancelShovelAction();
		QueueRedraw();
	}

	internal void EndGetItemOneHandPose()
	{
		if (!_getItemOneHandPose)
			return;
		_getItemOneHandPose = false;
		QueueRedraw();
	}

	internal void BeginGetItemTwoHandPose()
	{
		_getItemTwoHandPose = true;
		_walking = false;

		CancelSwordAttack();
		CancelShovelAction();
		QueueRedraw();
	}

	internal void EndGetItemTwoHandPose()
	{
		if (!_getItemTwoHandPose)
			return;
		_getItemTwoHandPose = false;
		QueueRedraw();
	}

	/// <summary>
	/// Applies the static Link animation modes written by
	/// boy_runFunnyJokeCutscene. These are direct wcc50 writes, independent
	/// of the ordinary held-item pose flags.
	/// </summary>
	internal void SetScriptedLinkAnimationMode(int? mode)
	{
		if (mode is not null and not (0x02 or 0x06 or 0x07 or 0x08 or 0x09 or
			  0x0a or 0x0d or 0x0e or 0x0f or 0x10 or 0x18 or 0x1c))
		{
			throw new NotSupportedException($"link.s: scripted Link animation ${mode:x2} requires its native presenter.");
		}
		if (mode is int animation)
			LinkNativeAnimationDatabase.For(AssetRoot).Set(_inventory.RuntimeState, animation);
		_scriptedLinkAnimationMode = mode;
		_walking = false;

		QueueRedraw();
	}

	/// <summary>
	/// Applies a source fixed-point SpecialObject.z offset without moving
	/// Link's room-space collision position.
	/// </summary>
	internal void SetCutsceneDrawZFixed(int zFixed)
	{
		// These script callbacks write SpecialObject.z, not merely a camera
		// offset. Keep the gameplay owner current even for a grounded pose.
		_topDownAirZFixed = zFixed;
		if (_cutsceneDrawZFixed == zFixed)
			return;
		_cutsceneDrawZFixed = zFixed;
		QueueRedraw();
	}

	internal void SetCutsceneSpriteFrame(ObjectSpriteFrame? frame)
	{
		if (frame is not null) _cutsceneSpriteRenderer ??= new CutsceneSpriteRenderer(AssetRoot);
		_cutsceneSpriteFrame = frame;
		QueueRedraw();
	}
	internal System.Collections.Generic.IEnumerable<bool> PrepareCutscenePresentation(
		System.Collections.Generic.IEnumerable<ObjectSpriteFrame> frames, string assetRoot)
	{
		_cutsceneSpriteRenderer ??= new CutsceneSpriteRenderer(assetRoot);
		foreach (var frame in frames)
		{
			foreach (ObjectSpritePart part in frame.Parts) _ = _cutsceneSpriteRenderer.CellTexture(frame,part);
			yield return false;
		}
	}

	internal void SetCutsceneIdleAnimation(int direction)
	{
		if (direction is < 0 or > 3)
			throw new ArgumentOutOfRangeException(nameof(direction));
		// specialObjectSetAnimation selects a pose without writing direction.
		_cutsceneIdleFacing = (Facing)direction;
		_walking = false;

		QueueRedraw();
	}

	internal void BeginCarriedObjectPose()
	{
		ParentItemAnimationState.SetPriority(_inventory.RuntimeState, 2, TreasureId.Bombs, 0);
		ParentItemAnimationState.SetPriority(_inventory.RuntimeState, 2, TreasureId.Bracelet, 0);
		_carriedObjectPose = true;

		QueueRedraw();
	}

	internal void BeginSwitchHookPose()
	{
		// The maximum-priority hook parent excludes the later shield parent.
		ClearShieldParent();
		_walking = false;

		QueueRedraw();
	}

	internal void SetBraceletActionPose(BraceletActionPose pose)
	{
		_braceletActionPose = pose;
		_walking = false;

		CancelSwordAttack();
		CancelShovelAction();
		int parent = _inventory.RuntimeState.ReadWramByte(0xd201);
		if (parent is TreasureId.Bombs or TreasureId.Bracelet)
		{
			int graphic = pose == BraceletActionPose.LiftHigh ? 0x5c :
				_linkItems.Graphic("bracelet", (int)pose, 0, (int)_facing).GraphicsIndex - (int)_facing;
			_inventory.RuntimeState.SetWramByte(0xd231, (byte)graphic);
		}
		if (pose == BraceletActionPose.Throw)
		{
			ParentItemAnimationState.SetPriority(_inventory.RuntimeState, 2, TreasureId.Bombs, 0x0f);
			ParentItemAnimationState.SetPriority(_inventory.RuntimeState, 2, TreasureId.Bracelet, 0x0f);
		}
		QueueRedraw();
	}

	internal void SetBraceletEntityOffset(Vector2I? offset) =>
		BraceletEntityOffset = offset;

	internal void SetBraceletLiftCollisionsDisabled(bool disabled) =>
		_braceletLiftCollisionsDisabled = disabled;

	internal void ClearBraceletActionPose()
	{
		if (!_braceletActionPose.HasValue)
			return;
		_braceletActionPose = null;
		QueueRedraw();
	}

	internal void EndCarriedObjectPose()
	{
		if (!_carriedObjectPose)
			return;
		_carriedObjectPose = false;
		BraceletObjectWeight = 0;
		QueueRedraw();
	}

	internal void AdvanceCutsceneInput(Vector2I direction)
	{
		if (!_cutsceneControlled)
			return;
		_walking = direction != Vector2I.Zero;
		if (_walking)
		{
			// These callers emulate wSimulatedInput. Preserve the direction
			// as Link's current angle so screenTransitionState2 can authorize
			// the matching edge transition on the same update.
			_lastMovementInput = direction;
			Face(direction);
			TryMove((Vector2)direction, allowWallSlide: false);
			AdvanceLinkWalkAnimation();
		}
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void AdvanceCutsceneSimulatedInput(
		Vector2I direction,
		int angle,
		int normalSpeed,
		int slowSpeed)
	{
		if (!_cutsceneControlled || _world.NativeTextActive || _world.PlayerUpdatesFrozen || _world.NativePaletteChanging)
			return;

		_walking = direction != Vector2I.Zero;
		if (_walking)
		{
			// wSimulatedInput follows Link's ordinary top-down movement path:
			// objectApplySpeed supplies the exact 8.8 vector, while Link's
			// terrain handler selects SPEED_c0 on grass/puddles and SPEED_080
			// on stairs/vines. It also writes Link's angle, which
			// screenTransitionState2 converts back to direction buttons at a
			// room edge; retaining an earlier live-input vector here made
			// scripted room exits depend on whichever validation ran first.
			int speed = GetCutsceneSimulatedInputSpeed(normalSpeed, slowSpeed);
			_lastMovementInput = OracleObjectMovement.Shared.Direction(angle);
			Face(direction);
			ApplyTopDownObjectSpeed(speed, angle, allowWallSlide: true);
			AdvanceLinkWalkAnimation();
		}
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal int NativePlanarSpeed => _topDownMovementSpeedRaw;
	internal int NativePlanarAngle => _topDownMovementAngle;

	internal void SetNativeCutsceneAngle(int angle)
	{
		_topDownMovementAngle = angle;
		Vector2 direction = OracleObjectMath.StrictCardinalVector(angle);
		Face(new Vector2I((int)direction.X, (int)direction.Y));
	}

	internal void SetNativeCutsceneSpeed(int speed) => _topDownMovementSpeedRaw = speed;

	internal void SetNativeCutsceneCoordinates(byte y, byte x)
	{
		// Native yh/xh assignments retain the object's fractional bytes and
		// incoming motion registers; they are not room-warp initialization.
		OracleObjectPosition current = OracleObjectPosition.FromPixels(_precisePosition);
		_precisePosition = new OracleObjectPosition(
			(ushort)((y << 8) | (current.YFixed & 0xff)),
			(ushort)((x << 8) | (current.XFixed & 0xff))).PrecisePosition;
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void AdvanceNativeCutsceneMovement(int speed, int angle)
	{
		if (!_cutsceneControlled) return;
		_topDownMovementSpeedRaw = speed;
		_topDownMovementAngle = angle;
		_walking = speed != 0 && (angle & 0x80) == 0;
		// SPECIALOBJECT_LINK_CUTSCENE calls objectApplySpeed without terrain
		// collision. The player world owns WRAM publication and word wrapping.
		AdvanceLinkWalkAnimation();
		_precisePosition = _world.ApplyNativeUnblockedMovement(_precisePosition, speed, angle);
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void AdvanceCutsceneMovement(Vector2 movement, Vector2I direction)
	{
		if (!_cutsceneControlled)
			return;
		_walking = movement != Vector2.Zero;
		if (direction != Vector2I.Zero)
			Face(direction);
		_precisePosition += movement;
		if (_walking)
			AdvanceLinkWalkAnimation();
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	private void InitializeForcedRoomEntryMovement()
	{
		// linkState0b substate0, after checkLinkForceState's request update.
		_world.Pegasus?.Clear();
		CancelNativeItemUsage();
		NativeAdjacentWalls = 0;
		SetTopDownTerrainSpeed(0,GetTopDownMovementSpeed(),writeSpeedDirectly:true);
	}

	internal void EndForcedRoomEntryMovement()
	{
		CancelNativeForcedMovement();
		_walking = false;
		QueueRedraw();
	}

	/// <summary>
	/// spinnerScript drops Link's held object on the collision update, one
	/// interaction update before INTERAC_SPINNER clears every parent item and
	/// snaps Link to its entry point.
	/// </summary>
	internal void BeginSpinnerTouch()
	{
		if (_world is not null)
		{
			_world.InterruptBomb(this, discard: false);
			_world.InterruptBracelet(this, discard: false);
		}
		_walking = false;

		QueueRedraw();
	}

	internal void BeginSpinnerTurn(Vector2 position, int exitDirection)
	{
		InterruptCarriedItems(discard: true);
		CancelSwordAttack();
		CancelShovelAction();
		ClearShieldParent();
		ClearTopDownAirState();
		_enemyKnockbackFrames = 0.0f;
		_pendingSwordKnockbackFrames = 0;
		SetSpinnerTurnPosition(position, exitDirection);
	}

	internal void SetSpinnerTurnPosition(Vector2 position, int exitDirection)
	{
		if (exitDirection is < 0 or > 3)
			throw new ArgumentOutOfRangeException(nameof(exitDirection));
		// spinner_setLinkRelativePosition writes yh/xh only.
		_precisePosition = new(
			Mathf.Floor(position.X)+_precisePosition.X-Mathf.Floor(_precisePosition.X),
			Mathf.Floor(position.Y)+_precisePosition.Y-Mathf.Floor(_precisePosition.Y));
		_facing = (Facing)exitDirection;
		_walking = false;

		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void BeginSpinnerExit(int exitDirection)
	{
		if (exitDirection is < 0 or > 3)
			throw new ArgumentOutOfRangeException(nameof(exitDirection));
		// The interaction publishes a request; Link consumes it on its next
		// visit, then owns counter1 and all15 movement updates itself.
		RequestNativeForcedMovement(0x10, exitDirection * 8, exitDirection);

		QueueRedraw();
	}

	internal void SetScriptedPosition(Vector2 position)
	{
		_precisePosition = position;
		Position = OracleObjectMath.ToPixelPosition(position);
		QueueRedraw();
	}

	internal void AdvanceInteractionVelocity(int speed, int angle)
	{
		// Native interactions call updateLinkPositionGivenVelocity, which applies
		// adjacent-wall masks and cardinal edge sliding before changing Y/X.
		// The interaction phase consumes Link's retained adjacent-wall byte,
		// sampled before his input movement; it does not probe his new position.
		Vector2 movement = _world.ResolveNativeMovement(_precisePosition, speed, angle, allowWallSlide: true,
			adjacentWalls: NativeAdjacentWalls);
		SetScriptedPosition(_precisePosition + movement);
	}

	internal void ApplyMovingPlatformDisplacement(Vector2 displacement)
	{
		_precisePosition += displacement;
		_sideScrollYFixed =
			Mathf.FloorToInt(_precisePosition.Y * 256.0f);
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void ApplySideScrollMovingPlatformVelocity(
		int speed,
		int angle)
	{
		// interactionCodea1 calls updateLinkPositionGivenVelocity before
		// objectApplySpeed for right/down/left carries. Link therefore still
		// observes adjacent-wall collision even though the platform moves.
		ApplySideScrollVelocity(speed, angle, allowWallSlide: true);
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void ApplyMovingPlatformHighByteDisplacement(
		Vector2I displacement)
	{
		SetCoordinateHigh(
			horizontal: false,
			Mathf.FloorToInt(_precisePosition.Y) + displacement.Y);
		SetCoordinateHigh(
			horizontal: true,
			Mathf.FloorToInt(_precisePosition.X) + displacement.X);
		_sideScrollYFixed =
			Mathf.FloorToInt(_precisePosition.Y * 256.0f);
	}

	internal void SynchronizeMovingPlatformSubpixels(
		Vector2 platformPrecisePosition)
	{
		// sidescrollPlatform_updateLinkSubpixels copies both low coordinate
		// bytes when Link first mounts a side-view platform. Applying the same
		// later velocity with matched fractions keeps their rendered high
		// bytes in lockstep.
		_precisePosition = new Vector2(
			Mathf.Floor(_precisePosition.X) +
				(platformPrecisePosition.X -
					Mathf.Floor(platformPrecisePosition.X)),
			Mathf.Floor(_precisePosition.Y) +
				(platformPrecisePosition.Y -
					Mathf.Floor(platformPrecisePosition.Y)));
		_sideScrollYFixed =
			Mathf.FloorToInt(_precisePosition.Y * 256.0f);
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void SetMovingPlatformCoordinateHigh(
		bool horizontal,
		int coordinate)
	{
		// thwomp_updateLinkRidingSelf writes only Link's high Y byte.
		SetCoordinateHigh(horizontal, coordinate);
	}

	internal bool CheckSideScrollPlatformRide(
		Vector2 platformPosition,
		int radiusY,
		int radiusX)
	{
		int linkY = Mathf.FloorToInt(Position.Y);
		int linkX = Mathf.FloorToInt(Position.X);
		int platformY = Mathf.FloorToInt(platformPosition.Y);
		int platformX = Mathf.FloorToInt(platformPosition.X);
		if (!SidePlatformAxisOverlaps(
				linkY, platformY, radiusY + 6) ||
			!SidePlatformAxisOverlaps(
				linkX, platformX, radiusX + 6))
		{
			return false;
		}

		return linkY < unchecked((byte)(platformY - radiusY - 2)) &&
			SidePlatformLinkIsClose(
				linkY, linkX, platformY, platformX, radiusY, radiusX);
	}

	internal void ResolveSideScrollPlatformContact(
		Vector2 platformPosition,
		int radiusY,
		int radiusX,
		int platformAngle,
		bool riding)
	{
		// sidescrollingPlatformCommon returns unless w1Link.state is
		// LINK_STATE_NORMAL. Knockback remains a normal-state counter, but
		// death and the hazard/respawn states do not receive platform pushes.
		if (IsDying || _drowning || _fallingInHole || _pullingIntoHole ||
			SideScrollSquished || RespawnWaiting || RespawnRecovering)
			return;

		int linkY = Mathf.FloorToInt(Position.Y);
		int linkX = Mathf.FloorToInt(Position.X);
		int platformY = Mathf.FloorToInt(platformPosition.Y);
		int platformX = Mathf.FloorToInt(platformPosition.X);
		if (!SidePlatformAxisOverlaps(
				linkY, platformY, radiusY + 6) ||
			!SidePlatformAxisOverlaps(
				linkX, platformX, radiusX + 6))
		{
			return;
		}

		if (!SidePlatformLinkIsClose(
				linkY, linkX, platformY, platformX, radiusY, radiusX))
		{
			int probeX = linkX < platformX ? linkX - 5 : linkX + 4;
			bool blocked =
				_world.SideScrollTileBlocksPoint(
					new Vector2(probeX, linkY - 4)) ||
				_world.SideScrollTileBlocksPoint(
					new Vector2(probeX, linkY + 4));
			if (!blocked)
			{
				SnapAwayFromSidePlatform(
					horizontal: true,
					linkX,
					platformX,
					radiusX + 6);
				return;
			}
			if (CheckSideScrollPlatformSquish(
					platformPosition,
					radiusY,
					radiusX,
					platformAngle))
			{
				return;
			}
			MoveAwayFromSidePlatform(
				linkY >= platformY ? ObjectAngle.Up : ObjectAngle.Down);
			return;
		}

		int verticalProbeY =
			linkY < platformY ? linkY - 6 : linkY + 9;
		bool leftBlocked = _world.SideScrollTileBlocksPoint(
			new Vector2(linkX - 3, verticalProbeY));
		bool rightBlocked = _world.SideScrollTileBlocksPoint(
			new Vector2(linkX + 2, verticalProbeY));
		if (!leftBlocked && !rightBlocked)
		{
			SnapAwayFromSidePlatform(
				horizontal: false,
				linkY,
				platformY,
				radiusY + 6);
			return;
		}
		if (CheckSideScrollPlatformSquish(
				platformPosition,
				radiusY,
				radiusX,
				platformAngle))
		{
			return;
		}

		if (riding && leftBlocked != rightBlocked)
		{
			SnapAwayFromSidePlatform(
				horizontal: false,
				linkY,
				platformY,
				radiusY + 6);
			MoveAwayFromSidePlatform(leftBlocked ? ObjectAngle.Right : ObjectAngle.Left);
			return;
		}
		MoveAwayFromSidePlatform(linkX >= platformX ? ObjectAngle.Right : ObjectAngle.Left);
	}

	private static bool SidePlatformAxisOverlaps(
		int linkCoordinate,
		int platformCoordinate,
		int combinedRadius) =>
		unchecked((byte)(
			linkCoordinate - platformCoordinate + combinedRadius)) <
		combinedRadius * 2;

	private bool SidePlatformLinkIsClose(
		int linkY,
		int linkX,
		int platformY,
		int platformX,
		int radiusY,
		int radiusX)
	{
		int xRadius = radiusX + (_sideScrollAirborne ? 4 : 5);
		if (unchecked((byte)(linkX - platformX + xRadius)) >=
			xRadius * 2 + 1)
		{
			return false;
		}

		// sidescrollPlatform_checkLinkIsClose performs SUB $02, ADD A and
		// INC A in byte registers. State0 still has radiusY=$00, so the
		// comparison limit is $fd rather than signed -3.
		int yRadius = unchecked((byte)(radiusY - 2));
		return unchecked((byte)(linkY - platformY + yRadius)) >=
			unchecked((byte)(yRadius * 2 + 1));
	}

	private bool CheckSideScrollPlatformSquish(
		Vector2 platformPosition,
		int radiusY,
		int radiusX,
		int platformAngle)
	{
		int linkY = Mathf.FloorToInt(Position.Y);
		int linkX = Mathf.FloorToInt(Position.X);
		int platformY = Mathf.FloorToInt(platformPosition.Y);
		int platformX = Mathf.FloorToInt(platformPosition.X);
		bool yInside = unchecked((byte)(
			linkY - platformY + radiusY)) < radiusY * 2 + 1;
		int xRadius = radiusX + 2;
		bool xInside = unchecked((byte)(
			linkX - platformX + xRadius)) < xRadius * 2 + 1;
		if (!yInside || !xInside)
			return false;

		ForceSideScrollSquish(vertical: (platformAngle & 0x08) == 0);
		return true;
	}

	private void SnapAwayFromSidePlatform(
		bool horizontal,
		int linkCoordinate,
		int platformCoordinate,
		int combinedRadius)
	{
		int sign = platformCoordinate < linkCoordinate ? 1 : -1;
		SetCoordinateHigh(
			horizontal,
			platformCoordinate + sign * combinedRadius);
	}

	private void MoveAwayFromSidePlatform(int angle)
	{
		ApplySideScrollVelocity(
			_world.SideScrollParameters.PlatformPushSpeed,
			angle,
			allowWallSlide: false);
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void CopyPortalPosition(Vector2 position)
	{
		SetScriptedCoordinateHigh(horizontal: false, OracleObjectPosition.HighByte(position.Y));
		SetScriptedCoordinateHigh(horizontal: true, OracleObjectPosition.HighByte(position.X));
		// objectCopyPosition copies zh too, preserving Link's low Z byte.
		_topDownAirZFixed &= 0xff;
		_cutsceneDrawZFixed &= 0xff;
	}

	internal void SetScriptedCoordinateHigh(bool horizontal, int coordinate)
	{
		// preventObjectHFromPassingObjectD overwrites only Object.xh/yh. Keep
		// the 8.8 fractional byte accumulated by linkCutscene6 intact.
		SetCoordinateHigh(horizontal, coordinate);
	}

	// bank0.objectPushLinkAwayOnCollision uses SPEED_100 in the angle from
	// the interaction to Link, retaining Link's ordinary terrain response.
	internal void ApplyNpcLightPush(Vector2 objectPosition)
	{
		if (PassesNpcs) return;
		int angle = OracleObjectMovement.Shared.RelativeAngle(
			objectPosition, Position);
		ApplyTopDownObjectSpeed(ObjectSpeed.Speed100, angle,
			allowWallSlide: true);
	}

	internal byte ScriptedZHigh => unchecked((byte)(_topDownAirZFixed >> 8));

	internal void SetScriptedZHigh(byte coordinate)
	{
		// INTERAC$33 increments/decrements Link.zh while Link's update is
		// disabled. Preserve zl and wLinkInAir; this is not a jump/landing.
		int signedHigh = unchecked((sbyte)coordinate);
		_topDownAirZFixed = (signedHigh << 8) | (_topDownAirZFixed & 0xff);
		// Airborne body drawing already applies zh. A suspended grounded
		// pose needs the presentation offset, without drawing zl fractions.
		_cutsceneDrawZFixed = _topDownAirborne ? 0 : signedHigh << 8;
		QueueRedraw();
	}

	internal void SetScreenTransitionBoundaryCoordinate(
		bool horizontal,
		int coordinate)
	{
		// screenTransitionState2 clamps the high coordinate to the original
		// edge before checking wDisableScreenTransitions. Preserve the low
		// coordinate exactly as the source write does.
		SetCoordinateHigh(horizontal, coordinate);
	}

	private void SetCoordinateHigh(bool horizontal, int coordinate)
	{
		if (horizontal)
		{
			float fraction = _precisePosition.X - Mathf.Floor(_precisePosition.X);
			_precisePosition.X = coordinate + fraction;
		}
		else
		{
			float fraction = _precisePosition.Y - Mathf.Floor(_precisePosition.Y);
			_precisePosition.Y = coordinate + fraction;
			_sideScrollYFixed =
				Mathf.FloorToInt(_precisePosition.Y * 256.0f);
		}
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		QueueRedraw();
	}

	internal void MoveLocalRespawnOffShutter(
		OracleRoomData room,
		int doorPackedPosition,
		int doorSubId)
	{
		if (room.GetPackedPosition(_lastSafePosition) != doorPackedPosition)
			return;
		int offset = (doorSubId & 0x03) switch
		{
			0 => 0x10,
			1 => -1,
			2 => -0x10,
			_ => 1
		};
		int packed = doorPackedPosition + offset;
		_lastSafePosition = new Vector2(
			(packed & 0x0f) * OracleRoomData.MetatileSize + 8,
			(packed >> 4) * OracleRoomData.MetatileSize + 8);
	}

	internal void RequestForcedRespawn()
	{
		// doorController and bank0.respawnLink only write wLinkForceState
		// and parameter2; both feed the same state02 owner.
		// Position, visibility and item cancellation belong to state02.
		_inventory.RuntimeState.SetField("wLinkForceState",2);
		_inventory.RuntimeState.SetField("wLinkStateParameter",2);
	}

	private void InitializeForcedRespawn()
	{
		// linkState02 reads the parameter in substate0, after request consumption.
		CancelNativeItemUsage();
		byte parameter=_inventory.RuntimeState.ReadField("wLinkStateParameter");
		if(parameter > 4)
			throw new NotSupportedException($"link.s: state$02 parameter${parameter:x2} requires its native fall binding.");
		_forcedRespawnPhase=3;
		_fallingInHole=false;_fallInHoleRespawning=false;_fallInHoleWarpPending=false;
		_drowning=false;_topDownDrownPhase=0;_drownRespawning=false;
		if(parameter is 0 or 1 or 3)
		{
			_pullingIntoHole=false;_holePullPackedPosition=-1;_fallingInHole=true;
			_fallInHoleRespawning=false;_fallInHoleWarpPending=false;_walking=false;
			NativeLinkSubstate=(byte)(parameter==3 ? 4 : 1);
			InitializeHoleFallPresentation(center:parameter==0);
		}
		else if(parameter==2)
		{
			BeginNativeRespawnWait(collisionsDisabled:_instantRespawnCollisionDisabled);
		}
		else
		{
			NativeLinkSubstate=5;
			_drowning=true;_topDownDrownPhase=2;
			if(_drowningHazard==HazardType.None)_drowningHazard=HazardType.Water;
			UpdateTopDownDrowning();
		}
	}

	private bool AdvanceNativeRespawnState()
	{
		if(_forcedRespawnPhase<2)return false;
		_inventory.RuntimeState.SetField("wGameKeysPressed", 0xff);
		SetNativePushAnimationOverride(0x80);
		if(NativeLinkSubstate==0){InitializeForcedRespawn();return true;}
		if(NativeLinkSubstate==2)
		{
			// State02 substate2 reloads counter1 while scroll bit7 is held.
			if((_inventory.RuntimeState.ReadField("wScrollMode")&0x80)!=0)
			{NativeLinkCounter=4;return true;}
			NativeLinkCounter=unchecked((byte)(NativeLinkCounter-1));
			if(NativeLinkCounter==0)BeginRespawnRecovery();
		}
		else if(NativeLinkSubstate==3)
		{
			NativeLinkCounter=unchecked((byte)(NativeLinkCounter-1));
			if(NativeLinkCounter==0)
			{
				_instantRespawnCollisionDisabled=false;_forcedRespawnPhase=0;NativeLinkSubstate=0;
				_inventory.RuntimeState.SetWramByte(0xd035,0); // initLinkState retains the pending force-state byte.
				_fallingInHole=false;_fallInHoleRespawning=false;_fallInHoleWarpPending=false;
				_drowning=false;_drownRespawning=false;_topDownDrownPhase=0;
				_airborneLinkAnimationMode=AirborneLinkAnimationMode.None;
				Visible=true;ResetLinkWalkAnimation(); // initLinkStateAndAnimateStanding sets visible bit7.
			}
			_walking=false;QueueRedraw();
		}
		else if(NativeLinkSubstate==5)UpdateTopDownDrowning();
		else if(NativeLinkSubstate==1)UpdateFallInHole();
		else if(NativeLinkSubstate==4)
		{
			// Parameter3 never respawns or applies damage. The previous
			// animation marker gates visibility and the next forced state.
			if ((_inventory.RuntimeState.ReadWramByte(0xd021) & 0x80) == 0)
				LinkNativeAnimationDatabase.For(AssetRoot).Animate(_inventory.RuntimeState);
			else
			{
				Visible=false;
				ConsumeNativeForcedState();
			}
			QueueRedraw();
		}
		else throw new NotSupportedException($"link.s: state$02 substate${NativeLinkSubstate:x2} requires its native handler.");
		return true;
	}

	internal void RequestSuctionPitFall(Vector2 center)
	{
		// seaEffects.s snaps only high Y/X bytes and clears parents now;
		// checkLinkForceState and the fall initializer occupy later updates.
		_precisePosition += center - OracleObjectMath.ToPixelPosition(_precisePosition);
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		// State02's objectCenterOnTile also clears both fractional bytes.
		_holePullCenter = center;
		// Later writers (eg doorController's parameter2) overwrite this
		// same request; there is no independent suction state priority.
		_inventory.RuntimeState.SetField("wLinkForceState",2);
		_inventory.RuntimeState.SetField("wLinkStateParameter",0);
		ClearNativeItemParents();
		QueueRedraw();
	}

	internal void StopSuctionPitMovementSpeed() => _topDownMovementSpeedRaw = 0;

	internal void ApplyPollutionContact(Vector2 tileCenter)
	{
		// seaEffects.s:@collision0 checks only the live invincibility/recoil
		// bytes after the PART's collision/diving gate. Damage is consumed
		// by the next Link update, with that update's ordinary ring modifiers.
		if (_enemyInvincibilityFrames != 0 || _enemyKnockbackFrames != 0) return;
		_nativeContactSignal = true;
		_enemyInvincibilityFrames = 0x18;
		_enemyKnockbackFrames = 0x0c;
		_enemyKnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(tileCenter, EnemyContactPosition);
		_pendingContactDamageRaw = -2;
		_world.PlaySound(SoundId.SndDamageLink);
	}

	internal void EndCutsceneControl(object? owner = null)
	{
		if (owner is not null && !IsCutsceneControlOwner(owner))
			return;
		_scriptControl = ScriptControl.None;
		_cutsceneIdleFacing = null;
		_forcedState08LowPriority = false;
		SetAlternateTextboxPalettePriority(false);
		_cutsceneControlOwner = null;
		_walking = false;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (!ApplicationUpdateOwned)
			AdvanceItems(delta);
	}

	private static readonly PlayerGroundDrawPass[] GroundDrawOrder =
	[
		PlayerGroundDrawPass.Body,
		PlayerGroundDrawPass.TerrainEffect
	];

	internal static ReadOnlySpan<PlayerGroundDrawPass> GroundDrawPasses =>
		GroundDrawOrder;

	internal void ApplyTerrainWalkSoundParameter()
	{
		var memory = _inventory.RuntimeState;
		byte parameter = memory.ReadWramByte(0xd021);
		if ((parameter & 0x20) == 0)
			return;

		LinkTerrainEffectFrame? terrainEffect = GetCurrentTerrainEffect();
		if (terrainEffect is not { Sound: > 0 })
			return;

		// @tileType_puddle consumes animParameter bit 5 even when
		// wLinkImmobilized suppresses playback. On other terrain the bit
		// remains available until the walking animation loads its next frame.
		memory.SetWramByte(0xd021, (byte)(parameter & ~0x20));
		if (!_world.MovementDisabled)
			_world.PlaySound(terrainEffect.Sound);
	}

	private void TryMove(Vector2 movement, bool allowWallSlide = false)
	{
		if (movement == Vector2.Zero)
			return;

		Vector2 resolved = _world.ResolveMovement(_precisePosition, movement, allowWallSlide);
		if (resolved != Vector2.Zero)
		{
			_precisePosition += resolved;
		}
	}

	private void ApplySpikeDamage(int movementAngle)
	{
		// common/specialObjects/commonCode.s:dealSpikeDamageToLink writes
		// raw damage -$04 (two health quarters), then calls linkApplyDamage
		// directly. Its var2a publication lasts until this Link pass ends.
		if (_world.RidingObject || _enemyInvincibilityFrames != 0.0f)
		{
			return;
		}

		_enemyInvincibilityFrames = 40.0f;
		_enemyKnockbackFrames = RingEffects.KnockbackFrames(
			_inventory, ((int)_enemyKnockbackFrames + 10) & 0xff);
		_nativeContactSignal = true;
		int knockbackAngle = movementAngle < 0x80
			? (movementAngle ^ ObjectAngle.HalfTurn)
			: 0xff;
		_enemyKnockbackAngle = knockbackAngle;
		_world.PlaySound(SoundId.SndDamageLink);
		ApplyDamage(2, RingDamageSource.Spike);
	}

	private static void UpdateConvergingVelocity(
		ref int angle,
		ref int speedRaw,
		int targetSpeedRaw,
		ref int velocityCounter,
		int velocityInterval,
		int inputAngle,
		bool inAir)
	{
		if (angle >= 0x80)
		{
			angle = inputAngle;
			return;
		}

		int angleStep = 0;
		int speedStep;
		if (inputAngle >= 0x80)
		{
			speedStep = inAir ? 0 : -5;
		}
		else
		{
			int relative = (inputAngle - angle + 4) & ObjectAngle.Mask;
			if (relative < 9)
			{
				speedStep = 5;
				angleStep = -1;
				if (relative >= 3 && relative < 6)
				{
					angle = inputAngle;
					angleStep = 0;
				}
				else if (relative >= 6)
				{
					angleStep = 1;
				}
			}
			else
			{
				relative = (relative - 0x10) & 0xff;
				if (relative < 9)
				{
					speedStep = -5;
					angleStep = 1;
					if (relative >= 3 && relative < 6)
					{
						angle = inputAngle ^ ObjectAngle.HalfTurn;
						angleStep = 0;
					}
					else if (relative >= 6)
					{
						angleStep = -1;
					}
				}
				else
				{
					speedStep = 0;
					angleStep = (relative & 0x80) != 0 ? 1 : -1;
				}
			}
		}

		velocityCounter = (velocityCounter + 1) & 0xff;
		if (velocityCounter < velocityInterval)
			return;
		velocityCounter = 0;
		angle = (angle + angleStep) & ObjectAngle.Mask;

		int speed = speedRaw + speedStep;
		if (speed <= 0)
		{
			speedRaw = ObjectSpeed.Speed0;
			// func_5933 stores l ($12) in var12 when speed reaches zero.
			velocityCounter = 0x12;
			angle = 0xff;
			return;
		}
		speedRaw = Math.Min(targetSpeedRaw, speed);
	}

	private static int AngleForVector(Vector2 vector)
	{
		int x = Mathf.Abs(vector.X) > 0.01f ? Math.Sign(vector.X) : 0;
		int y = Mathf.Abs(vector.Y) > 0.01f ? Math.Sign(vector.Y) : 0;
		return (x, y) switch
		{
			(0, -1) => 0x00,
			(1, -1) => 0x04,
			(1, 0) => 0x08,
			(1, 1) => 0x0c,
			(0, 1) => 0x10,
			(-1, 1) => 0x14,
			(-1, 0) => 0x18,
			(-1, -1) => 0x1c,
			_ => 0xff
		};
	}

	private static int FractionalByte(float coordinate) =>
		Mathf.FloorToInt(coordinate * 256.0f) & 0xff;

	private void BeginNativeRespawnWait(bool collisionsDisabled = false)
	{
		// specialObjectSetCoordinatesToRespawnYX copies only yh/xh;
		// Link retains his current yl/xl, not the saved position's fraction.
		SetCoordinateHigh(horizontal: false, Mathf.FloorToInt(_lastSafePosition.Y));
		SetCoordinateHigh(horizontal: true, Mathf.FloorToInt(_lastSafePosition.X));
		// bank0.specialObjectSetCoordinatesToRespawnYX writes angle=$ff;
		// underwater convergence must not resume the preceding impulse.
		_topDownMovementAngle = 0xff;
		_topDownAirZFixed = 0;
		_enemyKnockbackFrames = 0.0f;
		_facing = _localRespawnFacing;
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		bool retainAir = _world.SideScrolling && _sideScrollAirborne;
		int retainedSpeedZ = _sideScrollSpeedZ;
		int retainedSwimming = _sideScrollSwimmingState;
		ClearSideScrollState(_precisePosition);
		// linkState02 @respawn leaves wLinkInAir/speedZ intact until the
		// invisible counter's zero update clears the shared air byte.
		_sideScrollAirborne = retainAir;
		_sideScrollSwimmingState = retainedSwimming;
		if (retainAir) _sideScrollSpeedZ = retainedSpeedZ;
		NativeLinkSubstate=2;NativeLinkCounter=2;
		_instantRespawnCollisionDisabled = collisionsDisabled;
		// linkState02 @respawn breaks the landing tile before writing
		// wEnteredWarpPosition, then hides Link for the counter$02 delay.
		_world.ApplyLandedTileHit(Position);
		_world.DeactivateWarpAtPlayerPosition(this);
		Visible = false;
		_walking = false;
		QueueRedraw();
	}

	internal bool IsAttemptingObjectPush(Vector2I direction)
	{
		if (direction == Vector2I.Zero || direction != FacingVector ||
			IsUsingItem || IsCarryingObject || IsHoldingItemOneHand ||
			IsHoldingItemTwoHands || !IsGroundedForFloorButton ||
			Input.IsActionPressed("attack") || Input.IsActionPressed("item"))
		{
			return false;
		}

		// The source rejects wLinkAngle diagonals before comparing Link's
		// direction with the direction from Link to the cube.
		bool horizontal = Mathf.Abs(_lastMovementInput.X) > 0.01f;
		bool vertical = Mathf.Abs(_lastMovementInput.Y) > 0.01f;
		return horizontal != vertical &&
			_lastMovementInput.Dot((Vector2)direction) > 0.01f;
	}

	// bank0.objectCheckLinkPushingAgainstCenter: no control, facing or Z gate.
	internal bool IsAttemptingCenteredObjectPush => _activeTransformation == 0 &&
		_lastMovementInput != Vector2.Zero &&
		!Input.IsActionPressed("attack") && !Input.IsActionPressed("item");

	internal bool IsAttemptingMinecartPush =>
		// checkLinkID0AndControlNormal jumps past the collision/menu and
		// invincibility tests in Ages. objectCheckLinkPushingAgainstCenter
		// accepts every non-$ff input angle and rejects held A/B; it does not
		// test facing, item parents, or the cube's cardinal pushing rule.
		_activeTransformation == 0 && !IsDying && !NativeMovementSuppressed &&
		!_newGameSlowFalling && !_roomWarpFallActive &&
		_ledgeJumpState == LedgeJumpState.None &&
		!_minecartJumpControlled && !_companionJumpControlled &&
		ObjectZHigh == 0 && _lastMovementInput != Vector2.Zero &&
		!Input.IsActionPressed("attack") && !Input.IsActionPressed("item");

	private int GetWalkAnimationFrame() =>
		_walking || _world.Pegasus is { Active: true } ? _linkWalkAnimationFrame : 0;

	private void ResetLinkWalkAnimation()
	{
		_scriptedLinkAnimationMode = null;
		if (_world.Pegasus is { Active: true }) { AdvanceLinkWalkAnimation(); return; }
		// animateLinkStanding forces animMode away from WALK and immediately
		// reselects it, loading animationData19f0b's two-update $54 frame.
		LinkNativeAnimationDatabase.For(AssetRoot).Set(_inventory.RuntimeState, 0x10);
	}

	private void AdvanceLinkWalkAnimation()
	{
		_scriptedLinkAnimationMode = null;
		_world.Pegasus?.AnimateWalking();
		var animations = LinkNativeAnimationDatabase.For(AssetRoot);
		if (_inventory.RuntimeState.ReadWramByte(0xd030) != 0x10)
			animations.Set(_inventory.RuntimeState, 0x10);
		else
			animations.Animate(_inventory.RuntimeState);
	}

	private void UpdateFacing(Vector2 input)
	{
		_facing = FacingForInput(_facing, input);
	}

	/// <summary>
	/// The shared Bomb/Bracelet parent stores angle $ff when no direction is
	/// held. Otherwise it uses w1Link.direction after the current input angle
	/// has updated Link's cardinal facing.
	/// </summary>
	internal Vector2I SelectCarriedObjectReleaseDirection(Vector2 input)
	{
		if (input.LengthSquared() <= 0.01f)
			return Vector2I.Zero;

		UpdateFacing(input);
		return FacingVector;
	}

	private static Facing FacingForInput(Facing current, Vector2 input)
	{
		float horizontal = Mathf.Abs(input.X);
		float vertical = Mathf.Abs(input.Y);
		if (horizontal > vertical)
			return input.X > 0 ? Facing.Right : Facing.Left;
		if (vertical > horizontal)
			return input.Y > 0 ? Facing.Down : Facing.Up;
		if (horizontal > 0.01f)
		{
			Facing horizontalFacing = input.X > 0 ? Facing.Right : Facing.Left;
			Facing verticalFacing = input.Y > 0 ? Facing.Down : Facing.Up;
			if (current == horizontalFacing || current == verticalFacing)
				return current;

			// updateLinkDirectionFromAngle keeps either current diagonal
			// component. With neither component current, angles $04/$0c/$14/$1c
			// round to up/right/down/left respectively.
			return input.X > 0
				? input.Y < 0 ? Facing.Up : Facing.Right
				: input.Y > 0 ? Facing.Down : Facing.Left;
		}
		return current;
	}

	private static bool DirectionalInputJustPressed() =>
		Input.IsActionJustPressed("move_up") ||
		Input.IsActionJustPressed("move_right") ||
		Input.IsActionJustPressed("move_down") ||
		Input.IsActionJustPressed("move_left");

	private static Vector2I FacingVectorFor(Facing facing) => facing switch
	{
		Facing.Up => Vector2I.Up,
		Facing.Right => Vector2I.Right,
		Facing.Down => Vector2I.Down,
		_ => Vector2I.Left
	};

	public void Face(Vector2I direction)
	{
		_facing = direction == Vector2I.Up ? Facing.Up
			: direction == Vector2I.Right ? Facing.Right
			: direction == Vector2I.Down ? Facing.Down
			: Facing.Left;
		QueueRedraw();
	}

	internal void FaceShooterDirection(Vector2I direction)
	{
		_facing = FacingForInput(_facing, direction);
		QueueRedraw();
	}

	/// <summary>
	/// Mirrors linkState01's ordinary non-side-view movement path. The Game
	/// Boy input supplies one of eight wLinkAngle values; updateLinkSpeed_standard
	/// selects an original speed byte and specialObjectUpdatePosition applies
	/// the imported signed 8.8 vector. Godot's normalized input magnitude must
	/// not become authoritative position state.
	/// </summary>
	private bool AdvanceTopDownInputMovement(
		Vector2 input,
		bool movementAllowed)
	{
		int angle = AngleForVector(input);
		Vector2 beforeInputMovement = _precisePosition;
		// linkState01 var2f bit 7 dispatches the same $98 velocity row as
		// surface Mermaid swimming, even when the active tile is dry floor.
		if (_world.Underwater)
		{
			var parameters = MermaidSwimming;
			int target = RingEffects.UsesFastSwim(_inventory)
				? parameters.MermaidFastTargetSpeed : parameters.MermaidTargetSpeed;
			SetTopDownTerrainSpeed(0x98, target, writeSpeedDirectly: false);
			UpdateMermaidImpulseVelocity(ref _topDownMovementAngle, ref _topDownMovementSpeedRaw,
				target, ref _topDownMovementVelocityCounter, movementAllowed ? angle : 0xff, movementAllowed);
			Vector2 beforeMovement = _precisePosition;
			if (_topDownMovementAngle < 0x80 && _topDownMovementSpeedRaw != 0)
				ApplyTopDownObjectSpeed(_topDownMovementSpeedRaw, _topDownMovementAngle,
					allowWallSlide: true);
			// linkUpdateMovement calls linkResetSpeed when both wall probes
			// prevent displacement, while retaining angle and convergence phase.
			if (_precisePosition == beforeMovement) _topDownMovementSpeedRaw = 0;
			if (movementAllowed && angle < 0x80)
				UpdateHeartRingCounter(_precisePosition - beforeInputMovement);
			if (movementAllowed && angle < 0x80 && !IsUsingItem) UpdateFacing(input);
			return movementAllowed && angle < 0x80;
		}
		if (UsesTopDownIcePhysics)
		{
			SetTopDownTerrainSpeed(0x88, NormalMovementSpeed, writeSpeedDirectly: false);
			UpdateConvergingVelocity(ref _topDownMovementAngle, ref _topDownMovementSpeedRaw,
				_topDownMovementTargetSpeedRaw, ref _topDownMovementVelocityCounter,
				_topDownMovementVelocityInterval, movementAllowed ? angle : 0xff, inAir: false);
			if (_topDownMovementAngle < 0x80 && _topDownMovementSpeedRaw != 0 &&
				!ApplyTopDownObjectSpeed(_topDownMovementSpeedRaw, _topDownMovementAngle,
					allowWallSlide: true))
				_topDownMovementSpeedRaw = 0; // linkUpdateMovement -> linkResetSpeed when blocked.
			// Released ice momentum uses C=$02, omitting the Heart Ring flag.
			if (movementAllowed && angle < 0x80)
				UpdateHeartRingCounter(_precisePosition - beforeInputMovement);
			if (movementAllowed && angle < 0x80 && !IsUsingItem)
				UpdateFacing(input);
			return movementAllowed && angle < 0x80;
		}
		_topDownMovementAngle = angle;
		if (!movementAllowed || NativeMovementSuppressed || angle >= 0x80)
		{
			_topDownMovementSpeedRaw = 0;
			// wcc95 stops translation; linkState01 still updates direction.
			if (movementAllowed && angle < 0x80 && !IsUsingItem)
				UpdateFacing(input);
			return false;
		}

		// parentItemLoadAnimationAndIncState disables Link's turning for the
		// sword's full lifetime, even after state 6 re-enables movement.
		if (!IsUsingItem)
			UpdateFacing(input);

		if ((_inventory.RuntimeState.ReadField("wLinkImmobilized") & 0x10) != 0)
		{
			_topDownMovementSpeedRaw = 0;
			return true;
		}
		SetTopDownTerrainSpeed(0, GetTopDownMovementSpeed(), writeSpeedDirectly: true);
		if (!ApplyTopDownObjectSpeed(
			_topDownMovementSpeedRaw, angle,
			allowWallSlide: true))
			_topDownMovementSpeedRaw = 0; // linkUpdateMovement -> linkResetSpeed.
		UpdateHeartRingCounter(_precisePosition - beforeInputMovement);
		return true;
	}

	private bool UsesTopDownIcePhysics => !_topDownAirborne &&
		(_inventory.RuntimeState.ReadField("wIsTileSlippery") & 0x40) != 0;

	private void SetTopDownTerrainSpeed(int terrainMode, int targetSpeed, bool writeSpeedDirectly)
	{
		// updateLinkSpeed_withParam: the slippery row's zero initial speed
		// preserves the preceding object's speed rather than stopping it.
		if (_topDownMovementTerrainMode != terrainMode)
		{
			_topDownMovementTerrainMode = terrainMode;
			if (terrainMode == 0)
				_topDownMovementSpeedRaw = NormalTopDownSpeed;
			_topDownMovementVelocityCounter = 0;
			_topDownMovementVelocityInterval = terrainMode == 0x88
				? TopDownAirDatabase.For(AssetRoot).Parameters.IceVelocityInterval
				: terrainMode == 0x98 ? MermaidSwimming.MermaidVelocityInterval : 0;
		}
		_topDownMovementTargetSpeedRaw = targetSpeed;
		if (writeSpeedDirectly)
			_topDownMovementSpeedRaw = targetSpeed;
	}

	private bool ApplyTopDownObjectSpeed(
		int speed,
		int angle,
		bool allowWallSlide,
		int? adjacentWalls = null)
	{
		OracleObjectPosition position =
			OracleObjectMovement.Shared.PositionFromPixels(_precisePosition);
		_precisePosition = position.PrecisePosition;

		Vector2 resolved = _world.ResolveNativeMovement(
			_precisePosition, speed, angle, allowWallSlide, adjacentWalls);
		if (resolved != Vector2.Zero)
		{
			position = position.Add(
				Mathf.RoundToInt(resolved.Y * 256.0f),
				Mathf.RoundToInt(resolved.X * 256.0f));
			_precisePosition = position.PrecisePosition;
			return true;
		}

		return false;
	}

	private int NormalMovementSpeed => _world.Pegasus is { Active: true } pegasus
		? pegasus.NormalSpeed : NormalTopDownSpeed;

	private int GetTopDownMovementSpeed()
	{
		TerrainType terrain = (TerrainType)_inventory.RuntimeState.ReadField("wActiveTileType");
		// updateLinkSpeed_withParam retains its grass-column index on holes
		// and warp holes, and skips the Pegasus modifier there.
		if (terrain is TerrainType.Hole or TerrainType.WarpHole)
			return GrassTopDownSpeed;
		if (_world.Pegasus is { Active: true } pegasus)
			return terrain switch
			{
				TerrainType.Grass => pegasus.GrassSpeed,
				TerrainType.Stairs or TerrainType.Vines => pegasus.StairsSpeed,
				_ => pegasus.NormalSpeed
			};
		// link.s:updateLinkSpeed_withParam slows only TILETYPE_GRASS.
		// Puddle presentation does not select that speed-table column.
		return terrain switch
		{
			TerrainType.Grass => GrassTopDownSpeed,
			TerrainType.Stairs or TerrainType.Vines => StairsTopDownSpeed,
			_ => NormalTopDownSpeed
		};
	}

	private int GetCutsceneSimulatedInputSpeed(int normalSpeed, int slowSpeed)
	{
		if (_world.RidingObject)
			return normalSpeed;
		TerrainType terrain = _world.GetActiveTerrain(Position).Terrain.Type;
		return terrain switch
		{
			TerrainType.Grass => normalSpeed * 3 / 4,
			TerrainType.Stairs or TerrainType.Vines => slowSpeed,
			_ => normalSpeed
		};
	}

	private ActiveTerrainInfo ApplyTopDownTileTypes()
	{
		byte previousPosition = _world.UpdateActiveLinkTile(_precisePosition,_topDownAirborne);
		var active = _world.GetActiveTerrain(_precisePosition);
		if (_topDownAirborne)
			return active with { Terrain = active.Terrain with { Type = TerrainType.Normal,Hazard = HazardType.None } };
		UpdateRaisedFloorOffset();
		var memory = _inventory.RuntimeState;
		TerrainType type = (TerrainType)memory.ReadField("wActiveTileType");
		if (type == TerrainType.Puddle) ApplyTerrainWalkSoundParameter();
		TerrainType handler = type;
		if (type == TerrainType.CrackedFloor && !RingEffects.ProtectsCrackedFloor(_inventory) && _holePullCounter >= FloorBreaks.Floor.Updates)
		{
			_world.BreakCrackedFloor(memory.ReadField("wActiveTilePos"),FloorBreaks.Floor.Replacement);
			_holePullCounter = 0;
			handler = TerrainType.Hole;
		}
		else if (type == TerrainType.CrackedIce && FloorBreaks.Ice is { } ice)
		{
			if (_holePullCounter < ice.Updates) handler = TerrainType.Ice;
			else
			{
				_world.SetLinkTerrainTile(memory.ReadField("wActiveTilePos"),ice.Replacement);
				handler = TerrainType.Water;
			}
		}
		bool magnetic = (memory.ReadField("wMagnetGloveState") & 0x40) != 0;
		bool water = handler is TerrainType.Water or TerrainType.SeaWater or TerrainType.Whirlpool or
			TerrainType.UpCurrent or TerrainType.RightCurrent or TerrainType.DownCurrent or TerrainType.LeftCurrent;
		bool normal = handler is TerrainType.Normal or TerrainType.Puddle or TerrainType.RaisableFloor ||
			handler is TerrainType.Hole or TerrainType.WarpHole && (_world.Underwater || _world.RidingObject || magnetic) ||
			type == TerrainType.CrackedFloor && RingEffects.ProtectsCrackedFloor(_inventory) ||
			handler is >= TerrainType.UpConveyor and <= TerrainType.LeftConveyor && (_world.RidingObject || RingEffects.IgnoresQuicksand(_inventory)) ||
			water && _world.RidingObject ||
			handler == TerrainType.Lava && (memory.Game == OracleGame.Ages ? _world.RidingObject : magnetic);
		if (normal)
		{
			memory.SetField("wActiveTileType",0);
			memory.SetField("wStandingOnTileCounter",0);
			active = active with { Terrain = active.Terrain with { Hazard = HazardType.None } };
		}
		else if (handler is TerrainType.Hole or TerrainType.WarpHole &&
			memory.ReadField("wLastActiveTileType") == (byte)type && previousPosition != memory.ReadField("wActiveTilePos"))
			memory.SetField("wStandingOnTileCounter",0x0e);
		if (handler == TerrainType.Ice &&
			!RingEffects.IgnoresIce(_inventory)) memory.SetField("wIsTileSlippery",0x40);
		if (!normal && handler is TerrainType.Hole or TerrainType.WarpHole)
		{
			active = active with { Terrain = active.Terrain with { Hazard = HazardType.Hole } };
			if (!_pullingIntoHole) StartPullIntoHole(active);
			UpdatePullIntoHole(active);
		}
		else CancelHolePull();
		if (handler == TerrainType.Spike) ApplySpikeDamage(_topDownMovementAngle);
		ApplyTopDownTerrainMotion(handler,NativeAdjacentWalls);
		if (!normal && (water || handler == TerrainType.Lava))
		{
			HazardType hazard = handler == TerrainType.Lava ? HazardType.Lava : HazardType.Water;
			active = active with { Terrain = active.Terrain with { Hazard = hazard } };
			if ((hazard == HazardType.Lava || !_world.Underwater) && _topDownSwimmingState == 0)
			{
				_enemyKnockbackFrames = 0;
				_topDownSwimmingState = hazard == HazardType.Lava ? 0x41 : 1;
			}
		}
		Position = OracleObjectMath.ToPixelPosition(_precisePosition);
		return active;
	}

	private void UpdateRaisedFloorOffset()
	{
		// linkApplyTileTypes runs before item use and retains $cc69 while
		// airborne. Landing reevaluates the y+$05 active tile before movement.
		if (!_world.SideScrolling && !_topDownAirborne)
			_world.RaisedFloorOffset = _world.GetActiveTerrain(Position).Terrain.Type == TerrainType.RaisableFloor ? -3 : 0;
	}

	private void ApplyTerrainAtFeet()
	{
		if (_world.RidingObject)
		{
			CancelHolePull();
			return;
		}
		ActiveTerrainInfo activeTerrain = _world.GetActiveTerrain(Position);
		TerrainInfo terrain = activeTerrain.Terrain;
		if (terrain.Hazard != HazardType.None &&
			terrain.Hazard != HazardType.Water && terrain.Hazard != HazardType.Lava)
		{
			TriggerHazard(activeTerrain);
			return;
		}

		if (_pullingIntoHole && _holePullCounter < 16)
		{
			_pullingIntoHole = false;
			_holePullPackedPosition = -1;
		}
	}

	private void CancelHolePull()
	{
		_pullingIntoHole = false;
		_holePullPackedPosition = -1;
	}

	private void StartPullIntoHole(ActiveTerrainInfo activeTerrain)
	{
		_pullingIntoHole = true;
		_holePullCenter = activeTerrain.TileCenter;
		_holePullPackedPosition = activeTerrain.PackedPosition;
		_walking = false;
		QueueRedraw();
	}

	private bool UpdatePullIntoHole(ActiveTerrainInfo? sampled = null)
	{
		ActiveTerrainInfo activeTerrain = sampled ?? _world.GetActiveTerrain(Position);
		if (activeTerrain.Terrain.Hazard == HazardType.Hole)
		{
			if (activeTerrain.PackedPosition != _holePullPackedPosition)
			{
				_holePullCenter = activeTerrain.TileCenter;
				_holePullPackedPosition = activeTerrain.PackedPosition;
			}
		}
		else
		{
			_pullingIntoHole = false;
			_holePullPackedPosition = -1;
			return false;
		}

		_enemyKnockbackFrames = 0;
		if (_holePullCounter >= 16)
		{
			var memory = _inventory.RuntimeState;
			memory.SetField("wLinkImmobilized",(byte)(memory.ReadField("wLinkImmobilized") | 0x10));
		}

		// Port of linkPullIntoHole's visible movement: every fourth frame it
		// nudges vertically, the next frame horizontally, then waits two
		// frames. For the first 16 frames, Link still has partial control;
		// after that he is immobilized until he reaches the hole center.
		int phase = _holePullCounter & 0x03;
		if (phase == 0)
			_precisePosition.Y = MoveOnePixelToward(_precisePosition.Y, _holePullCenter.Y);
		else if (phase == 1)
			_precisePosition.X = MoveOnePixelToward(_precisePosition.X, _holePullCenter.X);

		Position = OracleObjectMath.ToPixelPosition(_precisePosition);

		// The ROM checks high-byte nibbles $07..$09 only on movement phases.
		if (phase < 2 && ((int)Position.X & 0x0f) is >= 7 and <= 9 &&
			((int)Position.Y & 0x0f) is >= 7 and <= 9)
		{
			StartFallInHole();
			return true;
		}

		if (_holePullCounter >= 16)
		{
			_walking = false;
			QueueRedraw();
			return true;
		}

		return false;
	}

	private void StartFallInHole()
	{
		_pullingIntoHole = false;
		_holePullPackedPosition = -1;
		// linkPullIntoHole selects state02; its initializer runs next update.
		_fallInHoleRespawning = false;
		_fallInHoleWarpPending = false;
		_walking = false;
		CancelSwordAttack();
		CancelShovelAction();

		ClearNativeItemParents();
		_inventory.RuntimeState.SetField("wLinkStateParameter",0);
		_forcedRespawnPhase=2;NativeLinkSubstate=0;_fallingInHole=true;
		QueueRedraw();
	}

	private static float MoveOnePixelToward(float value, float target)
	{
		// linkPullIntoHole increments even at equality and preserves the low byte.
		return Mathf.Floor(value) <= target ? value + 1.0f : value - 1.0f;
	}


	private void StartDrowning(HazardType hazard)
	{
		// forceDrownLink uses this same request for side-view lava. Ordinary
		// side-view water without Flippers has its separate swimming-state path.
		_inventory.RuntimeState.SetField("wLinkForceState",2);
		_inventory.RuntimeState.SetField("wLinkStateParameter",4);
		_inventory.RuntimeState.SetField("wcc92",0x80);
		_topDownDrownPhase = 1; // state01 still owns the request update.
		_instantRespawnCollisionDisabled = true;
		_enemyInvincibilityFrames = hazard == HazardType.Water ? -120 : 120;
		_drowningHazard = hazard;
		_drowning = true;
		_drownRespawning = false;
		_walking = false;
		CancelSwordAttack();
		CancelShovelAction();
		Visible = true;

		// overworldSwimmingState1 requests SND_DAMAGE_LINK ($5f) before it
		// selects LINK_ANIM_MODE_DROWN and creates the splash interaction.
		_world.PlaySound(SoundId.SndDamageLink);
		SetScriptedLinkAnimationMode(null);
		LinkNativeAnimationDatabase.For(AssetRoot).Set(_inventory.RuntimeState, 0x0a);
		_world.SpawnDrowningSplash(Position, hazard);
		QueueRedraw();
	}

	private void UpdateDrowning()
	{
		if (_topDownDrownPhase != 0)
		{
			UpdateTopDownDrowning();
			return;
		}
		if (_world.NativeTextActive) return; // Normal state01's gate precedes side-view swimming.
		SetNativePushAnimationOverride(0);
		_inventory.RuntimeState.SetField("wcc92",0x80);
		LinkNativeAnimationDatabase.For(AssetRoot).Animate(_inventory.RuntimeState);
		_instantRespawnCollisionDisabled = true;
		if ((_inventory.RuntimeState.ReadWramByte(0xd021) & 0x80) != 0)
		{
			// Side-view linkUpdateDrowning selects state02, without consuming another request.
			_sideScrollSwimmingState = 0x0f; // Source copies terminal animParameter$ff to swimming state.
			_inventory.RuntimeState.SetField("wLinkSwimmingState",0xff);
			_inventory.RuntimeState.SetField("wLinkStateParameter",2);
			_forcedRespawnPhase=2;NativeLinkSubstate=0;
		}
		QueueRedraw();
	}

	internal int DrownAnimationFrame => GetDrownAnimationFrame();

	private void UpdateTopDownDrowning()
	{
		if (_topDownDrownPhase == 2)
		{
			// parameter_drown resets the animation, then returns without advancing it.
			_topDownDrownPhase = 3;
			SetScriptedLinkAnimationMode(null);
			LinkNativeAnimationDatabase.For(AssetRoot).Set(_inventory.RuntimeState, 0x0a);
		}
		else if (_topDownDrownPhase == 3)
		{
			// substate5 checks the previous animation parameter before animating.
			if ((_inventory.RuntimeState.ReadWramByte(0xd021) & 0x80) == 0)
				LinkNativeAnimationDatabase.For(AssetRoot).Animate(_inventory.RuntimeState);
			else
			{
				_topDownDrownPhase = 4;
				_drownRespawning = true;
				BeginNativeRespawnWait(collisionsDisabled:_instantRespawnCollisionDisabled);
			}
		}
		QueueRedraw();
	}

	private int GetDrownAnimationFrame() => Math.Min(1,
		LinkNativeAnimationDatabase.For(AssetRoot).FrameIndex(_inventory.RuntimeState, 0x0a, 3));

	private void UpdateFallInHole()
	{
		if (_fallInHoleWarpPending)
			return;

		if (!_fallInHoleRespawning)
		{
			// substate1 checks the previous animation marker before animating.
			if ((_inventory.RuntimeState.ReadWramByte(0xd021) & 0x80) != 0)
			{
				ActiveTerrainInfo activeTerrain =
					_world.GetActiveTerrain(Position);
				if (activeTerrain.Terrain.Type == TerrainType.WarpHole)
				{
					// LINK_STATE_RESPAWNING tests wActiveTileType after the
					// animation marker. TILETYPE_WARPHOLE starts the shared
					// dungeon-floor warp immediately; only ordinary holes
					// enter the invisible two-update respawn branch.
					_fallInHoleWarpPending = true;
					_world.BeginFallDownHoleWarp(
						this,
						activeTerrain.PackedPosition);
					QueueRedraw();
					return;
				}
				_fallInHoleRespawning = true;
				BeginNativeRespawnWait(collisionsDisabled:_instantRespawnCollisionDisabled);
			}
			else
				LinkNativeAnimationDatabase.For(AssetRoot).Animate(_inventory.RuntimeState);
			QueueRedraw();
			return;
		}

	}

	private void BeginRespawnRecovery()
	{
		// linkState02 substate2 reveals Link without clearing a later force-state request.
		_topDownAirborne=false;_sideScrollAirborne=false;
		_topDownSwimmingState=0;_sideScrollSwimmingState=0;
		_inventory.RuntimeState.SetField("wLinkSwimmingState",0);
		_pendingContactDamageRaw=0;
		ApplyDamage(TerrainHazardDamageQuarters,RingDamageSource.Hole);
		_enemyInvincibilityFrames=0x3c;NativeLinkSubstate=3;NativeLinkCounter=0x10;_forcedRespawnPhase=3;
		_fallingInHole=false;_fallInHoleRespawning=false;_fallInHoleWarpPending=false;
		_drowning=false;_drownRespawning=false;_topDownDrownPhase=0;
		_airborneLinkAnimationMode=AirborneLinkAnimationMode.None;
		_walking=false;Visible=true;
		NativeAdjacentWalls=_world.GetAdjacentWallsBitset(_precisePosition);
		ResetLinkWalkAnimation();QueueRedraw();
	}

	private void RestoreNormalGroundFlags()
	{
		// linkState01@notInAir enables collisions after the magnet/recoil
		// gates, then reveals ordinary grounded Link before its movement.
		if (_world.SideScrolling || _topDownAirborne || _enemyKnockbackFrames != 0 ||
			(_inventory.RuntimeState.ReadField("wMagnetGloveState") & 0x40) != 0) return;
		_instantRespawnCollisionDisabled = false;
		if (!TopDownSwimming) Visible = true;
	}

	private void InitializeHoleFallPresentation(bool center)
	{
		_instantRespawnCollisionDisabled=true;
		if(center)
		{
			_holePullCenter=new(((int)_precisePosition.X&0xf0)+8,((int)_precisePosition.Y&0xf0)+8);
			_precisePosition=_holePullCenter;Position=OracleObjectMath.ToPixelPosition(_precisePosition);
		}
		SetScriptedLinkAnimationMode(null);
		LinkNativeAnimationDatabase.For(AssetRoot).Set(_inventory.RuntimeState, 0x0d);
		_world.PlaySound(SoundId.SndLinkFall);QueueRedraw();
	}

	private int GetFallInHoleFrame() => Math.Min(2,
		LinkNativeAnimationDatabase.For(AssetRoot).FrameIndex(_inventory.RuntimeState, 0x0d, 4));

	// parentItemUsage.s:checkUseItems dispatches sidescroll before underwater.
	// Top-down underwater rooms allocate only A; B remains available to
	// link.s:checkForUnderwaterTransition regardless of its equipped item.
	// Existing parents still update and read their original button inputs.
	private bool SecondaryItemAllocationSuppressed => _world.Underwater && !_world.SideScrolling;

	internal int SeedSatchelFrame => _seedSatchelFrame;

	private void UpdateHeartRingCounter(Vector2 movement)
	{
		// linkUpdateMovement calls this only after an accepted displacement.
		// Idle/blocked updates preserve even a counter belonging to an old ring.
		if (movement == Vector2.Zero) return;
		(int threshold, int heal) = RingEffects.HeartRefill(_inventory);
		if (threshold == 0)
		{
			_heartRingDistanceFixed = 0;
			return;
		}
		int distanceFixed = Mathf.RoundToInt(
			(Mathf.Abs(movement.X) + Mathf.Abs(movement.Y)) * 256.0f);
		// wHeartRingCounter is a three-byte unsigned distance accumulator.
		_heartRingDistanceFixed = (_heartRingDistanceFixed + distanceFixed) & 0xffffff;
		if (_heartRingDistanceFixed < threshold)
			return;
		// The native caller skips giveTreasure at full health; otherwise its
		// obtained flag and Gasha maturity accompany the health increment.
		if (_inventory.HealthQuarters < _inventory.MaxHealthQuarters)
			_inventory.GiveTreasure(TreasureId.HeartRefill, heal);
		_heartRingDistanceFixed = 0;
	}

	internal (Texture2D Texture, Vector2 Offset) NativeLinkBodyFrame => Sprites.NativeFrame(
		_inventory.RuntimeState.ReadWramByte(0xd031), (int)_facing, DamagePaletteActive);

	internal (Texture2D Texture, Rect2 Region) CurrentWalkBodyFrame =>
		SelectWalkBodyFrame(GetWalkAnimationFrame());

	internal Vector2 AttackSpriteOrigin
	{
		get
		{
			if (_minecartRideControlled &&
				_swordState == SwordActionState.Swing)
			{
				return NormalSpriteOrigin;
			}
			Facing poseFacing = GetSwordPoseFacing();
			int phase = GetSwordPosePhase();
			Vector2 poseOffset = _swordState == SwordActionState.Spin || phase == 2
				? _linkItems.AttackPoseOffset((int)poseFacing)
				: Vector2.Zero;
			return NormalSpriteOrigin + poseOffset;
		}
	}

	internal Vector2 SwordSpritePosition
	{
		get => GetSwordSpritePosition(GetSwordArcIndex(), _linkItems);
	}
}

internal readonly record struct SwordPart(int Y, int X, int Tile, bool FlipX = false, bool FlipY = false);

internal readonly record struct SwordArc(int RadiusY, int RadiusX, int OffsetY, int OffsetX);

internal enum PlayerGroundDrawPass
{
	Body,
	TerrainEffect
}

internal enum SwordActionState
{
	None,
	Swing,
	Held,
	Charged,
	Poke,
	Spin
}

internal enum AirborneLinkAnimationMode
{
	None,
	Walk,
	Jump,
	RocsCape,
	SideScrollSwim
}

internal enum Facing
{
	Up,
	Right,
	Down,
	Left
}

internal enum BraceletActionPose
{
	Pull,
	PullStrain,
	Throw,
	LiftHigh
}

internal enum LedgeJumpState
{
	None,
	Airborne,
	AirborneBeforeScroll,
	WaitingForScroll,
	AirborneAfterScroll
}
