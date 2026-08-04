using Godot;
using System;

namespace oracleofages;

public partial class Player : Node2D
{
    internal const int NormalZIndex = 10;
    internal const int DivingZIndex = 8;
    internal const int AlternateTextboxPaletteZIndex = 13;
    private const int NormalTopDownSpeed = 0x28;
    private const int GrassTopDownSpeed = 0x1e;
    private const int StairsTopDownSpeed = 0x14;
    private static readonly Vector2 NormalSpriteOrigin = new(-8, -8);
    private const int PunchCollisionFrames = 4;
    private const int FistPunchFrames = 8;
    private const int ExpertPunchFrames = 14;
    private const float DrownAnimationDuration = 22.0f / 60.0f;
    private const float DrownInvisibleDuration = 2.0f / 60.0f;
    private const float FallInHoleAnimationDuration = 36.0f / 60.0f;
    private const float FallInHoleInvisibleDuration = 2.0f / 60.0f;
    private const float HazardRecoveryDuration = 16.0f / 60.0f;
    private const float EnemyKnockbackSpeed = 1.25f;
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
    private InventoryState _inventory = null!;
    private OracleRandom _random = null!;
    private Texture2D _texture = null!;
    private Texture2D _damageTexture = null!;
    private Texture2D _getItemOneHandTexture = null!;
    private Texture2D _damageGetItemOneHandTexture = null!;
    private Texture2D _getItemTwoHandTexture = null!;
    private Texture2D _damageGetItemTwoHandTexture = null!;
    private Texture2D _funnyJokeDanceLeftTexture = null!;
    private Texture2D _damageFunnyJokeDanceLeftTexture = null!;
    private Texture2D _funnyJokeDanceRightTexture = null!;
    private Texture2D _damageFunnyJokeDanceRightTexture = null!;
    private Texture2D _getItemOneHandRightTexture = null!;
    private Texture2D _damageGetItemOneHandRightTexture = null!;
    private Texture2D _carriedObjectTexture = null!;
    private Texture2D _damageCarriedObjectTexture = null!;
    private Texture2D _minecartLinkTexture = null!;
    private Texture2D _damageMinecartLinkTexture = null!;
    private Texture2D _minecartAttackTexture = null!;
    private Texture2D _damageMinecartAttackTexture = null!;
    private Texture2D? _companionRideTexture;
    private Texture2D? _damageCompanionRideTexture;
    private Vector2 _companionRideTextureOffset;
    private int _companionRideZFixed;
    private Texture2D[,] _braceletActionTextures = null!;
    private Texture2D[,] _damageBraceletActionTextures = null!;
    private Texture2D _shieldLinkTexture = null!;
    private Texture2D _damageShieldLinkTexture = null!;
    private Texture2D _pushTexture = null!;
    private Texture2D _damagePushTexture = null!;
    private Texture2D _attackTexture = null!;
    private Texture2D _damageAttackTexture = null!;
    private Texture2D _swordTexture = null!;
    private Texture2D _chargedSwordTexture = null!;
    private Texture2D _shovelLinkTexture = null!;
    private Texture2D _damageShovelLinkTexture = null!;
    private Texture2D _drownTexture = null!;
    private Texture2D _damageDrownTexture = null!;
    private Texture2D _topDownSwimTexture = null!;
    private Texture2D _damageTopDownSwimTexture = null!;
    private Texture2D _topDownDiveTexture = null!;
    private Texture2D _damageTopDownDiveTexture = null!;
    private Texture2D _fallInHoleTexture = null!;
    private Texture2D _damageFallInHoleTexture = null!;
    private Texture2D _ledgeJumpTexture = null!;
    private Texture2D _damageLedgeJumpTexture = null!;
    private Texture2D _sideScrollSquishXTexture = null!;
    private Texture2D _sideScrollSquishYTexture = null!;
    private Texture2D _terrainShadowTexture = null!;
    private Vector2 _terrainShadowOffset;
    private LinkItemDatabase _linkItems = null!;
    private TopDownSwimmingDatabase _topDownSwimmingData = null!;
    private LinkTerrainEffectDatabase _terrainEffects = null!;
    private int _terrainWalkUpdates;
    private bool _terrainWalkSoundParameter;
    private Texture2D _deathTexture = null!;
    private TransformedLinkDatabase _transformedLink = null!;
    private Vector2 _precisePosition;
    private Vector2 _minecartMainObjectPosition;
    private Vector2 _lastSafePosition;
    private HazardType _drowningHazard;
    private Facing _facing = Facing.Down;
    private Facing _localRespawnFacing = Facing.Down;
    private int _linkWalkAnimationFrame;
    private int _linkWalkAnimationCounter = 2;
    private AirborneLinkAnimationMode _airborneLinkAnimationMode;
    private double _swordFrameAccumulator;
    private double _shovelFrameAccumulator;
    private double _seedSatchelFrameAccumulator;
    private double _harpFrameAccumulator;
    private double _punchFrameAccumulator;
    private float _drownTime;
    private float _drownInvisibleTime;
    private float _hazardRecoveryTime;
    private float _fallInHoleTime;
    private float _fallInHoleInvisibleTime;
    private double _ledgeUpdateAccumulator;
    private double _sideScrollUpdateAccumulator;
    private double _topDownAirUpdateAccumulator;
    private bool _minecartJumpControlled;
    private bool _minecartRideControlled;
    private bool _companionJumpControlled;
    private bool _companionDismountJump;
    private bool _companionJumpAnimationDeferred;
    private bool _companionRideControlled;
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
    private int _sideScrollMermaidImpulseCounter;
    private int _sideScrollBubbleCounter;
    private SideScrollTileType _sideScrollPreviousActiveType;
    private string? _rocsCapeButtonAction;
    private bool _sideScrollReducedGravity;
    private int _sideScrollInstantRespawnCounter;
    private bool _sideScrollSquishPending;
    private bool _sideScrollSquishVertical;
    private int _sideScrollSquishAnimationCounter;
    private int _sideScrollSquishFlickerCounter;
    private int _sideScrollAnimationPhase;
    private int _sideScrollAnimationCounter;
    private bool _sideScrollAirborne;
    private bool _sideScrollJumpSoundPending;
    private bool _sideScrollClimbing;
    private int _topDownAirZFixed;
    private int _topDownAirSpeedZ;
    private int _topDownAirAngle = 0xff;
    private int _topDownAirSpeedRaw;
    private int _topDownAirAnimationPhase;
    private int _topDownAirAnimationCounter;
    private bool _topDownAirborne;
    private bool _topDownJumpSoundPending;
    private int _topDownSwimmingState;
    private int _topDownSwimmingEntryCounter;
    private int _topDownSwimAngle = 0xff;
    private int _topDownSwimSpeedRaw;
    private int _topDownSwimTargetSpeedRaw;
    private int _topDownSwimVelocityCounter;
    private int _topDownSwimBurstState;
    private int _topDownSwimBurstCounter;
    private int _topDownSwimAnimationFrame;
    private int _topDownSwimAnimationCounter;
    private bool _topDownDiving;
    private int _topDownDiveCounter;
    private float _enemyInvincibilityFrames;
    private float _enemyKnockbackFrames;
    private Vector2 _enemyKnockbackDirection;
    private int _pendingSwordKnockbackFrames;
    private Vector2 _pendingSwordKnockbackDirection;
    private bool _swordCollisionKnockback;
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
    private int _holePullCounter;
    private int _holePullPackedPosition = -1;
    private bool _fallInHoleWarpPending;
    private SwordActionState _swordState;
    private int _swordStateFrame;
    private int _swordChargeCounter;
    private int _currentSwordDamage;
    private bool _swordPokeReturnsToHeld;
    private bool _doubleEdgedDamagePending;
    private int _heartRingDistanceFixed;
    private int _activeTransformation;
    private int _transformationFrame;
    private int _transformationTicks;
    private string? _swordButtonAction;
    private int _shovelFrame;
    private bool _usingShovel;
    private bool _usingSeedSatchel;
    private bool _usingHarp;
    private int _harpActionUpdate;
    private int _harpActionFrames;
    private int _harpSong;
    private int _seedSatchelFrame;
    private int _seedSatchelActionFrames;
    private bool _usingPunch;
    private bool _expertPunch;
    private int _punchFrame;
    private int _punchDamage;
    private int _shieldParentButton;
    private bool _shieldParentInitialized;
    private bool _usingShield;
    private Vector2 _lastMovementInput;
    private bool _walking;
    private bool _pushing;
    private bool _pullingIntoHole;
    private bool _drowning;
    private bool _drownRespawning;
    private bool _fallingInHole;
    private bool _fallInHoleRespawning;
    private bool _cutsceneControlled;
    private bool _getItemOneHandPose;
    private bool _getItemTwoHandPose;
    private int? _scriptedLinkAnimationMode;
    private bool _carriedObjectPose;
    private BraceletActionPose? _braceletActionPose;
    private bool _braceletLiftCollisionsDisabled;
    private CutsceneSpriteRenderer? _newGameFallRenderer;
    private IntroSpriteFrame[]? _newGameFallFrames;
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
    private IntroSpriteFrame[]? _harpFrames;
    private IntroSpriteFrame[]? _cutsceneHarpFrames;
    private IntroSpriteFrame[]? _itemHarpFrames;
    private int _harpFrame;
    private int _harpFrameTicks;
    private bool _harpPoseActive;
    private int _floorDoorRespawnCounter;
    private int _floorDoorRecoveryCounter;

    public int HealthQuarters => _inventory.HealthQuarters;
    public int Rupees => _inventory.Rupees;
    public int MaxHealthQuarters => _inventory.MaxHealthQuarters;
    public bool IsDying => _deathPending || _deathAnimationActive;
    internal bool BraceletLiftCollisionsDisabled =>
        _braceletLiftCollisionsDisabled;
    public InventoryState Inventory => _inventory;
    public bool IsPullingIntoHole => _pullingIntoHole;
    public bool IsDrowning => _drowning;
    public bool IsFallingInHole => _fallingInHole;

    public Vector2I FacingVector => FacingVectorFor(_facing);
    public bool IsAttacking => _swordState != SwordActionState.None;
    public bool IsUsingShovel => _usingShovel;
    public bool IsUsingSeedSatchel => _usingSeedSatchel;
    public bool IsUsingHarp => _usingHarp;
    public bool IsUsingShield => _usingShield;
    internal bool IsUsingPunch => _usingPunch;
    private bool IsUsingItem =>
        IsAttacking || IsUsingShovel || IsUsingSeedSatchel || IsUsingHarp ||
        IsUsingPunch;
    internal bool IsPushing => _pushing;
    internal SwordActionState SwordState => _swordState;
    internal int SwordStateFrame => _swordStateFrame;
    internal int SwordDamage => _swordState == SwordActionState.Spin
        ? _currentSwordDamage * 2
        : IsUsingPunch ? _punchDamage : _currentSwordDamage;
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
    // current Link.zh minus two. Ledge hops cancel the weapon parent, while
    // Roc's Feather and minecart jumps retain their top-down object height.
    internal int MeleeItemZ => (_topDownAirborne ? TopDownAirZ : 0) - 2;
    internal bool UsesAirborneSwordPose =>
        IsAttacking && (_sideScrollAirborne || _topDownAirborne);
    internal bool AirborneLinkUsesJumpAnimation =>
        _airborneLinkAnimationMode == AirborneLinkAnimationMode.Jump;
    internal int AirborneLinkBodyFrame => _airborneLinkAnimationMode switch
    {
        AirborneLinkAnimationMode.Walk => _linkWalkAnimationFrame,
        AirborneLinkAnimationMode.Jump when _topDownAirborne =>
            _topDownAirAnimationPhase,
        AirborneLinkAnimationMode.Jump when _sideScrollAirborne =>
            _sideScrollAnimationPhase,
        _ => 0
    };
    internal int LinkAnimationCounter => _airborneLinkAnimationMode ==
        AirborneLinkAnimationMode.Walk
            ? _linkWalkAnimationCounter
            : _topDownAirborne
                ? _topDownAirAnimationCounter
                : _sideScrollAirborne
                    ? _sideScrollAnimationCounter
                    : _linkWalkAnimationCounter;
    internal Vector2 SwordDrawOffset =>
        new(0, _topDownAirborne ? TopDownAirZ : 0);
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
        !_deathAnimationActive && _enemyInvincibilityFrames > 0.0f &&
        (_world.FrameCounter & 0x04) == 0;
    internal bool DeathAnimationActive => _deathAnimationActive;
    internal int DeathAnimationFrame => _deathAnimationFrame;
    internal int DeathAnimationCounter => _deathAnimationCounter;
    internal int DeathAnimationSequenceIndex => _deathSequenceIndex;
    internal int DeathSpinLoopsRemaining => _deathSpinLoopsRemaining;
    internal ulong DeathAtlasPixelHash =>
        OracleGraphicsCache.PixelHash(_deathTexture.GetImage());
    internal ulong LinkAtlasPixelHash =>
        OracleGraphicsCache.PixelHash(_texture.GetImage());
    internal ulong DamageLinkAtlasPixelHash =>
        OracleGraphicsCache.PixelHash(_damageTexture.GetImage());
    internal bool IsNewGameSlowFalling => _newGameSlowFalling;
    internal bool IsRoomWarpFalling => _roomWarpFallActive;
    internal bool RoomWarpFallCollapsed => _roomWarpFallCollapsed;
    internal int RoomWarpFallZ => _roomWarpFallZFixed >> 8;
    internal bool IsGroundedForFloorButton =>
        _ledgeJumpState == LedgeJumpState.None && !_newGameSlowFalling &&
        !_sideScrollAirborne && !_topDownAirborne &&
        !TopDownSwimming && !_drowning && !_fallingInHole;
    internal bool AcceptsRoomEntityContact =>
        _ledgeJumpState == LedgeJumpState.None && !_topDownAirborne &&
        !TopDownDiving && !IsUsingHarp;
    internal int LedgeZ => _ledgeZFixed >> 8;
    internal int LedgeSpeedZ => _ledgeSpeedZ;
    internal int LedgeSpeedRaw => _ledgeSpeedRaw;
    internal int LedgeCliffLength => _ledgeCliffLength;
    internal int LedgeAnimationPhase => _ledgeAnimationPhase;
    internal bool SideScrollAirborne => _sideScrollAirborne;
    internal bool SideScrollClimbing => _sideScrollClimbing;
    internal int SideScrollSpeedZ => _sideScrollSpeedZ;
    internal int SideScrollYFixed => _sideScrollYFixed;
    internal int SideScrollAngle => _sideScrollAngle;
    internal int SideScrollSpeedRaw => _sideScrollSpeedRaw;
    internal int SideScrollAnimationPhase => _sideScrollAnimationPhase;
    internal bool SideScrollSwimming => _sideScrollSwimmingState != 0;
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
        OracleGraphicsCache.PixelHash(_topDownSwimTexture.GetImage());
    internal ulong TopDownDiveAtlasPixelHash =>
        OracleGraphicsCache.PixelHash(_topDownDiveTexture.GetImage());
    internal bool SideScrollSquished =>
        _sideScrollSquishPending ||
        _sideScrollSquishAnimationCounter != 0 ||
        _sideScrollSquishFlickerCounter != 0;
    internal bool DelaysOrdinaryScreenTransition =>
        _sideScrollAirborne || _topDownAirborne;
    internal bool RejectsOrdinaryScreenTransition =>
        _enemyKnockbackFrames > 0.0f || _drowning || _fallingInHole ||
        _sideScrollInstantRespawnCounter != 0 || SideScrollSquished;
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
    internal int TopDownAirSpeedZ => _topDownAirSpeedZ;
    internal int TopDownAirAnimationPhase => _topDownAirAnimationPhase;
    internal bool MinecartJumpActive => _minecartJumpControlled;
    internal bool MinecartRideActive => _minecartRideControlled;
    internal bool CompanionJumpActive => _companionJumpControlled;
    internal bool CompanionRideActive => _companionRideControlled;
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
        _floorDoorRespawnCounter != 0 || _floorDoorRecoveryCounter != 0;
    internal int FloorDoorRespawnCounter => _floorDoorRespawnCounter;
    internal Vector2 LocalRespawnPosition => _lastSafePosition;
    internal Vector2I LocalRespawnFacingVector =>
        FacingVectorFor(_localRespawnFacing);

    internal void SetLocalRespawnPosition(Vector2 position)
    {
        _lastSafePosition = position;
        _localRespawnFacing = _facing;
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
    internal bool IsHoldingItemOneHand => _getItemOneHandPose;
    internal bool IsHoldingItemTwoHands => _getItemTwoHandPose;
    internal int? ScriptedLinkAnimationMode => _scriptedLinkAnimationMode;
    internal ulong ScriptedLinkAnimationPixelHash =>
        _scriptedLinkAnimationMode is int mode
            ? OracleGraphicsCache.PixelHash(
                ScriptedLinkAnimationTexture(mode, damagePalette: false).GetImage())
            : 0;
    internal bool IsCarryingObject => _carriedObjectPose;
    internal Vector2 PrecisePosition => _precisePosition;
    internal int CarriedObjectAnimationFrame => GetWalkAnimationFrame();
    internal Vector2I? BraceletEntityOffset { get; private set; }
    internal int ShovelFrame => _shovelFrame;
    internal bool ShovelChildActive =>
        IsUsingShovel &&
        _shovelFrame >= _linkItems.Constants.ShovelDigFrame &&
        _shovelFrame < _linkItems.Constants.ShovelSecondPoseFrame;
    internal Vector2 ShovelChildOffset =>
        _linkItems.ShovelOffset((int)_facing);
    internal int ActiveTransformation => _activeTransformation;
    internal int TransformationFrame => _transformationFrame;
    internal int PunchFrame => _punchFrame;
    internal bool IsShieldEquipped => _inventory.ShieldLevel > 0 &&
        (_inventory.EquippedA == InventoryState.ItemShield ||
         _inventory.EquippedB == InventoryState.ItemShield);
    internal int ShieldGraphicsIndex
    {
        get
        {
            if (!IsShieldEquipped)
                return -1;
            int variant = (_usingShield ? 2 : 0) +
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
        OracleGraphicsCache.PixelHash(_shieldLinkTexture.GetImage());
    internal ulong AttackAtlasPixelHash =>
        OracleGraphicsCache.PixelHash(_attackTexture.GetImage());
    internal ulong ShovelAtlasPixelHash =>
        OracleGraphicsCache.PixelHash(_shovelLinkTexture.GetImage());
    internal ulong MinecartLinkAtlasPixelHash =>
        OracleGraphicsCache.PixelHash(_minecartLinkTexture.GetImage());
    internal ulong MinecartAttackAtlasPixelHash =>
        OracleGraphicsCache.PixelHash(_minecartAttackTexture.GetImage());
    internal ulong SwordAtlasPixelHash =>
        OracleGraphicsCache.PixelHash(_swordTexture.GetImage());
    internal ulong BraceletActionPixelHash(int pose, int direction)
    {
        if (pose is < 0 or >= 3)
            throw new ArgumentOutOfRangeException(nameof(pose));
        if (direction is < 0 or >= 4)
            throw new ArgumentOutOfRangeException(nameof(direction));
        return OracleGraphicsCache.PixelHash(
            _braceletActionTextures[pose, direction].GetImage());
    }

    public event Action? GameOverRequested;
    internal bool ApplicationUpdateOwned { get; set; }

    internal void Initialize(
        IPlayerWorld world,
        InventoryState inventory,
        Vector2 spawn,
        OracleRandom random)
    {
        _world = world;
        _inventory = inventory;
        _random = random;
        _linkItems = LinkItemDatabase.Shared;
        _topDownSwimmingData = TopDownSwimmingDatabase.Shared;
        _texture = BuildLinkTexture(damagePalette: false);
        _damageTexture = BuildLinkTexture(damagePalette: true);
        _getItemOneHandTexture = BuildGetItemOneHandTexture(damagePalette: false);
        _damageGetItemOneHandTexture = BuildGetItemOneHandTexture(damagePalette: true);
        _getItemTwoHandTexture = BuildGetItemTwoHandTexture(damagePalette: false);
        _damageGetItemTwoHandTexture = BuildGetItemTwoHandTexture(damagePalette: true);
        _funnyJokeDanceLeftTexture = BuildFunnyJokeDanceTexture(
            right: false, damagePalette: false);
        _damageFunnyJokeDanceLeftTexture = BuildFunnyJokeDanceTexture(
            right: false, damagePalette: true);
        _funnyJokeDanceRightTexture = BuildFunnyJokeDanceTexture(
            right: true, damagePalette: false);
        _damageFunnyJokeDanceRightTexture = BuildFunnyJokeDanceTexture(
            right: true, damagePalette: true);
        _getItemOneHandRightTexture = BuildGetItemOneHandRightTexture(
            damagePalette: false);
        _damageGetItemOneHandRightTexture = BuildGetItemOneHandRightTexture(
            damagePalette: true);
        _carriedObjectTexture = BuildCarriedObjectLinkTexture(damagePalette: false);
        _damageCarriedObjectTexture = BuildCarriedObjectLinkTexture(damagePalette: true);
        _minecartLinkTexture = BuildMinecartLinkTexture(damagePalette: false);
        _damageMinecartLinkTexture = BuildMinecartLinkTexture(damagePalette: true);
        _minecartAttackTexture = BuildMinecartAttackTexture(damagePalette: false);
        _damageMinecartAttackTexture = BuildMinecartAttackTexture(damagePalette: true);
        _braceletActionTextures = BuildBraceletActionTextures(damagePalette: false);
        _damageBraceletActionTextures = BuildBraceletActionTextures(damagePalette: true);
        _shieldLinkTexture = BuildShieldLinkTexture(damagePalette: false);
        _damageShieldLinkTexture = BuildShieldLinkTexture(damagePalette: true);
        _pushTexture = BuildPushLinkTexture(damagePalette: false);
        _damagePushTexture = BuildPushLinkTexture(damagePalette: true);
        _attackTexture = BuildAttackLinkTexture(damagePalette: false);
        _damageAttackTexture = BuildAttackLinkTexture(damagePalette: true);
        _swordTexture = BuildSwordTexture(chargedPalette: false);
        _chargedSwordTexture = BuildSwordTexture(chargedPalette: true);
        _shovelLinkTexture = BuildShovelLinkTexture(damagePalette: false);
        _damageShovelLinkTexture = BuildShovelLinkTexture(damagePalette: true);
        _drownTexture = BuildDrownTexture(damagePalette: false);
        _damageDrownTexture = BuildDrownTexture(damagePalette: true);
        _topDownSwimTexture = BuildTopDownSwimTexture(damagePalette: false);
        _damageTopDownSwimTexture = BuildTopDownSwimTexture(damagePalette: true);
        _topDownDiveTexture = BuildTopDownDiveTexture(damagePalette: false);
        _damageTopDownDiveTexture = BuildTopDownDiveTexture(damagePalette: true);
        _fallInHoleTexture = BuildFallInHoleTexture(damagePalette: false);
        _damageFallInHoleTexture = BuildFallInHoleTexture(damagePalette: true);
        _ledgeJumpTexture = BuildLedgeJumpTexture(damagePalette: false);
        _damageLedgeJumpTexture = BuildLedgeJumpTexture(damagePalette: true);
        _sideScrollSquishXTexture = BuildSideScrollSquishTexture(
            vertical: false);
        _sideScrollSquishYTexture = BuildSideScrollSquishTexture(
            vertical: true);
        TerrainShadowDefinition terrainShadow = TerrainShadow.Load();
        _terrainShadowTexture = terrainShadow.Texture;
        _terrainShadowOffset = terrainShadow.Offset;
        _terrainEffects = new LinkTerrainEffectDatabase();
        _deathTexture = BuildDeathTexture();
        _transformedLink = new TransformedLinkDatabase();
        EndNewGameSlowFall();
        EndRoomWarpFall();
        _precisePosition = spawn;
        _lastSafePosition = spawn;
        _localRespawnFacing = _facing;
        ResetLinkWalkAnimation();
        Position = OracleObjectMath.ToPixelPosition(spawn);
        // Room entities/events are already initialized before Link. Select a
        // saved active disguise here so the ordinary Link frame is never
        // exposed for one render before the first physics update.
        RefreshTransformationState();
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
        bool preserveTopDownSwimming = false)
    {
        InterruptCarriedItems(discard: true);
        if (!preserveSword)
            CancelSwordAttack();
        CancelShovelAction();
        EndNewGameSlowFall();
        EndRoomWarpFall();
        EndHarpPose();
        _drownTime = 0.0f;
        _drownInvisibleTime = 0.0f;
        _hazardRecoveryTime = 0.0f;
        _fallInHoleTime = 0.0f;
        _fallInHoleInvisibleTime = 0.0f;
        if (!preserveLedgeJump)
            ClearLedgeHop();
        _enemyInvincibilityFrames = 0.0f;
        _enemyKnockbackFrames = 0.0f;
        _pendingSwordKnockbackFrames = 0;
        _pendingSwordKnockbackDirection = Vector2.Zero;
        _swordCollisionKnockback = false;
        _holePullCounter = 0;
        _holePullPackedPosition = -1;
        _fallInHoleWarpPending = false;
        _pullingIntoHole = false;
        _drowningHazard = HazardType.None;
        _drowning = false;
        _drownRespawning = false;
        _fallingInHole = false;
        _fallInHoleRespawning = false;
        _floorDoorRespawnCounter = 0;
        _floorDoorRecoveryCounter = 0;
        _sideScrollInstantRespawnCounter = 0;
        _sideScrollSquishPending = false;
        _sideScrollSquishAnimationCounter = 0;
        _sideScrollSquishFlickerCounter = 0;
        _getItemOneHandPose = false;
        _getItemTwoHandPose = false;
        _scriptedLinkAnimationMode = null;
        _carriedObjectPose = false;
        _braceletActionPose = null;
        _braceletLiftCollisionsDisabled = false;
        ClearSideScrollState(position);
        ClearTopDownAirState();
        if (!preserveTopDownSwimming)
            ClearTopDownSwimmingState();
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

    private void InterruptCarriedItems(bool discard)
    {
        if (_world is null)
            return;
        _world.InterruptBomb(this, discard);
        _world.InterruptBracelet(this, discard);
    }

    internal void BeginNewGameSlowFall(int initialZ)
    {
        _newGameFallRenderer ??= new CutsceneSpriteRenderer();
        _newGameFallFrames ??= new NewGameIntroDatabase().SpriteFrames("link-arrival");
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
            EndNewGameSlowFall();
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
        _newGameFallRenderer ??= new CutsceneSpriteRenderer();
        _newGameFallFrames ??=
            new NewGameIntroDatabase().SpriteFrames("link-arrival");
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
        _pushing = false;
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
            // Ages' removed tile read leaves A=$00 after the Z update. The
            // hazard-table lookup therefore misses and takes @linkCollapsed.
            _roomWarpFallCollapsed = true;
            _roomWarpFallCollapsedCounter = RoomWarpFallCollapsedFrames;
            _world.PlaySound(OracleSoundEngine.SndSplash);
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
        _harpRenderer ??= new CutsceneSpriteRenderer();
        _cutsceneHarpFrames ??=
            new NewGameIntroDatabase().SpriteFrames("link-harp");
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
        _pushing = false;
        QueueRedraw();
    }

    private void BeginPlayableHarpPose()
    {
        _harpRenderer ??= new CutsceneSpriteRenderer();
        _itemHarpFrames ??=
            new NewGameIntroDatabase().SpriteFrames("link-harp-item");
        _harpFrames = _itemHarpFrames;
        if (_harpFrames.Length != 17)
        {
            throw new InvalidOperationException(
                "Expected seventeen playable LINK_ANIM_MODE_HARP_2 frames.");
        }

        _harpFrame = 0;
        _harpFrameTicks = _harpFrames[0].Duration;
        _harpPoseActive = true;
        _walking = false;
        _pushing = false;
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

    public void BeginScrollingTransition(Vector2 position, Vector2I direction)
    {
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        // wScrollMode $08 freezes the active parent item, while the scrolling
        // transition moves Link without changing his parent-item-locked direction.
        if (!IsUsingItem)
            Face(direction);
        // updateItems clears wUsingShield before returning for wScrollMode
        // $08, but leaves the parent item allocated until scrolling ends.
        SuspendShield();
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
        bool preserveTopDownSwimming = TopDownSwimming;
        WarpTo(
            position,
            recordSafe: !resumeLedgeJump,
            preserveSword: true,
            preserveShield: true,
            preserveLedgeJump: resumeLedgeJump,
            preserveTopDownSwimming: preserveTopDownSwimming);
        if (resumeLedgeJump)
            _world.ResumeLedgeHopAfterScroll(this);
        _walking = false;
        QueueRedraw();
    }

    public void BeginRoomWarpTransition()
    {
        _cutsceneControlled = false;
        _walking = false;
        ClearShieldParent();
        CancelSwordAttack();
        CancelShovelAction();
        QueueRedraw();
    }

    public void BeginTimeWarpTransition(Vector2 portalPosition)
    {
        // interactionBeginTimewarp copies the portal position into w1Link and
        // writes DIR_DOWN before disabling Link. Clear pose state at the same
        // handoff so a pushing or item animation cannot survive underneath the
        // time-warp beam.
        WarpTo(portalPosition, recordSafe: false);
        BeginRoomWarpTransition();
        _facing = Facing.Down;
        _pushing = false;
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

    public void SetRoomWarpWalkPosition(Vector2 position, double delta)
    {
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        _walking = true;
        AdvanceLinkWalkAnimation();
        QueueRedraw();
    }

    public void FinishRoomWarpTransition(Vector2 position)
    {
        WarpTo(position);
        _walking = false;
        QueueRedraw();
    }

    public void TriggerHazard(ActiveTerrainInfo activeTerrain)
    {
        HazardType hazard = activeTerrain.Terrain.Hazard;
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
        int modified = RingEffects.IncomingDamageQuarters(_inventory, quarters, source);
        return ApplyUnmodifiedDamage(modified);
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

    internal bool ApplyEnemyContactDamage(
        Vector2 sourcePosition,
        int quarters,
        RingDamageSource source)
    {
        if (_braceletLiftCollisionsDisabled || !AcceptsRoomEntityContact ||
            IsDying || _enemyInvincibilityFrames > 0.0f || quarters <= 0)
            return false;
        if (!ApplyDamage(quarters, source))
            return false;

        // LINKDMG_04 selects SND_DAMAGE_LINK ($5f) when the collision is
        // accepted. Rejected contacts during Link's invincibility do not
        // enqueue another request.
        _world.PlaySound(OracleSoundEngine.SndDamageLink);
        _enemyInvincibilityFrames = EnemyInvincibilityFrames;
        _enemyKnockbackFrames = RingEffects.KnockbackFrames(
            _inventory, EnemyKnockbackFrames);
        _swordCollisionKnockback = false;
        _enemyKnockbackDirection = Position - sourcePosition;
        if (_enemyKnockbackDirection.LengthSquared() < 0.01f)
        {
            _enemyKnockbackDirection = -(Vector2)FacingVector;
        }
        else
        {
            int angle = OracleObjectMovement.Shared.RelativeAngle(
                sourcePosition, Position);
            _enemyKnockbackDirection =
                OracleObjectMovement.Shared.Direction(angle);
        }
        _walking = false;
        _pushing = false;
        InterruptCarriedItems(discard: false);
        ClearShieldParent();
        CancelSwordAttack();
        CancelShovelAction();
        QueueRedraw();
        return true;
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
                Vector2.Zero, -(Vector2)FacingVector)) & 0x18)
            : (OracleObjectMovement.Shared.RelativeAngle(
                sourcePosition, Position) & 0x18);
        _enemyKnockbackFrames = 0x18;
        _enemyKnockbackDirection = OracleObjectMath.StrictCardinalVector(angle);
        _swordCollisionKnockback = false;
        _walking = false;
        _pushing = false;
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
        _pendingSwordKnockbackDirection =
            OracleObjectMovement.Shared.Direction(angle);
    }

    private void TransferSwordCollisionKnockback()
    {
        if (_pendingSwordKnockbackFrames == 0)
            return;

        int frames = _pendingSwordKnockbackFrames;
        Vector2 direction = _pendingSwordKnockbackDirection;
        _pendingSwordKnockbackFrames = 0;
        _pendingSwordKnockbackDirection = Vector2.Zero;

        // itemTransferKnockbackToLink preserves a longer live counter but
        // always replaces Link's knockback angle.
        if (frames >= _enemyKnockbackFrames)
        {
            _enemyKnockbackFrames = frames;
            _swordCollisionKnockback = IsAttacking;
        }
        _enemyKnockbackDirection = direction;
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
        _pushing = false;
        CancelSwordAttack();
        CancelShovelAction();
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

    internal void AdvanceApplicationUpdate()
    {
        if (IsPhysicsProcessing())
            AdvancePhysics(ApplicationFixedUpdateScheduler.UpdateDelta);
        if (IsProcessing())
            AdvanceItems(ApplicationFixedUpdateScheduler.UpdateDelta);
    }

    private void AdvancePhysics(double delta)
    {
        _pushing = false;
        if (_floorDoorRespawnCounter != 0)
        {
            _floorDoorRespawnCounter--;
            if (_floorDoorRespawnCounter == 0)
            {
                Visible = true;
                ApplyDamage(4);
                _enemyInvincibilityFrames = 0x3c;
                _floorDoorRecoveryCounter = 0x10;
                QueueRedraw();
            }
            return;
        }
        if (_sideScrollInstantRespawnCounter != 0)
        {
            _sideScrollInstantRespawnCounter--;
            if (_sideScrollInstantRespawnCounter == 0)
            {
                Visible = true;
                ApplyDamage(
                    TerrainHazardDamageQuarters,
                    RingDamageSource.Hole);
                _enemyInvincibilityFrames = 0x3c;
                _hazardRecoveryTime = HazardRecoveryDuration;
                QueueRedraw();
            }
            return;
        }
        if (_sideScrollSquishPending ||
            _sideScrollSquishAnimationCounter != 0 ||
            _sideScrollSquishFlickerCounter != 0)
        {
            AdvanceSideScrollSquish();
            return;
        }
        if (IsDying)
        {
            // updateAllObjects continues from Link to ITEM_BRACELET after
            // linkCancelAllItemUsage/dropLinkHeldItem. Keep the released
            // child falling while LINK_STATE_DYING owns Link's update.
            _world.AdvanceBraceletProjectile();
            UpdateDeath(delta);
            return;
        }
        if (_floorDoorRecoveryCounter != 0)
        {
            _floorDoorRecoveryCounter--;
            return;
        }
        if (_cutsceneControlled)
            return;
        if (_drowning)
        {
            UpdateDrowning((float)delta);
            return;
        }

        if (_fallingInHole)
        {
            UpdateFallInHole((float)delta);
            return;
        }

        if (_hazardRecoveryTime > 0.0f)
        {
            _hazardRecoveryTime = Mathf.Max(0.0f, _hazardRecoveryTime - (float)delta);
            _walking = false;
            CancelSwordAttack();
            CancelShovelAction();
            QueueRedraw();
            return;
        }

        if (_pullingIntoHole && _world.RidingObject)
            CancelHolePull();
        if (_pullingIntoHole && UpdatePullIntoHole())
            return;

        if (_ledgeJumpState != LedgeJumpState.None)
        {
            UpdateLedgeHop(delta);
            return;
        }

        if (_enemyKnockbackFrames > 0.0f)
        {
            // Damage suppresses Link's ordinary item-input path, but the
            // released Bracelet item still receives its independent object
            // update. Angle $ff clears lateral and Z speed; gravity starts on
            // this update rather than after knockback expires.
            _world.AdvanceBraceletProjectile();
            ClearShieldParent();
            float frameDelta = (float)delta * 60.0f;
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
                            AngleForVector(_enemyKnockbackDirection)),
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
                    movementAllowed: _sideScrollAirborne);
            }
            else
            {
                Vector2 movement = OracleObjectMovement.Shared.Delta(
                    0x32,
                    AngleForVector(_enemyKnockbackDirection)) * frameDelta;
                TryMove(movement, allowWallSlide: false);
                _enemyKnockbackFrames = Mathf.Max(
                    0.0f,
                    _enemyKnockbackFrames - frameDelta);
            }
            _walking = false;
            if (!_swordCollisionKnockback || !IsAttacking)
            {
                _swordCollisionKnockback = false;
                CancelSwordAttack();
            }
            CancelShovelAction();
            if (_enemyKnockbackFrames == 0.0f)
                _swordCollisionKnockback = false;
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            if (_world.SideScrolling && !_world.CheckTileWarp(this))
                _world.CheckRoomExit(this);
            QueueRedraw();
            return;
        }

        if (_minecartJumpControlled)
        {
            AdvanceMinecartJumpUpdate();
            return;
        }
        if (_companionJumpControlled)
        {
            if (!AdvanceCompanionJumpUpdate())
                return;
        }

        if (_world.IsTransitioning)
            return;

        RefreshTransformationState();

        if (_world.DialogueOpen)
        {
            ClearShieldParent();
            _walking = false;
            CancelSwordAttack();
            CancelShovelAction();
            QueueRedraw();
            return;
        }

        ApplyTerrainWalkSoundParameter();

        Vector2 movementStart = _precisePosition;
        Vector2 input = Input.GetVector(
            "move_left", "move_right", "move_up", "move_down");
        if (_world.MovementDisabled &&
            !_minecartRideControlled && !_companionRideControlled)
            input = Vector2.Zero;
        Vector2 previousMovementInput = _lastMovementInput;
        _lastMovementInput = input;

        if (_companionRideControlled)
        {
            // SPECIALOBJECT_LINK_RIDING_ANIMAL owns Link's update while
            // wLinkObjectIndex points at the companion. The companion reads
            // A/B itself; ordinary equipped parent items must not turn those
            // presses into Link sword, feather, shield, or other item poses.
            _walking = false;
            _pushing = false;
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            _world.CheckRoomExit(this);
            QueueRedraw();
            return;
        }

        if (!_world.SideScrolling &&
            TryAdvanceTopDownSwimming(
                input,
                AngleForVector(previousMovementInput),
                movementStart,
                Input.IsActionJustPressed("attack"),
                Input.IsActionJustPressed("item")))
        {
            return;
        }

        bool primaryPressed = Input.IsActionPressed("attack");
        bool secondaryPressed = Input.IsActionPressed("item");
        bool itemButtonJustPressed =
            Input.IsActionJustPressed("attack") ||
            Input.IsActionJustPressed("item");
        if (_world.UpdateBomb(this, input, itemButtonJustPressed))
        {
            _walking = false;
            _pushing = false;
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            QueueRedraw();
            return;
        }
        if (_world.UpdateBracelet(
                this,
                input,
                primaryPressed,
                secondaryPressed,
                itemButtonJustPressed))
        {
            _walking = false;
            _pushing = false;
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            QueueRedraw();
            return;
        }

        if (_activeTransformation == 0 &&
            Input.IsActionJustPressed("attack") && !_world.SwordDisabled)
        {
            if (!IsUsingItem)
            {
                // Link's standing-state handler checks A-button-sensitive
                // objects and interactWithTileBeforeLink before checkUseItems.
                // A chest/sign/keyhole therefore wins over an equipped
                // Bracelet when both probes accept the same press.
                if (!_minecartRideControlled && _world.TryInteract(this))
                    return;
                if (!_world.ItemUsageDisabled &&
                    !_minecartRideControlled &&
                    _inventory.EquippedA == InventoryState.ItemBomb &&
                    _world.TryUseBomb(this))
                    return;
                if (!_world.ItemUsageDisabled &&
                    !_minecartRideControlled &&
                    _inventory.EquippedA == InventoryState.ItemBracelet &&
                    _world.TryUseBracelet(this, primaryButton: true))
                    return;
                if (!_world.ItemUsageDisabled &&
                    !_minecartRideControlled &&
                    RingEffects.CanPunch(
                    _inventory,
                    _inventory.EquippedA == InventoryState.ItemNone &&
                    _inventory.EquippedB == InventoryState.ItemNone))
                {
                    StartPunchAction(input);
                    return;
                }
            }
            if (_world.ItemUsageDisabled)
            {
                // wInShop routes A/B to checkShopInput instead of updating
                // either equipped parent item. Interaction remains available.
            }
            else if (_inventory.EquippedA == InventoryState.ItemSword)
                StartSwordAttack("attack", input);
            else if (!_minecartRideControlled &&
                _inventory.EquippedA == InventoryState.ItemShovel)
                StartShovelAction(input);
            else if (!_minecartRideControlled &&
                _inventory.EquippedA == InventoryState.ItemFeather)
                TryStartFeatherJump("attack");
            else if (_inventory.EquippedA == InventoryState.ItemSeedSatchel)
                StartSeedSatchelAction(input);
            else if (!_minecartRideControlled &&
                _inventory.EquippedA == InventoryState.ItemHarp)
                StartHarpAction();
        }
        else if (_activeTransformation == 0 &&
            Input.IsActionJustPressed("item") && !_world.SwordDisabled)
        {
            if (!_minecartRideControlled &&
                !IsUsingItem &&
                _world.TrySecondaryInteract(this))
            {
                return;
            }
            if (_world.ItemUsageDisabled)
            {
                // The secondary button can still lift or return shop stock.
            }
            else if (!IsUsingItem &&
                !_minecartRideControlled &&
                _inventory.EquippedB == InventoryState.ItemBomb)
            {
                if (_world.TryUseBomb(this))
                    return;
            }
            else if (!IsUsingItem &&
                !_minecartRideControlled &&
                RingEffects.CanPunch(
                _inventory,
                _inventory.EquippedA == InventoryState.ItemNone &&
                _inventory.EquippedB == InventoryState.ItemNone))
            {
                StartPunchAction(input);
            }
            else if (!IsUsingItem &&
                !_minecartRideControlled &&
                _inventory.EquippedB == InventoryState.ItemBracelet)
            {
                if (_world.TryUseBracelet(this, primaryButton: false))
                    return;
            }
            else if (_inventory.EquippedB == InventoryState.ItemSword)
            {
                StartSwordAttack("item", input);
            }
            else if (!_minecartRideControlled &&
                _inventory.EquippedB == InventoryState.ItemShovel)
            {
                StartShovelAction(input);
            }
            else if (!_minecartRideControlled &&
                _inventory.EquippedB == InventoryState.ItemFeather)
            {
                TryStartFeatherJump("item");
            }
            else if (_inventory.EquippedB == InventoryState.ItemSeedSatchel)
            {
                StartSeedSatchelAction(input);
            }
            else if (!_minecartRideControlled &&
                _inventory.EquippedB == InventoryState.ItemHarp)
            {
                StartHarpAction();
            }
        }

        UpdateShieldState(
            Input.IsActionPressed("attack"),
            Input.IsActionPressed("item"));

        if (_minecartRideControlled)
        {
            // linkState01 still runs checkUseItems and @updateDirection while
            // wLinkObjectIndex points at SPECIALOBJECT_MINECART. The companion
            // copies its position over Link later in the update, so input
            // changes Link's facing without moving him independently.
            UpdateMinecartRideDirection(input);
            _walking = false;
            _pushing = false;
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            _world.CheckRoomExit(this);
            QueueRedraw();
            return;
        }
        bool movementAllowed = !IsUsingItem || SwordAllowsMovement;
        if (_world.SideScrolling)
        {
            UpdateSideScrollMovement(delta, input, movementAllowed);
            AdvanceRocsCapeParent();
            UpdatePushingState(input);
            _world.UpdatePushableBlocks(
                _precisePosition,
                FacingVector,
                _walking ? input : Vector2.Zero);
            UpdateHeartRingCounter(_precisePosition - movementStart);

            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            if (!_world.CheckTileWarp(this))
                _world.CheckRoomExit(this);
            AdvanceTransformationAnimation(_walking);
            AdvanceTerrainWalkAnimation(walking: false);
            QueueRedraw();
            return;
        }

        if (_topDownAirborne)
        {
            UpdateTopDownAirMovement(
                delta, input, movementAllowed, movementStart);
            return;
        }

        _walking = AdvanceTopDownInputMovement(input, movementAllowed);
        if (_walking)
        {
            AdvanceLinkWalkAnimation();
        }
        else
        {
            ResetLinkWalkAnimation();
        }

        UpdatePushingState(input);

        // interactWithTileBeforeLink observes wLinkPushingDirection after
        // collision has stopped Link at the tile. Run the push check against
        // the resolved position, not the pre-movement approach position.
        _world.UpdatePushableBlocks(
            _precisePosition,
            FacingVector,
            _walking ? input : Vector2.Zero);

        Vector2 terrainPush = _world.GetTerrainPush(Position) * (float)delta;
        if (terrainPush != Vector2.Zero)
        {
            TryMove(terrainPush, allowWallSlide: false);
        }

        UpdateHeartRingCounter(_precisePosition - movementStart);

        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (!_world.CheckTileWarp(this))
            _world.CheckRoomExit(this);
        if (!_world.IsTransitioning)
            ApplyTerrainAtFeet();
        AdvanceTransformationAnimation(_walking);
        AdvanceTerrainWalkAnimation(_walking);
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

    private void AdvanceDeathUpdate()
    {
        if (!_deathSlowFadeRequested)
        {
            // standardGameState replaces wLinkDeathTrigger $ff with $e7 and
            // starts SNDCTRL_SLOW_FADEOUT on the first dying update.
            _deathSlowFadeRequested = true;
            _world.PlaySound(OracleSoundEngine.SndCtrlSlowFadeOut);
        }

        if (_deathPending)
        {
            // LINK_STATE_DYING substate 0 retains ordinary knockback until its
            // counter reaches zero. The spin begins on the following update.
            if (_enemyKnockbackFrames > 0.0f)
            {
                ClearShieldParent();
                Vector2 movement =
                    _enemyKnockbackDirection * EnemyKnockbackSpeed;
                TryMove(movement, allowWallSlide: false);
                _enemyKnockbackFrames = Mathf.Max(
                    0.0f, _enemyKnockbackFrames - 1.0f);
                _walking = false;
                _pushing = false;
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
        _enemyInvincibilityFrames = 0.0f;
        _walking = false;
        _pushing = false;
        InterruptCarriedItems(discard: true);
        ClearShieldParent();
        CancelSwordAttack();
        CancelShovelAction();
        _deathSpinLoopsRemaining = 4;
        SetDeathFrame(sequenceIndex: 0, frame: 2, duration: 8);
        _world.PlaySound(OracleSoundEngine.SndLinkDead);
    }

    private void SetDeathFrame(int sequenceIndex, int frame, int duration)
    {
        _deathSequenceIndex = sequenceIndex;
        _deathAnimationFrame = frame;
        _deathAnimationCounter = duration;
    }

    internal void BeginCutsceneControl(bool interruptBracelet = true)
    {
        if (interruptBracelet)
            InterruptCarriedItems(discard: true);
        _cutsceneControlled = true;
        ClearShieldParent();
        _walking = false;
        _pushing = false;
        CancelSwordAttack();
        CancelShovelAction();
        QueueRedraw();
    }

    internal bool CutsceneControlled => _cutsceneControlled;
    internal bool Walking => _walking;

    internal void BeginGetItemOneHandPose()
    {
        _getItemOneHandPose = true;
        _walking = false;
        _pushing = false;
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
        _pushing = false;
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
        if (mode is not null and not (0x06 or 0x07 or 0x08 or 0x09 or
            0x0e or 0x0f or 0x1c))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        if (_scriptedLinkAnimationMode == mode)
            return;
        _scriptedLinkAnimationMode = mode;
        _walking = false;
        _pushing = false;
        QueueRedraw();
    }

    internal void BeginCarriedObjectPose()
    {
        _carriedObjectPose = true;
        _pushing = false;
        QueueRedraw();
    }

    internal void SetBraceletActionPose(BraceletActionPose pose)
    {
        _braceletActionPose = pose;
        _walking = false;
        _pushing = false;
        CancelSwordAttack();
        CancelShovelAction();
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
        if (!_cutsceneControlled)
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
            TryMove(
                OracleObjectMovement.Shared.Delta(speed, angle),
                allowWallSlide: true);
            AdvanceLinkWalkAnimation();
        }
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

    internal void BeginForcedRoomEntryMovement(Vector2I direction)
    {
        if (direction != Vector2I.Up && direction != Vector2I.Right &&
            direction != Vector2I.Down && direction != Vector2I.Left)
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }
        ClearShieldParent();
        CancelSwordAttack();
        CancelShovelAction();
        _pushing = false;
        _walking = true;
        Face(direction);
        QueueRedraw();
    }

    internal void AdvanceForcedRoomEntryMovement(Vector2I direction)
    {
        _walking = true;
        Face(direction);
        // LINK_STATE_FORCE_MOVEMENT clears adjacentWallsBitset before each
        // standard-speed update, so this deliberately bypasses room blockers.
        _precisePosition += (Vector2)direction;
        AdvanceLinkWalkAnimation();
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    internal void EndForcedRoomEntryMovement()
    {
        _walking = false;
        QueueRedraw();
    }

    internal void SetScriptedPosition(Vector2 position)
    {
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        QueueRedraw();
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

        return linkY < platformY - radiusY - 2 &&
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
            SideScrollSquished || _sideScrollInstantRespawnCounter != 0)
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
                linkY >= platformY ? 0x00 : 0x10);
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
            MoveAwayFromSidePlatform(leftBlocked ? 0x08 : 0x18);
            return;
        }
        MoveAwayFromSidePlatform(linkX >= platformX ? 0x08 : 0x18);
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

        int yRadius = radiusY - 2;
        return unchecked((byte)(linkY - platformY + yRadius)) >=
            yRadius * 2 + 1;
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

    internal void SetScriptedCoordinateHigh(bool horizontal, int coordinate)
    {
        // preventObjectHFromPassingObjectD overwrites only Object.xh/yh. Keep
        // the 8.8 fractional byte accumulated by linkCutscene6 intact.
        SetCoordinateHigh(horizontal, coordinate);
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

    internal void BeginFloorDoorRespawn()
    {
        Vector2 respawn = _lastSafePosition;
        WarpTo(respawn, recordSafe: false);
        _walking = false;
        _pushing = false;
        CancelSwordAttack();
        CancelShovelAction();
        Visible = false;
        _floorDoorRespawnCounter = 2;
        QueueRedraw();
    }

    internal void SetCutscenePushing(bool pushing)
    {
        _pushing = pushing;
        QueueRedraw();
    }

    internal void EndCutsceneControl()
    {
        _cutsceneControlled = false;
        _walking = false;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!ApplicationUpdateOwned)
            AdvanceItems(delta);
    }

    private void AdvanceItems(double delta)
    {
        if (_enemyInvincibilityFrames > 0.0f)
        {
            _enemyInvincibilityFrames = Mathf.Max(
                0.0f, _enemyInvincibilityFrames - (float)delta * 60.0f);
            QueueRedraw();
        }

        if (IsDying)
            return;

        if (IsFloorDoorRespawning)
            return;

        // updateItems skips initialized items while wScrollMode is $08. Room
        // warps cancel the sword synchronously in BeginRoomWarpTransition.
        if (_world.IsTransitioning)
            return;

        TransferSwordCollisionKnockback();

        if (_world.SwordDisabled)
        {
            CancelSwordAttack();
            CancelShovelAction();
        }

        if (_drowning || _fallingInHole || _hazardRecoveryTime > 0.0f ||
            (_pullingIntoHole && _holePullCounter >= 16))
        {
            CancelSwordAttack();
            CancelShovelAction();
            return;
        }

        if (IsAttacking)
        {
            _swordFrameAccumulator += delta * 60.0;
            while (_swordFrameAccumulator + 0.000001 >= 1.0 && IsAttacking)
            {
                _swordFrameAccumulator -= 1.0;
                AdvanceSwordFrame(IsSwordButtonHeld(), _lastMovementInput);
            }
            QueueRedraw();
        }
        if (IsUsingShovel)
        {
            _shovelFrameAccumulator += delta * 60.0;
            while (_shovelFrameAccumulator + 0.000001 >= 1.0 && IsUsingShovel)
            {
                _shovelFrameAccumulator -= 1.0;
                AdvanceShovelFrame();
            }
            QueueRedraw();
        }
        if (IsUsingSeedSatchel)
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
        if (IsUsingPunch)
        {
            _punchFrameAccumulator += delta * 60.0;
            while (_punchFrameAccumulator + 0.000001 >= 1.0 && IsUsingPunch)
            {
                _punchFrameAccumulator -= 1.0;
                AdvancePunchFrame();
            }
            QueueRedraw();
        }
        if (IsUsingHarp)
        {
            _harpFrameAccumulator += delta * 60.0;
            while (_harpFrameAccumulator + 0.000001 >= 1.0 && IsUsingHarp)
            {
                _harpFrameAccumulator -= 1.0;
                AdvanceHarpAction();
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
        if (LedgeShadowDrawn ||
            _topDownAirborne && (_world.FrameCounter & 1) != 0)
            DrawTexture(_terrainShadowTexture, _terrainShadowOffset);

        foreach (PlayerGroundDrawPass pass in GroundDrawOrder)
        {
            switch (pass)
            {
                case PlayerGroundDrawPass.Body:
                    DrawBody();
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
        if (_deathAnimationActive)
        {
            DrawTextureRectRegion(
                _deathTexture,
                new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                new Rect2(_deathAnimationFrame * 16, 0, 16, 16));
        }
        else if (_roomWarpFallActive && _roomWarpFallCollapsed)
        {
            DrawTextureRectRegion(
                _deathTexture,
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
        else if (_harpPoseActive &&
            _harpRenderer is not null && _harpFrames is not null)
        {
            _harpRenderer.DrawRelativeFrame(
                this,
                _harpFrames[_harpFrame],
                z: 0);
        }
        else if (_scriptedLinkAnimationMode.HasValue)
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
        else if (_drowning && !_drownRespawning)
        {
            int frame = GetDrownAnimationFrame();
            DrawTextureRectRegion(
                DamagePaletteActive ? _damageDrownTexture : _drownTexture,
                new Rect2(DrownSpriteOrigin, new Vector2(16, 16)),
                new Rect2(frame * 16, (int)_facing * 16, 16, 16));
        }
        else if (_fallingInHole && !_fallInHoleRespawning)
        {
            int frame = GetFallInHoleFrame();
            DrawTextureRectRegion(
                DamagePaletteActive ? _damageFallInHoleTexture : _fallInHoleTexture,
                new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                new Rect2(frame * 16, 0, 16, 16));
        }
        else if (TopDownSwimming)
        {
            DrawTextureRectRegion(
                TopDownDiving
                    ? DamagePaletteActive
                        ? _damageTopDownDiveTexture
                        : _topDownDiveTexture
                    : DamagePaletteActive
                        ? _damageTopDownSwimTexture
                        : _topDownSwimTexture,
                new Rect2(DrownSpriteOrigin, new Vector2(16, 16)),
                new Rect2(
                    _topDownSwimAnimationFrame * 16,
                    TopDownDiving ? 0 : (int)_facing * 16,
                    16,
                    16));
        }
        else if (SideScrollSquished)
        {
            DrawTexture(
                _sideScrollSquishVertical
                    ? _sideScrollSquishYTexture
                    : _sideScrollSquishXTexture,
                new Vector2(-16, -16));
        }
        else if (UsesAirborneSwordPose)
        {
            DrawSwordPose();
        }
        else if (_sideScrollAirborne)
        {
            DrawAirborneLinkBody(Vector2.Zero);
        }
        else if (_topDownAirborne)
        {
            DrawAirborneLinkBody(new Vector2(0, _topDownAirZFixed >> 8));
        }
        else if (_ledgeJumpState != LedgeJumpState.None)
        {
            DrawTextureRectRegion(
                DamagePaletteActive
                    ? _damageLedgeJumpTexture
                    : _ledgeJumpTexture,
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
                    ? _damageGetItemTwoHandTexture
                    : _getItemTwoHandTexture,
                NormalSpriteOrigin);
        }
        else if (_getItemOneHandPose)
        {
            DrawTexture(
                DamagePaletteActive
                    ? _damageGetItemOneHandTexture
                    : _getItemOneHandTexture,
                NormalSpriteOrigin);
        }
        else if (_braceletActionPose is BraceletActionPose braceletPose)
        {
            DrawTexture(
                (DamagePaletteActive
                    ? _damageBraceletActionTextures
                    : _braceletActionTextures)[(int)braceletPose, (int)_facing],
                new Vector2(-16, -16));
        }
        else if (_carriedObjectPose)
        {
            int frame = GetWalkAnimationFrame();
            DrawTextureRectRegion(
                DamagePaletteActive
                    ? _damageCarriedObjectTexture
                    : _carriedObjectTexture,
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
        else if (IsUsingPunch)
        {
            Vector2 offset = _expertPunch && _punchFrame is >= 3 and < 11
                ? _linkItems.AttackPoseOffset((int)_facing)
                : Vector2.Zero;
            DrawTextureRectRegion(
                DamagePaletteActive ? _damageAttackTexture : _attackTexture,
                new Rect2(NormalSpriteOrigin + offset, new Vector2(16, 16)),
                new Rect2(16, (int)_facing * 16, 16, 16));
        }
        else if (IsAttacking)
        {
            DrawSwordPose();
        }
        else if (IsUsingShovel)
        {
            int phase = _shovelFrame <
                _linkItems.Constants.ShovelSecondPoseFrame ? 0 : 1;
            DrawTextureRectRegion(
                DamagePaletteActive
                    ? _damageShovelLinkTexture
                    : _shovelLinkTexture,
                new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                new Rect2(phase * 16, (int)_facing * 16, 16, 16));
        }
        else if (IsUsingSeedSatchel)
        {
            // LINK_ANIM_MODE_21 uses graphics $b0-$b3 for eight updates.
            DrawTextureRectRegion(
                DamagePaletteActive ? _damageAttackTexture : _attackTexture,
                new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                new Rect2(16, (int)_facing * 16, 16, 16));
        }
        else if (IsUsingShield)
        {
            DrawShieldPose();
        }
        else if (_minecartRideControlled)
        {
            // func_4553 forces walking-graphics variant $01 before checking
            // equipped shields or push state whenever wLinkObjectIndex is the
            // SPECIALOBJECT_MINECART slot.
            int frame = GetWalkAnimationFrame();
            DrawTextureRectRegion(
                DamagePaletteActive
                    ? _damageMinecartLinkTexture
                    : _minecartLinkTexture,
                new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                new Rect2(frame * 16, (int)_facing * 16, 16, 16));
        }
        else if (_pushing)
        {
            int frame = GetWalkAnimationFrame();
            DrawTextureRectRegion(
                DamagePaletteActive ? _damagePushTexture : _pushTexture,
                new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                new Rect2(frame * 16, (int)_facing * 16, 16, 16));
        }
        else if (IsShieldEquipped)
        {
            DrawShieldPose();
        }
        else
        {
            int frame = GetWalkAnimationFrame();
            Rect2 source = GetFrame(_facing, frame);
            DrawTextureRectRegion(
                DamagePaletteActive ? _damageTexture : _texture,
                new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                source);
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
                : NormalSpriteOrigin);
    }

    private Texture2D ScriptedLinkAnimationTexture(
        int mode,
        bool damagePalette)
    {
        return mode switch
        {
            0x06 => _sideScrollSquishXTexture,
            0x07 => _sideScrollSquishYTexture,
            0x08 => damagePalette
                ? _damageFunnyJokeDanceLeftTexture
                : _funnyJokeDanceLeftTexture,
            0x09 => damagePalette
                ? _damageFunnyJokeDanceRightTexture
                : _funnyJokeDanceRightTexture,
            0x0e => damagePalette
                ? _damageGetItemOneHandTexture
                : _getItemOneHandTexture,
            0x0f => damagePalette
                ? _damageGetItemTwoHandTexture
                : _getItemTwoHandTexture,
            0x1c => damagePalette
                ? _damageGetItemOneHandRightTexture
                : _getItemOneHandRightTexture,
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
        if (!_terrainWalkSoundParameter)
            return;

        LinkTerrainEffectFrame? terrainEffect = GetCurrentTerrainEffect();
        if (terrainEffect is not { Sound: > 0 })
            return;

        // @tileType_puddle consumes animParameter bit 5 even when
        // wLinkImmobilized suppresses playback. On other terrain the bit
        // remains available until the walking animation loads its next frame.
        _terrainWalkSoundParameter = false;
        if (!_world.MovementDisabled)
            _world.PlaySound(terrainEffect.Sound);
    }

    internal void AdvanceTerrainWalkAnimation(bool walking)
    {
        if (!walking)
        {
            _terrainWalkUpdates = 0;
            _terrainWalkSoundParameter = false;
            return;
        }

        _terrainWalkUpdates++;
        int nextUpdate = _terrainWalkUpdates + 1;
        if (_terrainEffects.WalkSoundWindowStarts(nextUpdate))
            _terrainWalkSoundParameter = true;
        else if (!_terrainEffects.WalkSoundWindowContains(nextUpdate))
            _terrainWalkSoundParameter = false;
    }

    private void TryMove(Vector2 movement, bool allowWallSlide = false)
    {
        if (movement == Vector2.Zero)
            return;

        Vector2 resolved = _world.ResolveMovement(_precisePosition, movement, allowWallSlide);
        if (resolved != Vector2.Zero)
        {
            _precisePosition += resolved;
            return;
        }

        if (!IsUsingItem && _world.TryStartLedgeHop(this, _precisePosition, movement))
            return;
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
            AdvanceSideScrollUpdate(input, movementAllowed);
        }
    }

    internal void AdvanceSideScrollUpdateForValidation(
        Vector2 input,
        bool startJump = false)
    {
        if (!_world.SideScrolling)
        {
            throw new InvalidOperationException(
                "A side-scrolling Link update was requested outside a side-scrolling tileset.");
        }
        if (startJump)
        {
            if (_sideScrollAirborne)
                throw new InvalidOperationException(
                    "The validation side-scrolling jump began while Link was already airborne.");
            BeginSideScrollAirborne(
                jumped: true,
                _world.SideScrollParameters);
        }
        AdvanceSideScrollUpdate(input, movementAllowed: true);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    private void AdvanceSideScrollUpdate(Vector2 input, bool movementAllowed)
    {
        SideScrollPlayerParameters parameters = _world.SideScrollParameters;
        SideScrollTerrainState terrain = _world.GetSideScrollTerrain(
            _precisePosition);

        if ((terrain.ActiveType & SideScrollTileType.Water) != 0)
        {
            if (_sideScrollSwimmingState == 0)
            {
                _sideScrollSwimmingState = 1;
                _world.SpawnDrowningSplash(Position, HazardType.Water);
            }
            AdvanceSideScrollSwimming(input, movementAllowed, parameters);
            _sideScrollPreviousActiveType = terrain.ActiveType;
            return;
        }

        if (_sideScrollSwimmingState != 0)
        {
            bool surfacedFromWaterLadder =
                _sideScrollPreviousActiveType ==
                (SideScrollTileType.Ladder | SideScrollTileType.Water);
            _sideScrollSwimmingState = 0;
            _sideScrollSwimBurstState = 0;
            _sideScrollSwimBurstCounter = 0;
            if (!surfacedFromWaterLadder)
            {
                BeginSideScrollAirborne(
                    jumped: false,
                    parameters,
                    parameters.WaterExitSpeedZ);
                _sideScrollAngle = AngleForVector(input);
                _world.SpawnDrowningSplash(Position, HazardType.Water);
            }
        }

        int walls = _world.GetAdjacentWallsBitset(_precisePosition);
        if (_sideScrollAirborne)
        {
            AdvanceSideScrollAirborne(
                input, movementAllowed, terrain, walls, parameters);
            _sideScrollPreviousActiveType = terrain.ActiveType;
            return;
        }

        bool grounded = (walls & parameters.GroundWallMask) != 0;
        bool onLadder =
            (terrain.CombinedType & SideScrollTileType.Ladder) != 0;
        if (!_world.RidingObject && !grounded && !onLadder)
        {
            BeginSideScrollAirborne(jumped: false, parameters);
            AdvanceSideScrollAirborne(
                input, movementAllowed, terrain, walls, parameters);
            _sideScrollPreviousActiveType = terrain.ActiveType;
            return;
        }

        if (_enemyKnockbackFrames > 0.0f)
        {
            _sideScrollPreviousActiveType = terrain.ActiveType;
            return;
        }
        if (terrain.ActiveTile == parameters.SpikeTile)
            ApplySideScrollSpikeDamage();
        AdvanceGroundedSideScroll(
            input, movementAllowed, terrain, parameters);
        _sideScrollPreviousActiveType = terrain.ActiveType;
    }

    private void AdvanceGroundedSideScroll(
        Vector2 input,
        bool movementAllowed,
        SideScrollTerrainState terrain,
        SideScrollPlayerParameters parameters)
    {
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
                targetSpeed: parameters.NormalSpeed,
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
            SetSideScrollTerrainSpeed(
                terrainMode: 0,
                initialSpeed: parameters.NormalSpeed,
                velocityInterval: 0,
                targetSpeed: parameters.NormalSpeed,
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
        walls = _world.GetAdjacentWallsBitset(_precisePosition);
        SideScrollTerrainState currentTerrain =
            _world.GetSideScrollTerrain(_precisePosition);
        _sideScrollClimbing =
            (walls & parameters.GroundWallMask) == 0 &&
            (currentTerrain.ActiveType & SideScrollTileType.Ladder) != 0;
    }

    private void AdvanceSideScrollAirborne(
        Vector2 input,
        bool movementAllowed,
        SideScrollTerrainState terrain,
        int walls,
        SideScrollPlayerParameters parameters)
    {
        if (_sideScrollJumpSoundPending)
        {
            _sideScrollJumpSoundPending = false;
            _world.PlaySound(parameters.JumpSound);
        }

        if (_sideScrollSpeedZ >= 0)
        {
            if (_world.RidingObject)
            {
                _sideScrollAngle = 0xff;
                LandFromSideScrollAir(parameters, snapToGround: false);
                return;
            }
            if ((walls & parameters.GroundWallMask) != 0)
            {
                LandFromSideScrollAir(parameters, snapToGround: true);
                return;
            }
            if ((terrain.ActiveType & SideScrollTileType.Ladder) != 0 &&
                IsUpwardAngle(AngleForVector(input)))
            {
                LandFromSideScrollAir(parameters, snapToGround: false);
                return;
            }

            int highY = _sideScrollYFixed >> 8;
            if ((highY & 0x08) != 0 &&
                terrain.BelowType ==
                    (SideScrollTileType.Ladder |
                     SideScrollTileType.LadderTop))
            {
                LandFromSideScrollAir(parameters, snapToGround: true);
                return;
            }
            if (terrain.ActiveType == SideScrollTileType.Lava)
            {
                StartDrowning(HazardType.Lava);
                return;
            }
            if ((terrain.ActiveType & SideScrollTileType.Hole) != 0 &&
                terrain.BelowType == SideScrollTileType.None)
            {
                _world.PlaySound(OracleSoundEngine.SndDamageLink);
                BeginSideScrollInstantRespawn();
                return;
            }

            UpdateSideScrollVelocity(
                movementAllowed ? AngleForVector(input) : 0xff,
                inAir: true);
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
            return;
        }

        int horizontalAngle =
            SideScrollHorizontalAngle(_sideScrollAngle);
        if (horizontalAngle < 0x80 && _sideScrollSpeedRaw != 0)
        {
            if (!IsUsingItem)
                UpdateFacingFromSideScrollAngle(horizontalAngle);
            ApplySideScrollVelocity(
                _sideScrollSpeedRaw,
                horizontalAngle,
                allowWallSlide: false);
        }

        if ((_sideScrollYFixed >> 8) >= parameters.BottomBoundary)
        {
            LandFromSideScrollAir(parameters, snapToGround: true);
            return;
        }

        _walking = false;
        AdvanceSideScrollAirAnimation(parameters);
    }

    private bool TryStartFeatherJump(string buttonAction)
    {
        return _world.SideScrolling
            ? TryStartSideScrollJump(buttonAction)
            : TryStartTopDownJump();
    }

    private bool TryStartSideScrollJump(string? buttonAction = null)
    {
        if (!_world.SideScrolling || _sideScrollAirborne ||
            _drowning || _fallingInHole || _pullingIntoHole ||
            IsCarryingObject || IsHoldingItemOneHand ||
            IsHoldingItemTwoHands ||
            _inventory.FeatherLevel <= 0)
        {
            return false;
        }

        SideScrollTerrainState terrain =
            _world.GetSideScrollTerrain(_precisePosition);
        if ((terrain.ActiveType &
                (SideScrollTileType.Water | SideScrollTileType.Hole)) != 0)
        {
            return false;
        }

        BeginSideScrollAirborne(
            jumped: true,
            _world.SideScrollParameters);
        _rocsCapeButtonAction =
            _inventory.FeatherLevel >= 2 ? buttonAction : null;
        return true;
    }

    private bool TryStartTopDownJump()
    {
        if (_topDownAirborne || _world.RidingObject ||
            _drowning || _fallingInHole || _pullingIntoHole ||
            IsCarryingObject || IsHoldingItemOneHand ||
            IsHoldingItemTwoHands || _inventory.FeatherLevel <= 0)
        {
            return false;
        }

        TopDownAirParameters parameters =
            TopDownAirDatabase.Shared.Parameters;
        _topDownAirUpdateAccumulator = 0.0;
        _topDownAirZFixed = 0;
        _topDownAirSpeedZ = parameters.JumpSpeedZ;
        // linkUpdateInAir's @startedJump branch normalizes wActiveTileType,
        // snapshots wLinkAngle at SPEED_100, and then advances that stored
        // trajectory even if a later parent item immobilizes Link.
        _topDownAirAngle = AngleForVector(_lastMovementInput);
        _topDownAirSpeedRaw = NormalTopDownSpeed;
        _topDownAirAnimationPhase = 0;
        _topDownAirAnimationCounter =
            parameters.AnimationPhaseDurations[0];
        _airborneLinkAnimationMode = IsAttacking
            ? AirborneLinkAnimationMode.Walk
            : AirborneLinkAnimationMode.Jump;
        _topDownAirborne = true;
        _topDownJumpSoundPending = true;
        _walking = false;
        _pushing = false;
        return true;
    }

    private void UpdateTopDownAirMovement(
        double delta,
        Vector2 input,
        bool movementAllowed,
        Vector2 movementStart)
    {
        _topDownAirUpdateAccumulator += delta * 60.0;
        while (_topDownAirUpdateAccumulator + 0.000001 >= 1.0 &&
            _topDownAirborne)
        {
            _topDownAirUpdateAccumulator -= 1.0;
            AdvanceTopDownAirUpdate();
            if (_topDownAirborne)
                AdvanceTopDownAirMomentum();
        }

        if (!_topDownAirborne)
        {
            // A landing calls animateLinkStanding inside linkUpdateInAir, then
            // resumes the ordinary movement path in the same update. Held
            // movement therefore performs the first WALK decrement immediately.
            _walking = AdvanceTopDownInputMovement(input, movementAllowed);
            if (_walking)
                AdvanceLinkWalkAnimation();
            else
                ResetLinkWalkAnimation();
        }
        else
        {
            _walking = false;
        }
        _pushing = false;
        UpdateHeartRingCounter(_precisePosition - movementStart);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);

        _world.CheckRoomExit(this);
        if (!_world.IsTransitioning && !_topDownAirborne)
            ApplyTerrainAtFeet();
        AdvanceTransformationAnimation(_walking);
        AdvanceTerrainWalkAnimation(walking: false);
        QueueRedraw();
    }

    private void AdvanceTopDownAirUpdate()
    {
        TopDownAirParameters parameters =
            TopDownAirDatabase.Shared.Parameters;
        if (_topDownJumpSoundPending)
        {
            _topDownJumpSoundPending = false;
            _world.PlaySound(parameters.JumpSound);
        }

        if (OracleObjectMath.UpdateSpeedZ(
            ref _topDownAirZFixed,
            ref _topDownAirSpeedZ,
            parameters.Gravity))
        {
            _topDownAirborne = false;
            _topDownAirSpeedZ = 0;
            _topDownAirAngle = 0xff;
            _topDownAirSpeedRaw = 0;
            _topDownAirAnimationPhase = 0;
            _topDownAirAnimationCounter = 0;
            _airborneLinkAnimationMode = AirborneLinkAnimationMode.None;
            ResetLinkWalkAnimation();
            _world.PlaySound(parameters.LandSound);
            return;
        }

        if (_topDownAirSpeedZ > parameters.MaximumFallSpeed)
            _topDownAirSpeedZ = parameters.MaximumFallSpeed;
        AdvanceTopDownAirAnimation(parameters);
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
                AdvanceTopDownAirMomentum();
        }
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    private void AdvanceTopDownAirMomentum()
    {
        if (_topDownAirAngle >= 0x80 || _topDownAirSpeedRaw == 0)
            return;
        ApplyTopDownObjectSpeed(
            _topDownAirSpeedRaw,
            _topDownAirAngle,
            allowWallSlide: true,
            allowLedgeHop: false);
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

        InterruptCarriedItems(discard: true);
        CancelSwordAttack();
        CancelShovelAction();
        ClearShieldParent();
        ClearTopDownAirState();
        _enemyKnockbackFrames = 0.0f;
        _pendingSwordKnockbackFrames = 0;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        TopDownAirParameters parameters =
            TopDownAirDatabase.Shared.Parameters;
        _topDownAirZFixed = initialZ << 8;
        _topDownAirSpeedZ = -0x01c0;
        _topDownAirAnimationPhase = 0;
        _topDownAirAnimationCounter =
            parameters.AnimationPhaseDurations[0];
        _airborneLinkAnimationMode = AirborneLinkAnimationMode.Jump;
        _topDownAirborne = true;
        _topDownJumpSoundPending = true;
        _minecartJumpAngle = angle;
        _minecartJumpControlled = true;
        _minecartRideControlled = false;
        _walking = false;
        _pushing = false;
        QueueRedraw();
    }

    internal void FinishMinecartMount(
        Vector2 cartPosition,
        int cartDirection,
        int animationParameter)
    {
        ClearTopDownAirState();
        SetMinecartRidePosition(
            cartPosition,
            cartDirection,
            animationParameter,
            Vector2.Zero);
        _world.PlaySound(TopDownAirDatabase.Shared.Parameters.LandSound);
    }

    /// <summary>
    /// companionTryToMount sets Link's vertical speed to -$01c0 while the
    /// companion nudges each high coordinate toward its own center.
    /// </summary>
    internal void BeginCompanionMount(Vector2 position)
    {
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
        _precisePosition = new Vector2(x, y);
        Position = _precisePosition;
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
        ClearTopDownAirState();
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
        _precisePosition = companionPosition + screenOffset + linkOffset;
        _companionRideTexture = linkTexture;
        _damageCompanionRideTexture = damageLinkTexture;
        _companionRideTextureOffset = textureOffset;
        // func_410d uses objectCopyPositionWithOffset, which copies the
        // companion's Z as well as its offset Y/X into riding Link.
        _companionRideZFixed = zFixed;
        _companionRideControlled = true;
        _facing = (Facing)direction;
        _walking = false;
        _pushing = false;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
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
        TopDownAirParameters parameters = TopDownAirDatabase.Shared.Parameters;
        _facing = (Facing)companionDirection;
        _lastSafePosition = OracleObjectMath.ToPixelPosition(companionPosition);
        _localRespawnFacing = _facing;
        _companionRideControlled = false;
        BeginCompanionJump(
            companionPosition,
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

        InterruptCarriedItems(discard: true);
        CancelSwordAttack();
        CancelShovelAction();
        ClearShieldParent();
        ClearTopDownAirState();
        _enemyKnockbackFrames = 0.0f;
        _pendingSwordKnockbackFrames = 0;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        TopDownAirParameters parameters = TopDownAirDatabase.Shared.Parameters;
        _topDownAirZFixed = initialZ << 8;
        _topDownAirSpeedZ = parameters.CompanionJumpSpeedZ;
        _topDownAirAnimationPhase = 0;
        _topDownAirAnimationCounter = parameters.AnimationPhaseDurations[0];
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
        _pushing = false;
        QueueRedraw();
    }

    private bool AdvanceCompanionJumpUpdate()
    {
        TopDownAirParameters parameters = TopDownAirDatabase.Shared.Parameters;
        if (_companionJumpAnimationDeferred)
        {
            _companionJumpAnimationDeferred = false;
            _airborneLinkAnimationMode = AirborneLinkAnimationMode.Jump;
        }
        if (_topDownJumpSoundPending)
        {
            _topDownJumpSoundPending = false;
            _world.PlaySound(parameters.JumpSound);
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
            ClearTopDownAirState();
            _world.PlaySound(parameters.LandSound);
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            QueueRedraw();
            return resumeOrdinaryMovement;
        }
        if (_topDownAirSpeedZ > parameters.MaximumFallSpeed)
            _topDownAirSpeedZ = parameters.MaximumFallSpeed;
        AdvanceTopDownAirAnimation(parameters);
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
        _precisePosition = cartPosition + screenOffset + linkOffset;
        _minecartMainObjectPosition = cartPosition;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        _minecartRideControlled = true;
        _walking = false;
        _pushing = false;
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
            TopDownAirDatabase.Shared.Parameters;
        if (_topDownJumpSoundPending)
        {
            _topDownJumpSoundPending = false;
            _world.PlaySound(parameters.JumpSound);
        }

        _precisePosition += OracleObjectMovement.Shared.Delta(
            0x14,
            _minecartJumpAngle);
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
            if (_topDownAirSpeedZ > parameters.MaximumFallSpeed)
                _topDownAirSpeedZ = parameters.MaximumFallSpeed;
            AdvanceTopDownAirAnimation(parameters);
        }

        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    private void AdvanceTopDownAirAnimation(
        TopDownAirParameters parameters)
    {
        if (_airborneLinkAnimationMode == AirborneLinkAnimationMode.Walk)
        {
            AdvanceLinkWalkAnimation();
            return;
        }

        if (_topDownAirAnimationPhase >=
            parameters.AnimationPhaseDurations.Length)
            return;
        if (--_topDownAirAnimationCounter > 0)
            return;
        _topDownAirAnimationPhase++;
        _topDownAirAnimationCounter = _topDownAirAnimationPhase <
            parameters.AnimationPhaseDurations.Length
                ? parameters.AnimationPhaseDurations[_topDownAirAnimationPhase]
                : 0;
    }

    private void ClearTopDownAirState()
    {
        _topDownAirUpdateAccumulator = 0.0;
        _topDownAirZFixed = 0;
        _topDownAirSpeedZ = 0;
        _topDownAirAngle = 0xff;
        _topDownAirSpeedRaw = 0;
        _topDownAirAnimationPhase = 0;
        _topDownAirAnimationCounter = 0;
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
        _companionRideTexture = null;
        _damageCompanionRideTexture = null;
        _companionRideTextureOffset = Vector2.Zero;
        _companionRideZFixed = 0;
    }

    /// <summary>
    /// Port of the non-side-view linkUpdateSwimming states used by Flippers.
    /// This includes normal-water linkUpdateDiving; Mermaid Suit movement and
    /// underwater transitions remain owned by a later implementation, so Ages
    /// SeaWater retains its drowning behavior on this path.
    /// </summary>
    private bool TryAdvanceTopDownSwimming(
        Vector2 input,
        int entryAngle,
        Vector2 movementStart,
        bool attackJustPressed,
        bool diveJustPressed)
    {
        if (_topDownAirborne || _world.RidingObject ||
            _minecartRideControlled || _companionRideControlled)
        {
            ClearTopDownSwimmingState();
            return false;
        }

        ActiveTerrainInfo activeTerrain = _world.GetActiveTerrain(Position);
        if (activeTerrain.Terrain.Hazard != HazardType.Water)
        {
            ClearTopDownSwimmingState();
            return false;
        }

        bool unsupportedSeaWater =
            activeTerrain.Terrain.Type == TerrainType.SeaWater;
        bool hasFlippers =
            _inventory.HasTreasure(TreasureDatabase.TreasureFlippers);
        if (unsupportedSeaWater || !hasFlippers)
        {
            ClearTopDownSwimmingState();
            InterruptCarriedItems(discard: true);
            ClearShieldParent();
            StartDrowning(HazardType.Water);
            return true;
        }

        ApplyTopDownSwimmingCurrent(activeTerrain.Terrain.Type);

        if (_topDownSwimmingState == 0)
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
        UpdateTopDownDiving(diveJustPressed);
        int inputAngle = AngleForVector(input);
        if (inputAngle < 0x80)
            UpdateFacing(input);
        UpdateTopDownFlippers(inputAngle, attackJustPressed);
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
        InterruptCarriedItems(discard: true);
        ClearShieldParent();
        CancelSwordAttack();
        CancelShovelAction();
        _topDownSwimmingState = 2;
        _topDownSwimmingEntryCounter = parameters.EntryUpdates;
        _topDownSwimAngle = entryAngle;
        _topDownSwimSpeedRaw = baseSpeed;
        _topDownSwimTargetSpeedRaw = baseSpeed;
        _topDownSwimVelocityCounter = 0;
        _topDownSwimBurstState = 0;
        _topDownSwimBurstCounter = 0;
        _topDownSwimAnimationFrame = 0;
        _topDownSwimAnimationCounter =
            parameters.AnimationFrameDurations[0];
        _topDownDiving = false;
        _topDownDiveCounter = 0;
        if (ZIndex == DivingZIndex)
            ZIndex = NormalZIndex;
        _walking = false;
        _pushing = false;
        ResetLinkWalkAnimation();
        _world.SpawnDrowningSplash(Position, HazardType.Water);
        QueueRedraw();
    }

    private void ApplyTopDownSwimmingCurrent(TerrainType terrain)
    {
        int angle = terrain switch
        {
            TerrainType.UpCurrent => 0x00,
            TerrainType.RightCurrent => 0x08,
            TerrainType.DownCurrent => 0x10,
            TerrainType.LeftCurrent => 0x18,
            _ => 0xff
        };
        if (angle >= 0x80)
            return;

        // linkApplyTileTypes@tileType_current applies SPEED_c0 before it
        // enters the shared water branch. Swimming momentum is then applied
        // independently by linkUpdateSwimming on the same update.
        ApplyTopDownObjectSpeed(
            GrassTopDownSpeed,
            angle,
            allowWallSlide: false,
            allowLedgeHop: false);
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

        if (!TopDownDiving || RingEffects.RemovesDiveTimer(_inventory))
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
        _topDownDiveCounter = 0;
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
            allowWallSlide: true,
            allowLedgeHop: false);
    }

    private void FinalizeTopDownSwimmingUpdate(Vector2 movementStart)
    {
        _walking = false;
        _pushing = false;
        UpdateHeartRingCounter(_precisePosition - movementStart);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (!_world.CheckTileWarp(this))
            _world.CheckRoomExit(this);
        AdvanceTransformationAnimation(walking: false);
        AdvanceTerrainWalkAnimation(walking: false);
        QueueRedraw();
    }

    private void ClearTopDownSwimmingState()
    {
        _topDownSwimmingState = 0;
        _topDownSwimmingEntryCounter = 0;
        _topDownSwimAngle = 0xff;
        _topDownSwimSpeedRaw = 0;
        _topDownSwimTargetSpeedRaw = 0;
        _topDownSwimVelocityCounter = 0;
        _topDownSwimBurstState = 0;
        _topDownSwimBurstCounter = 0;
        _topDownSwimAnimationFrame = 0;
        _topDownSwimAnimationCounter = 0;
        _topDownDiving = false;
        _topDownDiveCounter = 0;
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
        SetSideScrollTerrainSpeed(
            terrainMode: 0,
            initialSpeed: parameters.NormalSpeed,
            velocityInterval: 0,
            targetSpeed: parameters.NormalSpeed,
            writeSpeedDirectly: true);
        _sideScrollAirborne = true;
        _sideScrollJumpSoundPending = jumped;
        _sideScrollReducedGravity = false;
        _sideScrollClimbing = false;
        _sideScrollAnimationPhase = 0;
        _sideScrollAnimationCounter =
            parameters.AnimationPhaseDurations[0];
        _airborneLinkAnimationMode = IsAttacking
            ? AirborneLinkAnimationMode.Walk
            : AirborneLinkAnimationMode.Jump;
        _walking = false;
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
        _sideScrollClimbing = false;
        _sideScrollAnimationPhase = 0;
        _sideScrollAnimationCounter = 0;
        _airborneLinkAnimationMode = AirborneLinkAnimationMode.None;
        ResetLinkWalkAnimation();
        _walking = false;
        _rocsCapeButtonAction = null;
        _sideScrollReducedGravity = false;
        SideScrollTerrainState terrain =
            _world.GetSideScrollTerrain(_precisePosition);
        if (terrain.ActiveTile == parameters.SpikeTile)
            ApplySideScrollSpikeDamage();
        _world.PlaySound(parameters.LandSound);
    }

    private void ApplySideScrollSpikeDamage()
    {
        if (_world.RidingObject || _enemyInvincibilityFrames > 0.0f ||
            !ApplyDamage(4, RingDamageSource.Spike))
        {
            return;
        }

        _enemyInvincibilityFrames = 40.0f;
        _enemyKnockbackFrames += 10.0f;
        _swordCollisionKnockback = false;
        int knockbackAngle = _sideScrollAngle < 0x80
            ? (_sideScrollAngle ^ 0x10)
            : 0xff;
        _enemyKnockbackDirection = knockbackAngle < 0x80
            ? OracleObjectMovement.Shared.Direction(knockbackAngle)
            : Vector2.Zero;
        _world.PlaySound(OracleSoundEngine.SndDamageLink);
    }

    private void AdvanceSideScrollAirAnimation(
        SideScrollPlayerParameters parameters)
    {
        if (_airborneLinkAnimationMode == AirborneLinkAnimationMode.Walk)
        {
            AdvanceLinkWalkAnimation();
            return;
        }

        if (_sideScrollAnimationPhase >=
            parameters.AnimationPhaseDurations.Length)
            return;
        if (--_sideScrollAnimationCounter > 0)
            return;

        _sideScrollAnimationPhase++;
        _sideScrollAnimationCounter = _sideScrollAnimationPhase <
            parameters.AnimationPhaseDurations.Length
                ? parameters.AnimationPhaseDurations[_sideScrollAnimationPhase]
                : 0;
    }

    private void AdvanceSideScrollSwimming(
        Vector2 input,
        bool movementAllowed,
        SideScrollPlayerParameters parameters)
    {
        _sideScrollAirborne = false;
        _airborneLinkAnimationMode = AirborneLinkAnimationMode.None;
        ResetLinkWalkAnimation();
        _sideScrollSpeedZ = 0;
        _sideScrollReducedGravity = false;
        _rocsCapeButtonAction = null;
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
            _sideScrollSpeedRaw = swimSpeed;
            _sideScrollTargetSpeedRaw = swimSpeed;
            _sideScrollVelocityCounter = 3;
            _sideScrollVelocityInterval = 0;
            _sideScrollTerrainMode = -1;
            if (!_inventory.HasTreasure(TreasureDatabase.TreasureFlippers))
            {
                _sideScrollSwimmingState = 3;
                StartSideScrollDrowningWithoutEntryEffects();
            }
            return;
        }

        if (_sideScrollSwimmingState == 3)
            return;

        int inputAngle = movementAllowed
            ? AngleForVector(input)
            : 0xff;
        bool mermaidSuit =
            _inventory.HasTreasure(TreasureDatabase.TreasureMermaidSuit);
        if (mermaidSuit)
        {
            UpdateSideScrollMermaidSuit(
                inputAngle, movementAllowed, parameters);
        }
        else
        {
            UpdateSideScrollFlippers(inputAngle, parameters);
        }

        if (_sideScrollAngle < 0x80 && _sideScrollSpeedRaw != 0)
        {
            ApplySideScrollVelocity(
                _sideScrollSpeedRaw,
                _sideScrollAngle,
                allowWallSlide: true);
            UpdateFacingFromSideScrollAngle(_sideScrollAngle);
            _walking = inputAngle < 0x80;
        }
        _sideScrollYFixed =
            Mathf.FloorToInt(_precisePosition.Y * 256.0f);

        _sideScrollBubbleCounter =
            (_sideScrollBubbleCounter - 1) & 0xff;
        if ((_sideScrollBubbleCounter & 0x80) != 0)
        {
            // linkUpdateSwimming_sidescroll consumes the shared RNG before
            // creating INTERAC_BUBBLE $91. Bubble rendering is independent of
            // movement, but the RNG call is gameplay-observable.
            _sideScrollBubbleCounter =
                (_random.Next().Value & 0x1f) + 50;
        }
    }

    private void UpdateSideScrollFlippers(
        int inputAngle,
        SideScrollPlayerParameters parameters)
    {
        int baseSpeed = RingEffects.UsesFastSwim(_inventory)
            ? parameters.FastSwimSpeed
            : parameters.SwimSpeed;
        if (_sideScrollSwimBurstState == 0)
        {
            if (!Input.IsActionJustPressed("attack"))
            {
                _sideScrollTargetSpeedRaw = baseSpeed;
                UpdateSideScrollVelocity(inputAngle, inAir: false);
                return;
            }

            _sideScrollSwimBurstState = 1;
            for (int update = 0; update < 8; update++)
            {
                UpdateSideScrollVelocity(
                    ((int)_facing * 8) & 0x1f,
                    inAir: false);
            }
            _sideScrollSwimBurstCounter = 0x0d;
            _world.PlaySound(OracleSoundEngine.SndLinkSwim);
        }

        _sideScrollSwimBurstCounter =
            (_sideScrollSwimBurstCounter - 1) & 0xff;
        if (_sideScrollSwimBurstCounter == 0)
        {
            if (_sideScrollSwimBurstState == 1)
            {
                _sideScrollSwimBurstState = 2;
                _sideScrollSwimBurstCounter = 0x0c;
            }
            else
            {
                _sideScrollSwimBurstState = 0;
                _sideScrollSpeedRaw = baseSpeed;
                _sideScrollTargetSpeedRaw = baseSpeed;
                _sideScrollVelocityCounter = 3;
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
            int amount = _sideScrollSwimBurstState == 1 ? 5 : -5;
            _sideScrollTargetSpeedRaw =
                Math.Max(0, _sideScrollTargetSpeedRaw + amount);
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
        SideScrollPlayerParameters parameters)
    {
        SetSideScrollTerrainSpeed(
            terrainMode: 0x10,
            initialSpeed: 0,
            velocityInterval: 5,
            targetSpeed: RingEffects.UsesFastSwim(_inventory)
                ? parameters.FastMermaidTargetSpeed
                : parameters.MermaidTargetSpeed,
            writeSpeedDirectly: false);

        bool directionJustPressed =
            movementAllowed &&
            (Input.IsActionJustPressed("move_up") ||
             Input.IsActionJustPressed("move_right") ||
             Input.IsActionJustPressed("move_down") ||
             Input.IsActionJustPressed("move_left"));
        if (directionJustPressed)
        {
            _world.PlaySound(OracleSoundEngine.SndSplash);
            _sideScrollMermaidImpulseCounter = 4;
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

        _sideScrollVelocityCounter = 0x14;
        UpdateSideScrollVelocity(inputAngle, inAir: false);
    }

    private void StartSideScrollDrowningWithoutEntryEffects()
    {
        _drowningHazard = HazardType.Water;
        _drowning = true;
        _drownRespawning = false;
        _drownTime = 0.0f;
        _drownInvisibleTime = 0.0f;
        _walking = false;
        CancelSwordAttack();
        CancelShovelAction();
        Visible = true;
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
            int relative = (inputAngle - angle + 4) & 0x1f;
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
                        angle = inputAngle ^ 0x10;
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
        angle = (angle + angleStep) & 0x1f;

        int speed = speedRaw + speedStep;
        if (speed <= 0)
        {
            speedRaw = 0;
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
        if (speed == 0 || angle >= 0x80)
            return false;
        Vector2 movement = OracleObjectMovement.Shared.Delta(speed, angle);
        Vector2 resolved = _world.ResolveMovement(
            _precisePosition,
            movement,
            allowWallSlide);
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
        angle < 0x80 && ((angle + 4) & 0x1f) < 9;

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
        if (_rocsCapeButtonAction is null)
            return;
        if (!_sideScrollAirborne ||
            _sideScrollReducedGravity ||
            !Input.IsActionPressed(_rocsCapeButtonAction))
        {
            _rocsCapeButtonAction = null;
            return;
        }
        if (_sideScrollSpeedZ < 0 ||
            _sideScrollSpeedZ > 0x0100)
        {
            return;
        }

        _sideScrollSpeedZ =
            _world.SideScrollParameters.RocsCapeSpeedZ;
        _sideScrollReducedGravity = true;
        _rocsCapeButtonAction = null;
        _world.PlaySound(OracleSoundEngine.SndThrow);
    }

    internal bool ActivateRocsCapeForValidation()
    {
        if (!_sideScrollAirborne ||
            _inventory.FeatherLevel < 2 ||
            _sideScrollReducedGravity ||
            _sideScrollSpeedZ < 0 ||
            _sideScrollSpeedZ > 0x0100)
        {
            return false;
        }
        _sideScrollSpeedZ =
            _world.SideScrollParameters.RocsCapeSpeedZ;
        _sideScrollReducedGravity = true;
        _world.PlaySound(OracleSoundEngine.SndThrow);
        return true;
    }

    private void BeginSideScrollInstantRespawn()
    {
        InterruptCarriedItems(discard: true);
        CancelSwordAttack();
        CancelShovelAction();
        _precisePosition = _lastSafePosition;
        _facing = _localRespawnFacing;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        ClearSideScrollState(_precisePosition);
        _sideScrollInstantRespawnCounter = 2;
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
            _sideScrollSquishAnimationCounter = 0x2d;
            InterruptCarriedItems(discard: true);
            CancelSwordAttack();
            CancelShovelAction();
            _world.PlaySound(OracleSoundEngine.SndDamageEnemy);
            _walking = false;
            QueueRedraw();
            return;
        }
        if (_sideScrollSquishAnimationCounter != 0)
        {
            _sideScrollSquishAnimationCounter--;
            if (_sideScrollSquishAnimationCounter == 0)
                _sideScrollSquishFlickerCounter = 0x14;
            QueueRedraw();
            return;
        }

        bool visibleUpdate = (_world.FrameCounter & 1) == 0;
        Visible = visibleUpdate;
        if (visibleUpdate)
        {
            _sideScrollSquishFlickerCounter--;
            if (_sideScrollSquishFlickerCounter == 0)
                BeginSideScrollInstantRespawn();
        }
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
        _sideScrollMermaidImpulseCounter = 0;
        _sideScrollBubbleCounter = 0;
        _sideScrollSquishVertical = false;
        _sideScrollPreviousActiveType = SideScrollTileType.None;
        _rocsCapeButtonAction = null;
        _sideScrollReducedGravity = false;
        _sideScrollAnimationPhase = 0;
        _sideScrollAnimationCounter = 0;
        _sideScrollAirborne = false;
        _airborneLinkAnimationMode = AirborneLinkAnimationMode.None;
        _sideScrollJumpSoundPending = false;
        _sideScrollClimbing = false;
    }

    internal void UpdatePushingState(Vector2 movementInput)
    {
        _lastMovementInput = movementInput;
        _pushing = movementInput.LengthSquared() > 0.01f && !IsUsingItem &&
            _world.IsPushingAgainstWall(_precisePosition, FacingVector, movementInput);
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

    private int GetWalkAnimationFrame() =>
        _walking ? _linkWalkAnimationFrame : 0;

    private void ResetLinkWalkAnimation()
    {
        // animateLinkStanding forces animMode away from WALK and immediately
        // reselects it, loading animationData19f0b's two-update $54 frame.
        _linkWalkAnimationFrame = 0;
        _linkWalkAnimationCounter = 2;
    }

    private void AdvanceLinkWalkAnimation()
    {
        if (--_linkWalkAnimationCounter > 0)
            return;

        // After WALK's initial two-update $54 frame, its graphics alternate
        // between $80 and $54 in six-update entries. The longer source table
        // varies only animation parameters used for footsteps and dust.
        _linkWalkAnimationFrame ^= 1;
        _linkWalkAnimationCounter = 6;
    }

    internal bool TryBlockWithShield(Rect2 targetBounds, int minimumLevel = 1)
    {
        if (!IsUsingShield || _inventory.ShieldLevel < minimumLevel)
            return false;

        Rect2 shield = ShieldCollisionBounds;
        Vector2 shieldCenter = shield.GetCenter();
        Vector2 shieldRadius = shield.Size / 2.0f;
        Vector2 targetCenter = OracleObjectMath.ToPixelPosition(targetBounds.GetCenter());
        Vector2 targetRadius = targetBounds.Size / 2.0f;
        if (Mathf.Abs(targetCenter.X - shieldCenter.X) >=
                targetRadius.X + shieldRadius.X ||
            Mathf.Abs(targetCenter.Y - shieldCenter.Y) >=
                targetRadius.Y + shieldRadius.Y)
        {
            return false;
        }

        // Projectile modes $06/$07 select COLLISIONEFFECT_$1f for shield
        // collision types $01-$03. LINKDMG_$20 contributes only SND_CLINK2;
        // the projectile receives ENEMYDMG_$34 and enters its bounce state.
        _world.PlaySound(OracleSoundEngine.SndClink2);
        return true;
    }

    internal void UpdateShieldForValidation(bool attackHeld, bool itemHeld) =>
        UpdateShieldState(attackHeld, itemHeld);

    private void UpdateShieldState(bool attackHeld, bool itemHeld)
    {
        if ((_shieldParentButton == 1 &&
                (!attackHeld || _inventory.EquippedA != InventoryState.ItemShield)) ||
            (_shieldParentButton == 2 &&
                (!itemHeld || _inventory.EquippedB != InventoryState.ItemShield)))
        {
            ClearShieldParent();
        }

        bool shieldUsable = _activeTransformation == 0 &&
            !_world.ItemUsageDisabled && !_cutsceneControlled &&
            !_minecartRideControlled &&
            !_drowning && !_fallingInHole &&
            _ledgeJumpState == LedgeJumpState.None &&
            !IsFloorDoorRespawning && _inventory.ShieldLevel > 0;
        if (!shieldUsable)
        {
            ClearShieldParent();
            return;
        }

        if (_shieldParentButton == 0)
        {
            if (attackHeld && _inventory.EquippedA == InventoryState.ItemShield)
                _shieldParentButton = 1;
            else if (itemHeld && _inventory.EquippedB == InventoryState.ItemShield)
                _shieldParentButton = 2;
        }

        bool next = _shieldParentButton != 0 &&
            !IsUsingItem &&
            !IsCarryingObject &&
            !_world.BombParentActive;
        if (next && !_shieldParentInitialized)
        {
            _shieldParentInitialized = true;
            _world.PlaySound(_linkItems.Constants.ShieldSound);
        }
        if (_usingShield == next)
            return;
        _usingShield = next;
        QueueRedraw();
    }

    private void ClearShieldParent()
    {
        bool redraw = _usingShield;
        _shieldParentButton = 0;
        _shieldParentInitialized = false;
        _usingShield = false;
        if (redraw)
            QueueRedraw();
    }

    private void SuspendShield()
    {
        if (!_usingShield)
            return;
        _usingShield = false;
        QueueRedraw();
    }

    private void DrawShieldPose()
    {
        int variant = (IsUsingShield ? 2 : 0) +
            (_inventory.ShieldLevel >= 2 ? 1 : 0);
        int frame = GetWalkAnimationFrame();
        DrawTextureRectRegion(
            DamagePaletteActive
                ? _damageShieldLinkTexture
                : _shieldLinkTexture,
            new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
            new Rect2(variant * 32 + frame * 16,
                (int)_facing * 16, 16, 16));
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
        if (!movementAllowed || angle >= 0x80)
            return false;

        // parentItemLoadAnimationAndIncState disables Link's turning for the
        // sword's full lifetime, even after state 6 re-enables movement.
        if (!IsUsingItem)
            UpdateFacing(input);

        ApplyTopDownObjectSpeed(
            GetTopDownMovementSpeed(), angle,
            allowWallSlide: true);
        return true;
    }

    private void ApplyTopDownObjectSpeed(
        int speed,
        int angle,
        bool allowWallSlide,
        bool allowLedgeHop = true)
    {
        OracleObjectPosition position =
            OracleObjectMovement.Shared.PositionFromPixels(_precisePosition);
        _precisePosition = position.PrecisePosition;

        OracleObjectVelocity velocity =
            OracleObjectMovement.Shared.Velocity(speed, angle);
        Vector2 movement = new(
            velocity.XFixed / 256.0f,
            velocity.YFixed / 256.0f);
        Vector2 resolved = _world.ResolveMovement(
            _precisePosition, movement, allowWallSlide);
        if (resolved != Vector2.Zero)
        {
            position = position.Add(
                Mathf.RoundToInt(resolved.Y * 256.0f),
                Mathf.RoundToInt(resolved.X * 256.0f));
            _precisePosition = position.PrecisePosition;
            return;
        }

        if (allowLedgeHop && !IsUsingItem)
            _world.TryStartLedgeHop(this, _precisePosition, movement);
    }

    private int GetTopDownMovementSpeed()
    {
        if (_world.RidingObject)
            return NormalTopDownSpeed;
        TerrainType terrain = _world.GetActiveTerrain(Position).Terrain.Type;
        return terrain switch
        {
            TerrainType.Grass or TerrainType.Puddle => GrassTopDownSpeed,
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
            TerrainType.Grass or TerrainType.Puddle => normalSpeed * 3 / 4,
            TerrainType.Stairs or TerrainType.Vines => slowSpeed,
            _ => normalSpeed
        };
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
            terrain.Hazard != HazardType.Water)
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
        _holePullCounter = 0;
        _holePullPackedPosition = -1;
    }

    private void StartPullIntoHole(ActiveTerrainInfo activeTerrain)
    {
        _pullingIntoHole = true;
        _holePullCenter = activeTerrain.TileCenter;
        _holePullPackedPosition = activeTerrain.PackedPosition;
        _holePullCounter = 0;
        _walking = false;
        CancelSwordAttack();
        CancelShovelAction();
        QueueRedraw();
    }

    private bool UpdatePullIntoHole()
    {
        ActiveTerrainInfo activeTerrain = _world.GetActiveTerrain(Position);
        if (activeTerrain.Terrain.Hazard == HazardType.Hole)
        {
            if (activeTerrain.PackedPosition != _holePullPackedPosition)
            {
                _holePullCenter = activeTerrain.TileCenter;
                _holePullPackedPosition = activeTerrain.PackedPosition;
                _holePullCounter = 0;
            }
        }
        else if (_holePullCounter < 16)
        {
            _pullingIntoHole = false;
            _holePullPackedPosition = -1;
            return false;
        }

        _holePullCounter++;

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

        if (Mathf.Abs(_precisePosition.X - _holePullCenter.X) < 3.0f &&
            Mathf.Abs(_precisePosition.Y - _holePullCenter.Y) < 3.0f)
        {
            StartFallInHole(_holePullCenter);
            return true;
        }

        if (_holePullCounter >= 16)
        {
            _walking = false;
            CancelSwordAttack();
            CancelShovelAction();
            QueueRedraw();
            return true;
        }

        return false;
    }

    private void StartFallInHole(Vector2 holeCenter)
    {
        _pullingIntoHole = false;
        _holePullPackedPosition = -1;
        _fallingInHole = true;
        _fallInHoleRespawning = false;
        _fallInHoleWarpPending = false;
        _fallInHoleTime = 0.0f;
        _fallInHoleInvisibleTime = FallInHoleInvisibleDuration;
        _walking = false;
        CancelSwordAttack();
        CancelShovelAction();

        // LINK_STATE_RESPAWNING parameter $00 starts SND_LINK_FALL ($65) on
        // the same update that it selects LINK_ANIM_MODE_FALLINHOLE.
        _world.PlaySound(OracleSoundEngine.SndLinkFall);

        // The active hazard tile is selected by the same +5px sample used by
        // objectGetRelativeTile($0500). Carry its center through explicitly so
        // rounded-vs-precise coordinates cannot recenter Link on a neighboring
        // solid tile at tile boundaries.
        _precisePosition = holeCenter;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        Visible = true;
        QueueRedraw();
    }

    private static float MoveOnePixelToward(float value, float target)
    {
        if (Mathf.Abs(value - target) <= 1.0f)
            return target;
        return value < target ? value + 1.0f : value - 1.0f;
    }

    private static int GetTerrainHazardDamageQuarters(HazardType hazard)
    {
        // The original LINK_STATE_RESPAWNING path applies damageToApply=$fc
        // after Link reappears; linkApplyDamage consumes that as two
        // quarter-hearts, ie. a half-heart.
        return hazard == HazardType.None ? 0 : TerrainHazardDamageQuarters;
    }

    private void StartDrowning(HazardType hazard)
    {
        _drowningHazard = hazard;
        _drowning = true;
        _drownRespawning = false;
        _drownTime = 0.0f;
        _drownInvisibleTime = 0.0f;
        _walking = false;
        CancelSwordAttack();
        CancelShovelAction();
        Visible = true;

        // overworldSwimmingState1 requests SND_DAMAGE_LINK ($5f) before it
        // selects LINK_ANIM_MODE_DROWN and creates the splash interaction.
        _world.PlaySound(OracleSoundEngine.SndDamageLink);
        _world.SpawnDrowningSplash(Position, hazard);
        QueueRedraw();
    }

    private void UpdateDrowning(float delta)
    {
        if (!_drownRespawning)
        {
            _drownTime += delta;
            if (_drownTime < DrownAnimationDuration)
            {
                QueueRedraw();
                return;
            }

            delta = _drownTime - DrownAnimationDuration;
            _drownTime = DrownAnimationDuration;
            _drownRespawning = true;
            _drownInvisibleTime = DrownInvisibleDuration;
            MoveToLocalHazardRespawn();
            Visible = false;
            QueueRedraw();
        }

        _drownInvisibleTime -= delta;
        if (_drownInvisibleTime > 0.0f)
            return;

        ApplyDamage(
            GetTerrainHazardDamageQuarters(_drowningHazard),
            RingDamageSource.TerrainHazard);
        WarpTo(_lastSafePosition);
        _enemyInvincibilityFrames = 0x3c;
        _hazardRecoveryTime = HazardRecoveryDuration;
        _walking = false;
        CancelSwordAttack();
        CancelShovelAction();
        QueueRedraw();
    }

    internal int DrownAnimationFrame => GetDrownAnimationFrame();

    private int GetDrownAnimationFrame()
    {
        // LINK_ANIM_MODE_DROWN ($0a) holds directional frame $d4 for six
        // updates, then frame $0b for sixteen updates before setting bit 7 of
        // animParameter. Direction is added to $d4 by the graphics loader.
        return _drownTime < 6.0f / 60.0f ? 0 : 1;
    }

    private void UpdateFallInHole(float delta)
    {
        if (_fallInHoleWarpPending)
            return;

        if (!_fallInHoleRespawning)
        {
            _fallInHoleTime += delta;
            if (_fallInHoleTime >= FallInHoleAnimationDuration)
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
                _fallInHoleInvisibleTime = FallInHoleInvisibleDuration;
                MoveToLocalHazardRespawn();
                Visible = false;
            }
            QueueRedraw();
            return;
        }

        _fallInHoleInvisibleTime -= delta;
        if (_fallInHoleInvisibleTime > 0.0f)
            return;

        ApplyDamage(
            GetTerrainHazardDamageQuarters(HazardType.Hole),
            RingDamageSource.Hole);
        WarpTo(_lastSafePosition);
        _enemyInvincibilityFrames = 0x3c;
        _hazardRecoveryTime = HazardRecoveryDuration;
        _walking = false;
        CancelSwordAttack();
        CancelShovelAction();
        QueueRedraw();
    }

    private void MoveToLocalHazardRespawn()
    {
        // specialObjectSetCoordinatesToRespawnYX moves Link before substate 2's
        // two-update invisible wait. The following wEnteredWarpPosition write
        // suppresses any warp tile under the saved local anchor.
        _precisePosition = _lastSafePosition;
        _facing = _localRespawnFacing;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        _world.DeactivateWarpAtPlayerPosition(this);
    }

    private int GetFallInHoleFrame()
    {
        float frames = _fallInHoleTime * 60.0f;
        if (frames < 16.0f)
            return 0;
        if (frames < 26.0f)
            return 1;
        return 2;
    }

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
        _world.ApplyLandedTileHit(_precisePosition);
        int landSound = _ledgeLandSound;
        ClearLedgeHop();
        _walking = false;
        _pushing = false;
        _world.PlaySound(landSound);
    }

    private void AdvanceLedgeAnimation()
    {
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
        if (!IsAttacking || _swordState == SwordActionState.Poke)
            return new Rect2(Position, Vector2.Zero);
        return GetSwordHitbox(Position, GetSwordArcIndex());
    }

    internal static Rect2 GetSwordHitboxForValidation(Vector2 position, int arcIndex) =>
        GetSwordHitbox(position, arcIndex);

    private static Rect2 GetSwordHitbox(Vector2 position, int arcIndex)
    {
        SwordArc arc = LinkItemDatabase.Shared.SwordArc(arcIndex);
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
        if (IsUsingShovel || IsUsingSeedSatchel)
            return;
        if (IsAttacking && !SwordCanRestart)
            return;
        if (facingInput.LengthSquared() > 0.01f)
            UpdateFacing(facingInput);
        _swordState = SwordActionState.Swing;
        _swordStateFrame = 0;
        _swordChargeCounter = _linkItems.Constants.SwordChargeCounter;
        _swordFrameAccumulator = 0.0;
        _swordButtonAction = buttonAction;
        _swordPokeReturnsToHeld = false;
        _walking = false;
        int sound = _linkItems.SwordSlashSound(
            _random.Next().Value & 0x07);
        _world.PlaySound(sound);
        byte whimsicalRoll = 0xff;
        if (_inventory.IsRingActive(RingId.Whimsical))
        {
            whimsicalRoll = _random.Next().Value;
            if (whimsicalRoll == 0)
                _world.PlaySound(OracleSoundEngine.SndLightning);
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
        bool changed = IsAttacking;
        _swordState = SwordActionState.None;
        _swordStateFrame = 0;
        _swordChargeCounter = 0;
        _swordFrameAccumulator = 0.0;
        _swordButtonAction = null;
        _swordPokeReturnsToHeld = false;
        _currentSwordDamage = 0;
        _doubleEdgedDamagePending = false;
        if (changed)
            QueueRedraw();
    }

    public void StartShovelAction() => StartShovelAction(Vector2.Zero);

    internal void StartShovelActionForValidation(Vector2 facingInput) =>
        StartShovelAction(facingInput);

    private void StartShovelAction(Vector2 facingInput)
    {
        if (IsUsingItem)
            return;
        if (facingInput.LengthSquared() > 0.01f)
            UpdateFacing(facingInput);
        _usingShovel = true;
        _shovelFrame = 0;
        _shovelFrameAccumulator = 0.0;
        _walking = false;
        _pushing = false;
        QueueRedraw();
    }

    private void CancelShovelAction()
    {
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

    private void StartHarpAction()
    {
        if (IsUsingItem || !IsGroundedForFloorButton ||
            _pullingIntoHole || _drowning || _fallingInHole)
        {
            return;
        }
        int frames = _world.BeginHarp(this);
        if (frames <= 0)
            return;
        _usingHarp = true;
        _harpSong = _inventory.SelectedHarpSong;
        _harpActionUpdate = 0;
        _harpActionFrames = frames;
        _harpFrameAccumulator = 0.0;
        BeginPlayableHarpPose();
    }

    private void AdvanceHarpAction()
    {
        _harpActionUpdate++;
        _world.AdvanceHarp(this, _harpActionUpdate);
        AdvanceHarpPose();
        if (_harpActionUpdate < _harpActionFrames)
            return;

        int completedSong = _harpSong;
        _usingHarp = false;
        _harpSong = 0;
        _harpActionUpdate = 0;
        _harpActionFrames = 0;
        _harpFrameAccumulator = 0.0;
        EndHarpPose();
        _world.CompleteHarp(this, completedSong);
    }

    private void CancelHarpAction()
    {
        bool changed = IsUsingHarp;
        _usingHarp = false;
        _harpSong = 0;
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
            _inventory.EquippedA == InventoryState.ItemNone &&
            _inventory.EquippedB == InventoryState.ItemNone))
        {
            return;
        }
        if (facingInput.LengthSquared() > 0.01f)
            UpdateFacing(facingInput);
        _usingPunch = true;
        _expertPunch = RingEffects.UsesExpertPunch(_inventory);
        _punchFrame = 0;
        _punchDamage = _expertPunch ? 4 : 1;
        _punchFrameAccumulator = 0.0;
        _walking = false;
        _pushing = false;
        if (_expertPunch)
            _world.ApplyExpertsRingTileHit(this, (int)_facing * 2);
        _world.PlaySound(_expertPunch
            ? OracleSoundEngine.SndExplosion
            : OracleSoundEngine.SndStrike);
        ApplyPunchCollision();
        QueueRedraw();
    }

    private void AdvancePunchFrame()
    {
        _punchFrame++;
        if (_punchFrame < PunchCollisionFrames)
            ApplyPunchCollision();
        int duration = _expertPunch ? ExpertPunchFrames : FistPunchFrames;
        if (_punchFrame >= duration)
            CancelPunchAction();
    }

    private void ApplyPunchCollision()
    {
        if (!IsUsingPunch || _punchFrame >= PunchCollisionFrames)
            return;
        _world.ApplySwordHit(
            this, GetSwordHitbox(Position, 24 + (int)_facing));
    }

    private void CancelPunchAction()
    {
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

    internal void StartSeedSatchelActionForValidation(Vector2 facingInput) =>
        StartSeedSatchelAction(facingInput);

    private void StartSeedSatchelAction(Vector2 facingInput)
    {
        if (IsUsingItem)
            return;
        if (facingInput.LengthSquared() > 0.01f)
            UpdateFacing(facingInput);
        int actionFrames = _world.TryUseSeedSatchel(this);
        if (actionFrames <= 0)
            return;
        _usingSeedSatchel = true;
        _seedSatchelFrame = 0;
        _seedSatchelActionFrames = actionFrames;
        _seedSatchelFrameAccumulator = 0.0;
        _walking = false;
        _pushing = false;
        QueueRedraw();
    }

    private void AdvanceSeedSatchelFrame()
    {
        _seedSatchelFrame++;
        if (_seedSatchelFrame >= _seedSatchelActionFrames)
            CancelSeedSatchelAction();
    }

    private void CancelSeedSatchelAction()
    {
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

    internal void AdvanceShovelForValidation(int frames)
    {
        for (int frame = 0; frame < frames && IsUsingShovel; frame++)
            AdvanceShovelFrame();
        QueueRedraw();
    }

    private void AdvanceShovelFrame()
    {
        _shovelFrame++;
        if (_shovelFrame == _linkItems.Constants.ShovelDigFrame)
        {
            _world.DigWithShovel(
                Position + ShovelChildOffset,
                FacingVector);
        }
        if (_shovelFrame >= _linkItems.Constants.ShovelActionFrames)
        {
            CancelShovelAction();
            return;
        }
        QueueRedraw();
    }

    private bool IsSwordButtonHeld() =>
        _swordButtonAction is not null && Input.IsActionPressed(_swordButtonAction);

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
        switch (_swordState)
        {
            case SwordActionState.Swing:
                _swordStateFrame++;
                if (_swordStateFrame ==
                    _linkItems.Constants.SwordTileHitFrame)
                {
                    _world.ApplySwordTileHit(this, (int)_facing * 2, swordPoke: false);
                    TryCreateSwordBeamFromSwing();
                }
                if (_swordStateFrame >=
                    _linkItems.Constants.SwordSwingFrames)
                {
                    // swordParent state 6 deletes the sword when Link's main
                    // object is SPECIALOBJECT_MINECART. The swing is usable,
                    // but cannot be held or charged during the ride.
                    if (!buttonHeld || _minecartRideControlled)
                    {
                        CancelSwordAttack();
                        return;
                    }
                    EnterSwordHeldState();
                }
                ApplySwordCollision();
                break;

            case SwordActionState.Held:
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
                    _world.PlaySound(OracleSoundEngine.SndChargeSword);
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
                    _linkItems.Constants.SwordPokeFrames)
                    break;
                if (_swordPokeReturnsToHeld && buttonHeld)
                    EnterSwordHeldState();
                else
                    CancelSwordAttack();
                break;

            case SwordActionState.Spin:
                int previousPhase = GetSpinArcPhase();
                _swordStateFrame++;
                if (_swordStateFrame >= RingEffects.SwordSpinFrames(
                    _inventory, _linkItems.Constants.SwordSpinFrames))
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
        _swordState = SwordActionState.Held;
        _swordStateFrame = 0;
        _swordChargeCounter = _linkItems.Constants.SwordChargeCounter;
        _swordPokeReturnsToHeld = false;
    }

    private bool CheckSwordPoke(Vector2 movementInput)
    {
        if (!_world.IsPushingAgainstWall(_precisePosition, FacingVector, movementInput))
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
    }

    private void TriggerSwordPoke(bool returnsToHeld)
    {
        _swordState = SwordActionState.Poke;
        _swordStateFrame = 0;
        _swordPokeReturnsToHeld = returnsToHeld;
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
        _swordState = SwordActionState.Spin;
        _swordStateFrame = 0;
        _walking = false;
        _world.PlaySound(OracleSoundEngine.SndSwordSpin);
        _world.ApplySwordTileHit(this, (int)_facing * 2, swordPoke: false);
        ApplySwordCollision();
    }

    private bool ApplySwordCollision()
    {
        Rect2 hitbox = GetSwordHitbox();
        if (hitbox.Size == Vector2.Zero ||
            !_world.ApplySwordHit(this, hitbox))
        {
            return false;
        }
        if (!_doubleEdgedDamagePending)
            return true;
        // swordParent.s applies $f8 (four quarter-hearts) once after the first
        // accepted enemy contact, and clears var3a so later overlap frames do
        // not hurt Link again. The health >= $05 check occurs at swing start.
        ApplyUnmodifiedDamage(4);
        _doubleEdgedDamagePending = false;
        return true;
    }

    private void UpdateHeartRingCounter(Vector2 movement)
    {
        (int threshold, int heal) = RingEffects.HeartRefill(_inventory);
        if (threshold == 0)
        {
            _heartRingDistanceFixed = 0;
            return;
        }
        int distanceFixed = Mathf.RoundToInt(
            (Mathf.Abs(movement.X) + Mathf.Abs(movement.Y)) * 256.0f);
        _heartRingDistanceFixed = Math.Min(
            int.MaxValue, _heartRingDistanceFixed + distanceFixed);
        if (_heartRingDistanceFixed < threshold)
            return;
        _inventory.Heal(heal);
        _heartRingDistanceFixed = 0;
    }

    private void RefreshTransformationState()
    {
        int transformation = _world.RingTransformationsAllowed
            ? RingEffects.LinkTransformation(_inventory)
            : 0;
        if (transformation == _activeTransformation)
            return;
        _activeTransformation = transformation;
        _transformationFrame = 0;
        _transformationTicks = transformation == 0
            ? 0
            : _transformedLink.Record(
                transformation, (int)_facing, 0).InitialDuration;
        if (transformation != 0)
        {
            // transformedLink state 0 drops held objects and clears every
            // parent item before making the replacement special object live.
            CancelSwordAttack();
            CancelShovelAction();
            _pushing = false;
        }
        QueueRedraw();
    }

    private void AdvanceTransformationAnimation(bool walking)
    {
        if (_activeTransformation == 0)
            return;
        FrameRecord record = _transformedLink.Record(
            _activeTransformation, (int)_facing, _transformationFrame);
        if (!walking)
        {
            _transformationFrame = 0;
            _transformationTicks = record.InitialDuration;
            return;
        }
        if (_transformationTicks > 0)
            _transformationTicks--;
        if (_transformationTicks != 0)
            return;
        _transformationFrame ^= 1;
        _transformationTicks = record.LoopDuration;
    }

    internal void RefreshTransformationForValidation() =>
        RefreshTransformationState();

    internal void AdvanceTransformationForValidation(bool walking, int frames)
    {
        for (int frame = 0; frame < frames; frame++)
            AdvanceTransformationAnimation(walking);
        QueueRedraw();
    }

    private void DrawSwordPose()
    {
        Vector2 drawOffset = SwordDrawOffset;

        // The weapon item occupies an earlier visual layer than Link, so
        // Link's body masks the sword where their sprites overlap.
        DrawSword();
        if (_minecartRideControlled &&
            _swordState == SwordActionState.Swing)
        {
            // parentItemLoadAnimationAndIncState changes the Sword's Link
            // animation from mode $22 to $26 while the main object is the
            // minecart. Unlike ordinary phase 2, all four cart-swing body
            // frames retain the standard Link origin.
            int phase = GetSwordPosePhase();
            DrawTextureRectRegion(
                DamagePaletteActive
                    ? _damageMinecartAttackTexture
                    : _minecartAttackTexture,
                new Rect2(
                    NormalSpriteOrigin + drawOffset,
                    new Vector2(16, 16)),
                new Rect2(
                    phase * 16, (int)_facing * 16, 16, 16));
        }
        else if (_swordState is
            SwordActionState.Held or SwordActionState.Charged)
        {
            // swordParent state 6 clears Item.var3f to zero. func_4553 then
            // exposes Link's independently advancing animation: WALK when the
            // sword already owned turning at takeoff, or JUMP when the Feather
            // initialized first.
            if (_sideScrollAirborne || _topDownAirborne)
                DrawAirborneLinkBody(drawOffset);
            else
                DrawWalkLinkBody(GetWalkAnimationFrame(), drawOffset);
        }
        else
        {
            Facing poseFacing = GetSwordPoseFacing();
            int phase = GetSwordPosePhase();
            int texturePhase =
                _swordState == SwordActionState.Spin || phase == 3
                    ? 1
                    : phase;
            DrawTextureRectRegion(
                DamagePaletteActive ? _damageAttackTexture : _attackTexture,
                new Rect2(
                    AttackSpriteOrigin + drawOffset,
                    new Vector2(16, 16)),
                new Rect2(
                    texturePhase * 16,
                    (int)poseFacing * 16,
                    16,
                    16));
        }
    }

    private void DrawAirborneLinkBody(Vector2 drawOffset)
    {
        if (_airborneLinkAnimationMode == AirborneLinkAnimationMode.Walk)
        {
            DrawWalkLinkBody(_linkWalkAnimationFrame, drawOffset);
            return;
        }

        DrawTextureRectRegion(
            DamagePaletteActive
                ? _damageLedgeJumpTexture
                : _ledgeJumpTexture,
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
        if (IsShieldEquipped)
        {
            int variant = (IsUsingShield ? 2 : 0) +
                (_inventory.ShieldLevel >= 2 ? 1 : 0);
            DrawTextureRectRegion(
                DamagePaletteActive
                    ? _damageShieldLinkTexture
                    : _shieldLinkTexture,
                new Rect2(
                    NormalSpriteOrigin + drawOffset,
                    new Vector2(16, 16)),
                new Rect2(
                    variant * 32 + frame * 16,
                    (int)_facing * 16,
                    16,
                    16));
            return;
        }

        DrawTextureRectRegion(
            DamagePaletteActive ? _damageTexture : _texture,
            new Rect2(
                NormalSpriteOrigin + drawOffset,
                new Vector2(16, 16)),
            GetFrame(_facing, frame));
    }

    private void DrawSword()
    {
        int animation = _swordState == SwordActionState.Spin
            ? GetSwordArcIndex() - 16
            : _linkItems.SwordAnimation(
                (int)_facing,
                GetSwordPosePhase());
        DrawTextureRectRegion(
            SwordUsesChargedPalette ? _chargedSwordTexture : _swordTexture,
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
        get => GetSwordSpritePosition(GetSwordArcIndex());
    }

    internal static Vector2 GetSwordSpritePositionForValidation(int arcIndex) =>
        GetSwordSpritePosition(arcIndex);

    private static Vector2 GetSwordSpritePosition(int arcIndex)
    {
        SwordArc arc = LinkItemDatabase.Shared.SwordArc(arcIndex);
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
        return (int)_facing + GetSwordPosePhase() * 4;
    }

    private int GetSpinArcPhase()
    {
        int phase = _linkItems.SpinPhase(_swordStateFrame);
        return phase == 8 ? 0 : phase;
    }

    private static Texture2D BuildLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_WALK uses base gfx indices $54 and $80, then adds
        // direction (UP, RIGHT, DOWN, LEFT). These resolve to the offsets and
        // OAM compositions below in specialObjectAnimationData.s. Up/down
        // alternate a mirrored composition of the same source tiles; they are
        // not neighboring 16x16 crops.
        WriteWalkFrame(output, source, Facing.Up, 0, 0x0000, false, damagePalette); // gfx $54, OAM $00
        WriteWalkFrame(output, source, Facing.Up, 1, 0x0000, true, damagePalette);  // gfx $80, OAM $01
        WriteWalkFrame(output, source, Facing.Right, 0, 0x0080, true, damagePalette); // gfx $55
        WriteWalkFrame(output, source, Facing.Right, 1, 0x00c0, true, damagePalette); // gfx $81
        WriteWalkFrame(output, source, Facing.Down, 0, 0x0200, false, damagePalette); // gfx $56
        WriteWalkFrame(output, source, Facing.Down, 1, 0x0200, true, damagePalette);  // gfx $82
        WriteWalkFrame(output, source, Facing.Left, 0, 0x0080, false, damagePalette); // gfx $57
        WriteWalkFrame(output, source, Facing.Left, 1, 0x00c0, false, damagePalette); // gfx $83

        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildSideScrollSquishTexture(bool vertical)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        // LINK_ANIM_MODE_SQUISHX/Y use graphics $32/$33. Their source
        // pointers are spr_link+$0ce0/$03c0 with special-object OAM $2d/$04.
        return NpcCharacter.BuildOamTexture(
            source,
            vertical
                ? "8,0,0,0;8,8,0,32"
                : "0,4,0,0;16,4,2,0",
            vertical ? 0x3c : 0xce,
            basePalette: 0);
    }

    private static Texture2D BuildGetItemOneHandTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_GETITEM1HAND ($0e) is the static graphics frame $05:
        // OAM $00, spr_link+$0da0, four tiles. The frame is below $54, so
        // loadLinkAndCompanionAnimationFrame_body does not add Link's direction.
        WriteLinkFrame(output, source, 0, 0, 0x0da0, false, damagePalette);
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildGetItemTwoHandTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_GETITEM2HAND ($0f) is static graphics frame $06:
        // OAM $04 mirrors the single spr_link+$0de0 cell into a 16-pixel body.
        WriteSymmetricLinkCell(output, source, 0, 0, 0x0de0, damagePalette);
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildFunnyJokeDanceTexture(
        bool right,
        bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_DANCELEFT/RIGHT ($08/$09) are static graphics
        // frames $1d/$1e: spr_link+$0d60 with OAM $00/$01.
        WriteLinkFrame(
            output, source, 0, 0, 0x0d60,
            mirroredOam: right,
            damagePalette: damagePalette);
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildGetItemOneHandRightTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_GETITEM1HAND_RIGHT ($1c) is static graphics frame
        // $07: the same spr_link+$0da0 cells as $0e, mirrored by OAM $01.
        WriteLinkFrame(
            output, source, 0, 0, 0x0da0,
            mirroredOam: true,
            damagePalette: damagePalette);
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildCarriedObjectLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // A finished grab leaves LINK_ANIM_MODE_WALK active. func_4553 adds
        // held-object variant $08 to its $54/$80 graphics frames, producing
        // the direction-aware $5c-$5f/$88-$8b frames below.
        WriteLinkFrame(output, source, 0, (int)Facing.Up * 16, 0x0040, false, damagePalette);       // $5c, OAM $00
        WriteLinkFrame(output, source, 0, (int)Facing.Right * 16, 0x01c0, true, damagePalette);    // $5d, OAM $01
        WriteLinkFrame(output, source, 0, (int)Facing.Down * 16, 0x0180, false, damagePalette);    // $5e, OAM $00
        WriteLinkFrame(output, source, 0, (int)Facing.Left * 16, 0x01c0, false, damagePalette);    // $5f, OAM $00
        WriteLinkFrame(output, source, 16, (int)Facing.Up * 16, 0x0040, true, damagePalette);      // $88, OAM $01
        WriteLinkFrame(output, source, 16, (int)Facing.Right * 16, 0x1140, true, damagePalette);  // $89, OAM $01
        WriteLinkFrame(output, source, 16, (int)Facing.Down * 16, 0x0180, true, damagePalette);   // $8a, OAM $01
        WriteLinkFrame(output, source, 16, (int)Facing.Left * 16, 0x1140, false, damagePalette);  // $8b, OAM $00

        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildMinecartLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // getLinkWalkingAnimation selects variant $01 while the main object is
        // SPECIALOBJECT_MINECART. Added to walk frames $54/$80, that resolves
        // to $58-$5b/$84-$87. Both animation phases intentionally use the
        // same seated pixels even though the cart and Link offset animate.
        for (int phase = 0; phase < 2; phase++)
        for (int facing = 0; facing < 4; facing++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("minecart", 0, phase, facing);
            if (record.OamIndex == 0x04)
            {
                WriteSymmetricLinkCell(
                    output, source, phase * 16, facing * 16,
                    record.ByteOffset, damagePalette);
            }
            else
            {
                WriteLinkFrame(
                    output, source, phase * 16, facing * 16,
                    record.ByteOffset, record.MirrorX, damagePalette);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildMinecartAttackTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_26 resolves the Sword's four 3/3/8/terminal body
        // phases to $c8-$cb, $cc-$cf, $cc-$cf, and seated $58-$5b.
        for (int phase = 0; phase < 4; phase++)
        for (int facing = 0; facing < 4; facing++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("minecart-attack", 0, phase, facing);
            if (record.OamIndex == 0x04)
            {
                WriteSymmetricLinkCell(
                    output, source, phase * 16, facing * 16,
                    record.ByteOffset, damagePalette);
            }
            else
            {
                WriteLinkFrame(
                    output, source, phase * 16, facing * 16,
                    record.ByteOffset, record.MirrorX, damagePalette);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D[,] BuildBraceletActionTextures(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        var result = new Texture2D[3, 4];
        for (int pose = 0; pose < result.GetLength(0); pose++)
        for (int direction = 0; direction < result.GetLength(1); direction++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("bracelet", pose, 0, direction);
            result[pose, direction] = NpcCharacter.BuildOamTexture(
                source,
                record.Oam,
                record.ByteOffset / 16,
                basePalette: damagePalette ? 5 : 0);
        }
        return result;
    }

    private Texture2D BuildShieldLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(128, 64, false, Image.Format.Rgba8);

        // func_4553 selects variants $05/$06 while the shield is merely
        // equipped and $07/$08 while wUsingShield is nonzero. Added to walk
        // frames $54/$80, these are $68-$77 and $94-$a3. Every entry uses
        // special-object OAM $00, so each source pair retains its native order.
        for (int variant = 0; variant < 4; variant++)
        for (int facing = 0; facing < 4; facing++)
        for (int phase = 0; phase < 2; phase++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("shield", variant, phase, facing);
            WriteLinkFrame(
                output, source,
                variant * 32 + phase * 16, facing * 16,
                record.ByteOffset, false, damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildPushLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // The pushing walking variant adds $10 to LINK_ANIM_MODE_WALK's
        // gfx indices, producing frames $64-$67 and $90-$93. The source
        // offsets and compositions below come from specialObjectAnimationData.s.
        WriteLinkFrame(output, source, 0, (int)Facing.Up * 16, 0x0a00, false, damagePalette);       // $64, OAM $00
        WriteLinkFrame(output, source, 0, (int)Facing.Right * 16, 0x0b00, true, damagePalette);    // $65, OAM $01
        WriteSymmetricLinkCell(output, source, 0, (int)Facing.Down * 16, 0x0aa0, damagePalette);   // $66, OAM $04
        WriteLinkFrame(output, source, 0, (int)Facing.Left * 16, 0x0b00, false, damagePalette);    // $67, OAM $00
        WriteLinkFrame(output, source, 16, (int)Facing.Up * 16, 0x0a40, false, damagePalette);     // $90, OAM $00
        WriteLinkFrame(output, source, 16, (int)Facing.Right * 16, 0x0b40, true, damagePalette);  // $91, OAM $01
        WriteSymmetricLinkCell(output, source, 16, (int)Facing.Down * 16, 0x0ac0, damagePalette); // $92, OAM $04
        WriteLinkFrame(output, source, 16, (int)Facing.Left * 16, 0x0b40, false, damagePalette);  // $93, OAM $00

        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildAttackLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(48, 64, false, Image.Format.Rgba8);
        for (int facing = 0; facing < 4; facing++)
        for (int phase = 0; phase < 3; phase++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("attack", 0, phase, facing);
            WriteLinkFrame(
                output, source, phase * 16, facing * 16,
                record.ByteOffset, record.MirrorX, damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildShovelLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_DIG_2 ($1a) selects $f8-$ff. The first and second
        // columns are the $f8-$fb and $fc-$ff phases respectively.
        for (int facing = 0; facing < 4; facing++)
        for (int phase = 0; phase < 2; phase++)
        {
            LinkGraphicRecord record =
                _linkItems.Graphic("shovel", 0, phase, facing);
            WriteLinkFrame(
                output, source, phase * 16, facing * 16,
                record.ByteOffset, record.MirrorX, damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildSwordTexture(bool chargedPalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_swords.png");
        Image output = Image.CreateEmpty(8 * 32, 32, false, Image.Format.Rgba8);

        for (int animation = 0; animation < 8; animation++)
        foreach (SwordPart part in _linkItems.SwordOam(animation))
        {
            int sourceX = (part.Tile / 2) * 8;
            int destinationX = animation * 32 + part.X + 8;
            int destinationY = part.Y;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int readX = sourceX + (part.FlipX ? 7 - x : x);
                int readY = part.FlipY ? 15 - y : y;
                Color pixel = RecolorSwordPixel(source.GetPixel(readX, readY), chargedPalette);
                if (pixel.A > 0.0f)
                    output.SetPixel(destinationX + x, destinationY + y, pixel);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildDrownTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_DROWN ($0a) uses directional graphics $d4-$d7 for
        // six updates. Their OAM records $10-$12 place both 8x16 cells at
        // y=$0c. The final sixteen updates use graphics $0b with OAM $12.
        WriteLinkFrame(output, source, 0, (int)Facing.Up * 16, 0x0e00, false, damagePalette);    // $d4, OAM $10
        WriteLinkFrame(output, source, 0, (int)Facing.Right * 16, 0x0ec0, true, damagePalette); // $d5, OAM $11
        WriteSymmetricLinkCell(output, source, 0, (int)Facing.Down * 16, 0x0e80, damagePalette); // $d6, OAM $12
        WriteLinkFrame(output, source, 0, (int)Facing.Left * 16, 0x0ec0, false, damagePalette); // $d7, OAM $10

        for (int facing = 0; facing < 4; facing++)
            WriteSymmetricLinkCell(
                output, source, 16, facing * 16, 0x0f40, damagePalette); // $0b, OAM $12

        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildTopDownSwimTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        for (int frame = 0; frame < 2; frame++)
        for (int direction = 0; direction < 4; direction++)
        {
            TopDownSwimmingFrame record =
                _topDownSwimmingData.Frame(frame, direction);
            if (record.OamIndex == 0x12)
            {
                WriteSymmetricLinkCell(
                    output,
                    source,
                    frame * 16,
                    direction * 16,
                    record.SourceOffset,
                    damagePalette);
            }
            else
            {
                WriteLinkFrame(
                    output,
                    source,
                    frame * 16,
                    direction * 16,
                    record.SourceOffset,
                    mirroredOam: record.OamIndex == 0x11,
                    damagePalette);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private Texture2D BuildTopDownDiveTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 16, false, Image.Format.Rgba8);

        for (int frame = 0; frame < 2; frame++)
        {
            TopDownDivingFrame record =
                _topDownSwimmingData.DiveFrame(frame);
            WriteSymmetricLinkCell(
                output,
                source,
                frame * 16,
                0,
                record.SourceOffset,
                damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildFallInHoleTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(48, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_FALLINHOLE (mode $0d) uses frames $08, $09,
        // and $0a. In Ages' specialObjectAnimationData.s these resolve to:
        //   $08: OAM $00, spr_link+$0100, 4 tiles, duration $10
        //   $09: OAM $06, spr_link+$0140, 2 tiles, duration $0a
        //   $0a: OAM $06, spr_link+$0160, 2 tiles, duration $0a
        WriteLinkFrame(output, source, 0, 0, 0x0100, false, damagePalette);
        WriteCenteredSingleLinkCell(
            output, source, 16, 0, 0x0140, damagePalette);
        WriteCenteredSingleLinkCell(
            output, source, 32, 0, 0x0160, damagePalette);

        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildLedgeJumpTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_JUMP ($18) is animationData19f78:
        // 9 updates of $e4-$e7, 9 of $e8-$eb, 6 of $ec-$ef, then
        // terminal frame $80-$83. The entries retain their source OAM flips.
        WriteSymmetricLinkCell(
            output, source, 0, 0, 0x0c00, damagePalette);
        WriteLinkFrame(
            output, source, 0, 16, 0x0c60, true, damagePalette);
        WriteSymmetricLinkCell(
            output, source, 0, 32, 0x0c40, damagePalette, flipY: true);
        WriteLinkFrame(
            output, source, 0, 48, 0x0c60, false, damagePalette);

        WriteSymmetricLinkCell(
            output, source, 16, 0, 0x0c20, damagePalette);
        WriteLinkFrame(
            output, source, 16, 16, 0x0ca0, true, damagePalette);
        WriteSymmetricLinkCell(
            output, source, 16, 32, 0x0c00, damagePalette, flipY: true);
        WriteLinkFrame(
            output, source, 16, 48, 0x0ca0, false, damagePalette);

        WriteSymmetricLinkCell(
            output, source, 32, 0, 0x0c40, damagePalette);
        WriteLinkFrameWithFlips(
            output, source, 32, 16, 0x0c60,
            mirrorX: false, flipY: true, damagePalette: damagePalette);
        WriteSymmetricLinkCell(
            output, source, 32, 32, 0x0c20, damagePalette, flipY: true);
        WriteLinkFrameWithFlips(
            output, source, 32, 48, 0x0c60,
            mirrorX: true, flipY: true, damagePalette: damagePalette);

        WriteLinkFrame(
            output, source, 48, 0, 0x0000, true, damagePalette);
        WriteLinkFrame(
            output, source, 48, 16, 0x00c0, true, damagePalette);
        WriteLinkFrame(
            output, source, 48, 32, 0x0200, true, damagePalette);
        WriteLinkFrame(
            output, source, 48, 48, 0x00c0, false, damagePalette);

        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildDeathTexture()
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(80, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_SPIN ($01) uses graphics $02,$01,$00,$03 and
        // LINK_ANIM_MODE_COLLAPSED ($02) uses frame $04.
        WriteLinkFrame(output, source, 0, 0, 0x0000, false, false); // $00
        WriteLinkFrame(output, source, 16, 0, 0x0080, true, false); // $01
        WriteLinkFrame(output, source, 32, 0, 0x0200, false, false); // $02
        WriteLinkFrame(output, source, 48, 0, 0x0080, false, false); // $03
        WriteSymmetricLinkCell(output, source, 64, 0, 0x03e0, false); // $04

        return ImageTexture.CreateFromImage(output);
    }

    private static void WriteWalkFrame(
        Image output,
        Image source,
        Facing facing,
        int frame,
        int byteOffset,
        bool mirroredOam,
        bool damagePalette)
    {
        // spr_link.png is interleaved as 8x16 cells (32 bytes each). OAM $00
        // draws cells 0/1 normally; OAM $01 swaps them and flips both on X.
        WriteLinkFrame(
            output, source, frame * 16, (int)facing * 16,
            byteOffset, mirroredOam, damagePalette);
    }

    private static void WriteLinkFrame(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset,
        bool mirroredOam,
        bool damagePalette)
    {
        WriteLinkFrameWithFlips(
            output,
            source,
            destinationX,
            destinationY,
            byteOffset,
            mirroredOam,
            flipY: false,
            damagePalette: damagePalette);
    }

    private static void WriteLinkFrameWithFlips(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset,
        bool mirrorX,
        bool flipY,
        bool damagePalette)
    {
        int firstCell = byteOffset / 32;

        for (int destinationPart = 0; destinationPart < 2; destinationPart++)
        {
            int sourcePart = mirrorX ? 1 - destinationPart : destinationPart;
            int cell = firstCell + sourcePart;
            int cellX = (cell % 16) * 8;
            int cellY = (cell / 16) * 16;

            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int sourceX = cellX + (mirrorX ? 7 - x : x);
                int sourceY = cellY + (flipY ? 15 - y : y);
                Color sourceColor = source.GetPixel(sourceX, sourceY);
                output.SetPixel(
                    destinationX + destinationPart * 8 + x,
                    destinationY + y,
                    RecolorLinkPixel(sourceColor, damagePalette));
            }
        }
    }

    private static void WriteCenteredSingleLinkCell(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset,
        bool damagePalette)
    {
        int cell = byteOffset / 32;
        int cellX = (cell % 16) * 8;
        int cellY = (cell / 16) * 16;

        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            Color sourceColor = source.GetPixel(cellX + x, cellY + y);
            output.SetPixel(
                destinationX + 4 + x,
                destinationY + y,
                RecolorLinkPixel(sourceColor, damagePalette));
        }
    }

    private static void WriteSymmetricLinkCell(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset,
        bool damagePalette,
        bool flipY = false)
    {
        int cell = byteOffset / 32;
        int cellX = (cell % 16) * 8;
        int cellY = (cell / 16) * 16;

        for (int destinationPart = 0; destinationPart < 2; destinationPart++)
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sourceX = cellX + (destinationPart == 0 ? x : 7 - x);
            int sourceY = cellY + (flipY ? 15 - y : y);
            Color sourceColor = source.GetPixel(sourceX, sourceY);
            output.SetPixel(
                destinationX + destinationPart * 8 + x,
                destinationY + y,
                RecolorLinkPixel(sourceColor, damagePalette));
        }
    }

    internal static Color RecolorLinkPixel(
        Color source,
        bool damagePalette = false)
    {
        float value = source.R;
        if (damagePalette)
        {
            // updateLinkInvincibilityCounter replaces Link's OAM palette 0
            // with standardSpritePaletteData palette 5 while frame-counter
            // bit 2 is clear.
            return value < 0.1f ? Colors.Transparent
                : value < 0.5f ? GbcColor(0x1f, 0x16, 0x06)
                : value < 0.9f ? GbcColor(0x1b, 0x00, 0x00)
                : Colors.Black;
        }
        return value < 0.1f ? Colors.Transparent
            // specialObjectSetOamVariables gives Link OAM flags $08, selecting
            // standardSpritePaletteData palette 0. Color 0 is transparent.
            : value < 0.5f ? Colors.Black
            : value < 0.9f ? GbcColor(0x02, 0x15, 0x08)
            : GbcColor(0x1f, 0x1a, 0x11);
    }

    private static Color GbcColor(int red, int green, int blue) =>
        new(red / 31.0f, green / 31.0f, blue / 31.0f);

    private static Color RecolorSwordPixel(Color source, bool chargedPalette)
    {
        float value = source.R;
        if (chargedPalette)
        {
            return value < 0.1f ? Colors.Transparent
                : value < 0.5f ? GbcColor(0x1f, 0x16, 0x06)
                : value < 0.9f ? GbcColor(0x1b, 0x00, 0x00)
                : Colors.Black;
        }
        return value < 0.1f ? Colors.Transparent
            : value < 0.5f ? Colors.Black
            : value < 0.9f ? Color.Color8(16, 173, 66)
            : Color.Color8(255, 214, 140);
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
    Jump
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
    Throw
}

internal enum LedgeJumpState
{
    None,
    Airborne,
    AirborneBeforeScroll,
    WaitingForScroll,
    AirborneAfterScroll
}
