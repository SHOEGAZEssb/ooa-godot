
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
    private readonly Texture2D?[] _scriptedJumpTextures = new Texture2D?[32];
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
        Started(TreasureId.Boomerang, IsUsingBoomerang) ||
        Started(TreasureId.Bombchus, true) ||
        Started(0x02, IsUsingPunch);
    private bool Started(int id, bool active) => active && (_startedParentItemAnimations & (1u << id)) != 0;
    // setLinkUsingItem1's low bit lasts until clearParentItem; its paired
    // high pulse cannot outlive that parent. Sample animation parent owners,
    // including Switch Hook's controller rather than its finished child.
    internal bool NativeItemUseActive =>
        Signals(TreasureId.Sword,IsAttacking) || Signals(TreasureId.Shovel,IsUsingShovel) ||
        Signals(TreasureId.SeedSatchel,IsUsingSeedSatchel) || Signals(TreasureId.Shooter,IsUsingSeedShooter) ||
        Signals(TreasureId.SwitchHook,IsUsingSwitchHook) || Signals(TreasureId.CaneOfSomaria,IsUsingSomaria) ||
        Signals(TreasureId.BiggoronSword,IsUsingBiggoron) || Signals(TreasureId.Boomerang,IsUsingBoomerang) ||
        Signals(TreasureId.Punch,IsUsingPunch);
    private bool Signals(int id,bool active) => active && _linkItems.ParentAnimationSignalsItemUse(id);
    internal void NotifyParentItemAnimationStarted(int id, int slot = 2)
    {
        ParentItemAnimationState.Begin(_inventory.RuntimeState, slot, id, _linkItems.ParentAnimationPriority(id));
        if (_linkItems.ParentAnimationSignalsItemUse(id))
            _startedParentItemAnimations |= 1u << id;
    }
    private bool IsUsingItem => IsAttacking || OtherItemAnimationActive;
    private bool OtherItemAnimationActive =>
        IsUsingShovel || IsUsingSeedSatchel ||
        IsUsingSeedShooter || IsUsingHarp || IsUsingPunch || IsUsingSwitchHook || IsUsingSomaria || IsUsingBiggoron || IsUsingBoomerang ||
        IsUsingBombchu || Started(TreasureId.Bombchus, true);
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
    internal void ClearNativeItemParents()
    {
        CancelNativeItemUsage();
    }
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

    internal int AdvanceRaftKnockback()
    {
        if (!_raftRideControlled || _enemyKnockbackFrames <= 0)
            return 0xff;
        // SPECIALOBJECT_RAFT decrements Link's counter and still moves on zero.
        _enemyKnockbackFrames = Mathf.Max(0, _enemyKnockbackFrames - 1);
        return _enemyKnockbackAngle;
    }

    internal void DisableInstruments(int frames) => _inventory.RuntimeState.SetField("wInstrumentsDisabledCounter", checked((byte)frames));
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
    internal ulong BraceletActionPixelHash(int pose, int direction)
    {
        if (pose is < 0 or >= 3)
            throw new ArgumentOutOfRangeException(nameof(pose));
        if (direction is < 0 or >= 4)
            throw new ArgumentOutOfRangeException(nameof(direction));
        return OracleGraphicsCache.PixelHash(
            Sprites.BraceletActionTextures[pose, direction].GetImage());
    }

    public event Action? GameOverRequested;
    internal bool ApplicationUpdateOwned { get; set; }

    private bool _initialPresentationPrepared;

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
        _lastSafePosition = spawn;
        _localRespawnFacing = _facing;
        ResetLinkWalkAnimation();
        Position = OracleObjectMath.ToPixelPosition(spawn);
        _activeTransformation = _ringTransformationRequested = 0;
        _transformationFrame = _transformationTicks = 0;
        QueueRedraw();
    }

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

    /// <summary>
    /// Selects LINK_ANIM_MODE_HARP_2 ($1e). The imported frames include the
    /// first frame entered after the fourth 52-update phrase, which remains
    /// visible until INTERAC_PLAY_HARP_SONG reaches state 6 on the next update.
    /// </summary>
    internal void BeginHarpPose()
    {
        _harpRenderer ??= new CutsceneSpriteRenderer(AssetRoot);
        _cutsceneHarpFrames ??=
            new NewGameIntroDatabase(AssetRoot).SpriteFrames("link-harp");
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
            _itemHarpFrames ??= new NewGameIntroDatabase(AssetRoot).SpriteFrames("link-harp-item");
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
        _cutsceneSpriteRenderer ??= new CutsceneSpriteRenderer(AssetRoot);
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

    private static readonly PlayerGroundDrawPass[] GroundDrawOrder =
    [
        PlayerGroundDrawPass.Body,
        PlayerGroundDrawPass.TerrainEffect
    ];

    internal static ReadOnlySpan<PlayerGroundDrawPass> GroundDrawPasses =>
        GroundDrawOrder;

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
            0x18 => _scriptedJumpTextures[_topDownAirAnimationPhase*8+(int)_facing+(damagePalette?4:0)] ??= new AtlasTexture
                {Atlas=damagePalette?Sprites.DamageLedgeJumpTexture:Sprites.LedgeJumpTexture,
                    Region=new Rect2(_topDownAirAnimationPhase*16,(int)_facing*16,16,16)},
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

    private void AdvanceSideScrollAirAnimation()
    {
        if (_airborneLinkAnimationMode == AirborneLinkAnimationMode.SideScrollSwim)
        {
            AdvanceSideScrollSwimAnimation();
            return;
        }
        AdvanceNativeLinkAnimation();
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

    private static int SideScrollHorizontalAngle(int angle) =>
        angle switch
        {
            >= 0x01 and <= 0x0f => 0x08,
            >= 0x11 and <= 0x1f => 0x18,
            _ => 0xff
        };

    private static bool IsUpwardAngle(int angle) =>
        angle < 0x80 && ((angle + 4) & ObjectAngle.Mask) < 9;

    private static int FractionalByte(float coordinate) =>
        Mathf.FloorToInt(coordinate * 256.0f) & 0xff;

    private void UpdateFacingFromSideScrollAngle(int angle)
    {
        int horizontal = SideScrollHorizontalAngle(angle);
        if (horizontal == 0x08)
            _facing = Facing.Right;
        else if (horizontal == 0x18)
            _facing = Facing.Left;
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

    private static Rect2 GetFrame(Facing facing, int frame)
    {
        return new Rect2(frame * 16, (int)facing * 16, 16, 16);
    }

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

    // parentItemUsage.s:checkUseItems dispatches sidescroll before underwater.
    // Top-down underwater rooms allocate only A; B remains available to
    // link.s:checkForUnderwaterTransition regardless of its equipped item.
    // Existing parents still update and read their original button inputs.
    private bool SecondaryItemAllocationSuppressed => _world.Underwater && !_world.SideScrolling;

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
            Input.IsActionJustPressed("attack") && !_world.SwordDisabled)
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
            Input.IsActionJustPressed("item") && !_world.SwordDisabled)
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

    public void StartSwordAttack() => StartSwordAttack(null, Vector2.Zero);

    internal void StartSwordAttackForValidation(Vector2 facingInput) =>
        StartSwordAttack(null, facingInput);

    private void StartSwordAttack(string? buttonAction, Vector2 facingInput)
    {
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
        if (IsUsingItem && !IsUsingSomaria && !IsUsingBiggoron && !IsUsingBombchu && !IsUsingBoomerang || IsUsingSeedSatchel)
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

    internal int SeedSatchelFrame => _seedSatchelFrame;

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
        if (_airborneLinkAnimationMode == AirborneLinkAnimationMode.RocsCape)
        {
            var frame = Sprites.CapeFrame(AirborneLinkBodyFrame,(int)_facing,DamagePaletteActive);
            DrawTexture(frame.Texture,frame.Offset + drawOffset);
            return;
        }
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

        DrawTextureRectRegion(
            DamagePaletteActive
                ? Sprites.DamageLedgeJumpTexture
                : Sprites.LedgeJumpTexture,
            new Rect2(
                NormalSpriteOrigin + drawOffset,
                new Vector2(16, 16)),
            new Rect2(
                AirborneLinkBodyFrame * 16,
                (int)_facing * 16,
                16,
                16));
    }

    private void DrawWalkLinkBody(int frame, Vector2 drawOffset)
    {
        var body = SelectWalkBodyFrame(frame);
        DrawTextureRectRegion(
            body.Texture,
            new Rect2(NormalSpriteOrigin + drawOffset, new Vector2(16, 16)),
            body.Region);
    }

    internal (Texture2D Texture, Rect2 Region) CurrentWalkBodyFrame =>
        SelectWalkBodyFrame(GetWalkAnimationFrame());

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
