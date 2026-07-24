using Godot;
using System;

namespace oracleofages;

public partial class Player : Node2D
{

    private const float Speed = 60.0f;
    private static readonly Vector2 NormalSpriteOrigin = new(-8, -8);
    private const int SwordSwingFrames = 17;
    private const int SwordChargeCounter = 0x28;
    private const int SwordPokeFrames = 12;
    private const int SwordSpinFrames = 23;
    private const int ShovelActionFrames = 23;
    private const int ShovelDigFrame = 4;
    private const int ShovelSecondPoseFrame = 8;
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
    private Texture2D _carriedObjectTexture = null!;
    private Texture2D _damageCarriedObjectTexture = null!;
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
    private Texture2D _fallInHoleTexture = null!;
    private Texture2D _damageFallInHoleTexture = null!;
    private Texture2D _ledgeJumpTexture = null!;
    private Texture2D _damageLedgeJumpTexture = null!;
    private Texture2D _terrainShadowTexture = null!;
    private Vector2 _terrainShadowOffset;
    private Texture2D _deathTexture = null!;
    private TransformedLinkDatabase _transformedLink = null!;
    private Vector2 _precisePosition;
    private Vector2 _lastSafePosition;
    private HazardType _drowningHazard;
    private Facing _facing = Facing.Down;
    private float _walkTime;
    private double _swordFrameAccumulator;
    private double _shovelFrameAccumulator;
    private double _seedSatchelFrameAccumulator;
    private double _punchFrameAccumulator;
    private float _drownTime;
    private float _drownInvisibleTime;
    private float _hazardRecoveryTime;
    private float _fallInHoleTime;
    private float _fallInHoleInvisibleTime;
    private double _ledgeUpdateAccumulator;
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
    private float _enemyInvincibilityFrames;
    private float _enemyKnockbackFrames;
    private Vector2 _enemyKnockbackDirection;
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
    private SwordActionState _swordState;
    private int _swordStateFrame;
    private int _swordChargeCounter;
    private int _currentSwordDamage;
    private bool _doubleEdgedDamagePending;
    private int _heartRingDistanceFixed;
    private int _activeTransformation;
    private int _transformationFrame;
    private int _transformationTicks;
    private string? _swordButtonAction;
    private int _shovelFrame;
    private bool _usingShovel;
    private bool _usingSeedSatchel;
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

    public Vector2I FacingVector => _facing switch
    {
        Facing.Up => Vector2I.Up,
        Facing.Right => Vector2I.Right,
        Facing.Down => Vector2I.Down,
        _ => Vector2I.Left
    };
    public bool IsAttacking => _swordState != SwordActionState.None;
    public bool IsUsingShovel => _usingShovel;
    public bool IsUsingSeedSatchel => _usingSeedSatchel;
    public bool IsUsingShield => _usingShield;
    internal bool IsUsingPunch => _usingPunch;
    private bool IsUsingItem =>
        IsAttacking || IsUsingShovel || IsUsingSeedSatchel || IsUsingPunch;
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
    internal int SwordArcIndex => IsAttacking ? GetSwordArcIndex() : -1;
    internal bool SwordAllowsMovement =>
        _swordState is SwordActionState.Held or SwordActionState.Charged;
    internal bool SwordCanRestart => _swordState switch
    {
        SwordActionState.Swing => _swordStateFrame >= 3,
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
    internal bool IsGroundedForFloorButton =>
        _ledgeJumpState == LedgeJumpState.None && !_newGameSlowFalling &&
        !_drowning && !_fallingInHole;
    internal bool IsJumpingDownLedge =>
        _ledgeJumpState != LedgeJumpState.None;
    internal bool AcceptsRoomEntityContact =>
        _ledgeJumpState == LedgeJumpState.None;
    internal int LedgeZ => _ledgeZFixed >> 8;
    internal int LedgeSpeedZ => _ledgeSpeedZ;
    internal int LedgeSpeedRaw => _ledgeSpeedRaw;
    internal int LedgeCliffLength => _ledgeCliffLength;
    internal int LedgeAnimationPhase => _ledgeAnimationPhase;
    internal LedgeJumpState LedgeJumpPhase => _ledgeJumpState;
    internal bool LedgeShadowDrawn =>
        _ledgeZFixed < 0 &&
        _ledgeJumpState is
            LedgeJumpState.Airborne or
            LedgeJumpState.AirborneAfterScroll &&
        (_world.FrameCounter & 1) != 0;
    internal bool IsFloorDoorRespawning =>
        _floorDoorRespawnCounter != 0 || _floorDoorRecoveryCounter != 0;
    internal int FloorDoorRespawnCounter => _floorDoorRespawnCounter;
    internal Vector2 LocalRespawnPosition => _lastSafePosition;
    internal int NewGameSlowFallFrame => _newGameFallFrame;
    internal int NewGameSlowFallZ => _newGameFallZFixed >> 8;
    internal bool IsHoldingItemOneHand => _getItemOneHandPose;
    internal bool IsHoldingItemTwoHands => _getItemTwoHandPose;
    internal bool IsCarryingObject => _carriedObjectPose;
    internal Vector2 PrecisePosition => _precisePosition;
    internal int CarriedObjectAnimationFrame => GetWalkAnimationFrame();
    internal Vector2I? BraceletEntityOffset { get; private set; }
    internal int ShovelFrame => _shovelFrame;
    internal bool ShovelChildActive =>
        IsUsingShovel && _shovelFrame is >= ShovelDigFrame and < ShovelSecondPoseFrame;
    internal Vector2 ShovelChildOffset => ShovelOffsets[(int)_facing];
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
            int phaseBase = GetWalkAnimationFrame() == 0 ? 0x68 : 0x94;
            return phaseBase + variant * 4 + (int)_facing;
        }
    }
    internal Rect2 ShieldCollisionBounds
    {
        get
        {
            Vector2 center = OracleObjectMath.ToPixelPosition(Position) +
                ShieldCenterOffsets[(int)_facing];
            Vector2 radius = ShieldCollisionRadii[(int)_facing];
            return new Rect2(center - radius, radius * 2.0f);
        }
    }
    internal ulong ShieldAtlasPixelHash =>
        OracleGraphicsCache.PixelHash(_shieldLinkTexture.GetImage());

    public event Action? GameOverRequested;

    internal void Initialize(
        IPlayerWorld world,
        InventoryState inventory,
        Vector2 spawn,
        OracleRandom random)
    {
        _world = world;
        _inventory = inventory;
        _random = random;
        _texture = BuildLinkTexture(damagePalette: false);
        _damageTexture = BuildLinkTexture(damagePalette: true);
        _getItemOneHandTexture = BuildGetItemOneHandTexture(damagePalette: false);
        _damageGetItemOneHandTexture = BuildGetItemOneHandTexture(damagePalette: true);
        _getItemTwoHandTexture = BuildGetItemTwoHandTexture(damagePalette: false);
        _damageGetItemTwoHandTexture = BuildGetItemTwoHandTexture(damagePalette: true);
        _carriedObjectTexture = BuildCarriedObjectLinkTexture(damagePalette: false);
        _damageCarriedObjectTexture = BuildCarriedObjectLinkTexture(damagePalette: true);
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
        _fallInHoleTexture = BuildFallInHoleTexture(damagePalette: false);
        _damageFallInHoleTexture = BuildFallInHoleTexture(damagePalette: true);
        _ledgeJumpTexture = BuildLedgeJumpTexture(damagePalette: false);
        _damageLedgeJumpTexture = BuildLedgeJumpTexture(damagePalette: true);
        TerrainShadowDefinition terrainShadow = TerrainShadow.Load();
        _terrainShadowTexture = terrainShadow.Texture;
        _terrainShadowOffset = terrainShadow.Offset;
        _deathTexture = BuildDeathTexture();
        _transformedLink = new TransformedLinkDatabase();
        EndNewGameSlowFall();
        _precisePosition = spawn;
        _lastSafePosition = spawn;
        Position = OracleObjectMath.ToPixelPosition(spawn);
        // Room entities/events are already initialized before Link. Select a
        // saved active disguise here so the ordinary Link frame is never
        // exposed for one render before the first physics update.
        RefreshTransformationState();
        QueueRedraw();
    }

    public void WarpTo(
        Vector2 position,
        bool recordSafe = true,
        bool preserveSword = false,
        bool preserveShield = false,
        bool preserveLedgeJump = false)
    {
        _world?.InterruptBracelet(this, discard: true);
        if (!preserveSword)
            CancelSwordAttack();
        CancelShovelAction();
        EndNewGameSlowFall();
        _drownTime = 0.0f;
        _drownInvisibleTime = 0.0f;
        _hazardRecoveryTime = 0.0f;
        _fallInHoleTime = 0.0f;
        _fallInHoleInvisibleTime = 0.0f;
        if (!preserveLedgeJump)
            ClearLedgeHop();
        _enemyInvincibilityFrames = 0.0f;
        _enemyKnockbackFrames = 0.0f;
        _holePullCounter = 0;
        _holePullPackedPosition = -1;
        _pullingIntoHole = false;
        _drowningHazard = HazardType.None;
        _drowning = false;
        _drownRespawning = false;
        _fallingInHole = false;
        _fallInHoleRespawning = false;
        _floorDoorRespawnCounter = 0;
        _floorDoorRecoveryCounter = 0;
        _getItemOneHandPose = false;
        _getItemTwoHandPose = false;
        _carriedObjectPose = false;
        _braceletActionPose = null;
        _braceletLiftCollisionsDisabled = false;
        if (preserveShield)
            SuspendShield();
        else
            ClearShieldParent();
        _precisePosition = position;
        if (recordSafe)
            _lastSafePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        Visible = true;
        QueueRedraw();
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

    internal static int NewGameSlowFallInitialZ(int screenY) =>
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
        WarpTo(
            position,
            recordSafe: !resumeLedgeJump,
            preserveSword: true,
            preserveShield: true,
            preserveLedgeJump: resumeLedgeJump);
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
        _walkTime = 0.0f;
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
        _walkTime += (float)delta;
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
        _enemyKnockbackDirection = Position - sourcePosition;
        if (_enemyKnockbackDirection.LengthSquared() < 0.01f)
            _enemyKnockbackDirection = -(Vector2)FacingVector;
        int angle = OracleObjectMath.AngleToward(Vector2.Zero, _enemyKnockbackDirection);
        _enemyKnockbackDirection = OracleObjectMath.VectorFromAngle32(angle);
        _walking = false;
        _pushing = false;
        _world.InterruptBracelet(this, discard: false);
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
            ? ((OracleObjectMath.AngleToward(
                Vector2.Zero, -(Vector2)FacingVector)) & 0x18)
            : (OracleObjectMath.AngleToward(sourcePosition, Position) & 0x18);
        _enemyKnockbackFrames = 0x18;
        _enemyKnockbackDirection = OracleObjectMath.StrictCardinalVector(angle);
        _walking = false;
        _pushing = false;
        _world.InterruptBracelet(this, discard: false);
        ClearShieldParent();
        CancelSwordAttack();
        CancelShovelAction();
        QueueRedraw();
        return true;
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
        if (IsDying)
        {
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
            ClearShieldParent();
            float frameDelta = (float)delta * 60.0f;
            Vector2 movement = _enemyKnockbackDirection * EnemyKnockbackSpeed * frameDelta;
            TryMove(movement, allowWallSlide: false);
            _enemyKnockbackFrames = Mathf.Max(0.0f, _enemyKnockbackFrames - frameDelta);
            _walking = false;
            CancelSwordAttack();
            CancelShovelAction();
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            QueueRedraw();
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

        Vector2 movementStart = _precisePosition;
        Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (_world.MovementDisabled)
            input = Vector2.Zero;
        _lastMovementInput = input;

        bool primaryPressed = Input.IsActionPressed("attack");
        bool secondaryPressed = Input.IsActionPressed("item");
        bool itemButtonJustPressed =
            Input.IsActionJustPressed("attack") ||
            Input.IsActionJustPressed("item");
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
                if (_world.TryInteract(this))
                    return;
                if (!_world.ItemUsageDisabled &&
                    _inventory.EquippedA == InventoryState.ItemBracelet &&
                    _world.TryUseBracelet(this, primaryButton: true))
                    return;
                if (!_world.ItemUsageDisabled && RingEffects.CanPunch(
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
            else if (_inventory.EquippedA == InventoryState.ItemShovel)
                StartShovelAction(input);
            else if (_inventory.EquippedA == InventoryState.ItemSeedSatchel)
                StartSeedSatchelAction(input);
        }
        else if (_activeTransformation == 0 &&
            Input.IsActionJustPressed("item") && !_world.SwordDisabled)
        {
            if (!IsUsingItem && _world.TrySecondaryInteract(this))
            {
                return;
            }
            if (_world.ItemUsageDisabled)
            {
                // The secondary button can still lift or return shop stock.
            }
            else if (!IsUsingItem && RingEffects.CanPunch(
                _inventory,
                _inventory.EquippedA == InventoryState.ItemNone &&
                _inventory.EquippedB == InventoryState.ItemNone))
            {
                StartPunchAction(input);
            }
            else if (!IsUsingItem && _inventory.EquippedB == InventoryState.ItemBracelet)
            {
                if (_world.TryUseBracelet(this, primaryButton: false))
                    return;
            }
            else if (_inventory.EquippedB == InventoryState.ItemSword)
            {
                StartSwordAttack("item", input);
            }
            else if (_inventory.EquippedB == InventoryState.ItemShovel)
            {
                StartShovelAction(input);
            }
            else if (_inventory.EquippedB == InventoryState.ItemSeedSatchel)
            {
                StartSeedSatchelAction(input);
            }
        }

        UpdateShieldState(
            Input.IsActionPressed("attack"),
            Input.IsActionPressed("item"));

        bool movementAllowed = !IsUsingItem || SwordAllowsMovement;
        _walking = input.LengthSquared() > 0.01f && movementAllowed;
        if (_walking)
        {
            // parentItemLoadAnimationAndIncState disables Link's turning for
            // the sword's full lifetime, even after state 6 re-enables movement.
            if (!IsUsingItem)
                UpdateFacing(input);
            Vector2 movement = input * Speed * GetTerrainSpeedMultiplier() * (float)delta;
            TryMove(movement, allowWallSlide: true);
            _walkTime += (float)delta;
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
        _world.InterruptBracelet(this, discard: true);
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

    internal void BeginCutsceneControl()
    {
        _world.InterruptBracelet(this, discard: true);
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
            Face(direction);
            TryMove((Vector2)direction, allowWallSlide: false);
            _walkTime += 1.0f / 60.0f;
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
            _walkTime += 1.0f / 60.0f;
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
        _walkTime += 1.0f / 60.0f;
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
    }

    public override void _Draw()
    {
        if (LedgeShadowDrawn)
            DrawTexture(_terrainShadowTexture, _terrainShadowOffset);

        if (_deathAnimationActive)
        {
            DrawTextureRectRegion(
                _deathTexture,
                new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                new Rect2(_deathAnimationFrame * 16, 0, 16, 16));
        }
        else if (_newGameSlowFalling &&
            _newGameFallRenderer is not null && _newGameFallFrames is not null)
        {
            _newGameFallRenderer.DrawRelativeFrame(
                this,
                _newGameFallFrames[_newGameFallFrame],
                _newGameFallZFixed >> 8);
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
                ? AttackPoseOffsets[(int)_facing]
                : Vector2.Zero;
            DrawTextureRectRegion(
                DamagePaletteActive ? _damageAttackTexture : _attackTexture,
                new Rect2(NormalSpriteOrigin + offset, new Vector2(16, 16)),
                new Rect2(16, (int)_facing * 16, 16, 16));
        }
        else if (IsAttacking)
        {
            // The weapon item occupies an earlier visual layer than Link, so
            // Link's body masks the sword where their sprites overlap.
            DrawSword();
            int heldBodyFrame = GetHeldSwordBodyAnimationFrame();
            if (heldBodyFrame >= 0)
            {
                DrawTextureRectRegion(
                    DamagePaletteActive ? _damageTexture : _texture,
                    new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                    GetFrame(_facing, heldBodyFrame));
            }
            else
            {
                Facing poseFacing = GetSwordPoseFacing();
                int phase = GetSwordPosePhase();
                int texturePhase = _swordState == SwordActionState.Spin || phase == 3 ? 1 : phase;
                DrawTextureRectRegion(
                    DamagePaletteActive ? _damageAttackTexture : _attackTexture,
                    new Rect2(AttackSpriteOrigin, new Vector2(16, 16)),
                    new Rect2(texturePhase * 16, (int)poseFacing * 16, 16, 16));
            }
        }
        else if (IsUsingShovel)
        {
            int phase = _shovelFrame < ShovelSecondPoseFrame ? 0 : 1;
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

    private int GetWalkAnimationFrame() => GetWalkAnimationFrame(_walking, _walkTime);

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

        bool next = _shieldParentButton != 0 && !IsUsingItem;
        if (next && !_shieldParentInitialized)
        {
            _shieldParentInitialized = true;
            _world.PlaySound(OracleSoundEngine.SndShield);
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

    private int GetHeldSwordBodyAnimationFrame() =>
        GetHeldSwordBodyAnimationFrame(_swordState, _walking, _walkTime);

    internal static int GetHeldSwordBodyAnimationFrameForValidation(
        SwordActionState state,
        bool walking,
        float walkTime) => GetHeldSwordBodyAnimationFrame(state, walking, walkTime);

    private static int GetHeldSwordBodyAnimationFrame(
        SwordActionState state,
        bool walking,
        float walkTime)
    {
        // Sword state 6 clears the parent item's var3f priority. func_4553 then
        // resolves the priority-0 tie in Link's favor, exposing his ordinary
        // standing/walking body while the child sword retains its held arc.
        if (state is not (SwordActionState.Held or SwordActionState.Charged))
            return -1;
        return GetWalkAnimationFrame(walking, walkTime);
    }

    private static int GetWalkAnimationFrame(bool walking, float walkTime) =>
        walking && ((int)(walkTime / 0.10f) & 1) == 1 ? 1 : 0;

    private void UpdateFacing(Vector2 input)
    {
        float horizontal = Mathf.Abs(input.X);
        float vertical = Mathf.Abs(input.Y);
        if (horizontal > vertical)
            _facing = input.X > 0 ? Facing.Right : Facing.Left;
        else if (vertical > horizontal)
            _facing = input.Y > 0 ? Facing.Down : Facing.Up;
        else if (horizontal > 0.01f)
        {
            Facing horizontalFacing = input.X > 0 ? Facing.Right : Facing.Left;
            Facing verticalFacing = input.Y > 0 ? Facing.Down : Facing.Up;
            if (_facing == horizontalFacing || _facing == verticalFacing)
                return;

            // updateLinkDirectionFromAngle keeps either current diagonal
            // component. With neither component current, angles $04/$0c/$14/$1c
            // round to up/right/down/left respectively.
            _facing = input.X > 0
                ? input.Y < 0 ? Facing.Up : Facing.Right
                : input.Y > 0 ? Facing.Down : Facing.Left;
        }
    }

    public void Face(Vector2I direction)
    {
        _facing = direction == Vector2I.Up ? Facing.Up
            : direction == Vector2I.Right ? Facing.Right
            : direction == Vector2I.Down ? Facing.Down
            : Facing.Left;
        QueueRedraw();
    }

    private float GetTerrainSpeedMultiplier()
    {
        if (_world.RidingObject)
            return 1.0f;
        TerrainType terrain = _world.GetActiveTerrain(Position).Terrain.Type;
        return terrain switch
        {
            TerrainType.Grass or TerrainType.Puddle => 0.75f,
            TerrainType.Stairs or TerrainType.Vines => 0.5f,
            _ => 1.0f
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
        if (terrain.Hazard != HazardType.None)
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
        if (!_fallInHoleRespawning)
        {
            _fallInHoleTime += delta;
            if (_fallInHoleTime >= FallInHoleAnimationDuration)
            {
                _fallInHoleRespawning = true;
                _fallInHoleInvisibleTime = FallInHoleInvisibleDuration;
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
            _lastSafePosition = _precisePosition;
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
        if ((uint)arcIndex >= SwordArcs.Length)
            throw new ArgumentOutOfRangeException(nameof(arcIndex));
        SwordArc arc = SwordArcs[arcIndex];
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
        _swordChargeCounter = SwordChargeCounter;
        _swordFrameAccumulator = 0.0;
        _swordButtonAction = buttonAction;
        _walking = false;
        int sound = SwordSlashSounds[_random.Next().Value & 0x07];
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
        if (_shovelFrame == ShovelDigFrame)
        {
            _world.DigWithShovel(
                Position + ShovelChildOffset,
                FacingVector);
        }
        if (_shovelFrame >= ShovelActionFrames)
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
                if (_swordStateFrame == 6)
                {
                    _world.ApplySwordTileHit(this, (int)_facing * 2, swordPoke: false);
                    TryCreateSwordBeamFromSwing();
                }
                if (_swordStateFrame >= SwordSwingFrames)
                {
                    if (!buttonHeld)
                    {
                        CancelSwordAttack();
                        return;
                    }
                    EnterSwordHeldState();
                }
                ApplySwordCollision();
                break;

            case SwordActionState.Held:
                ApplySwordCollision();
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
                ApplySwordCollision();
                if (CheckSwordPoke(movementInput))
                    return;
                if (!buttonHeld)
                    BeginSwordSpin();
                else
                    _swordStateFrame++;
                break;

            case SwordActionState.Poke:
                _swordStateFrame++;
                if (_swordStateFrame < SwordPokeFrames)
                    break;
                if (buttonHeld)
                    EnterSwordHeldState();
                else
                    CancelSwordAttack();
                break;

            case SwordActionState.Spin:
                int previousPhase = GetSpinArcPhase();
                _swordStateFrame++;
                if (_swordStateFrame >= RingEffects.SwordSpinFrames(
                    _inventory, SwordSpinFrames))
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
        _swordChargeCounter = SwordChargeCounter;
    }

    private bool CheckSwordPoke(Vector2 movementInput)
    {
        if (!_world.IsPushingAgainstWall(_precisePosition, FacingVector, movementInput))
            return false;

        _swordState = SwordActionState.Poke;
        _swordStateFrame = 0;
        _walking = false;
        _world.ApplySwordTileHit(this, (int)_facing * 2, swordPoke: true);
        return true;
    }

    private void TriggerEnergySwordPoke()
    {
        // ENERGY_RING branches directly to @triggerSwordPoke after attempting
        // to allocate ITEM_SWORD_BEAM. It does so even when the one-beam
        // object cap prevents allocation, and does not play the charge sound.
        _swordState = SwordActionState.Poke;
        _swordStateFrame = 0;
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

    private void ApplySwordCollision()
    {
        Rect2 hitbox = GetSwordHitbox();
        if (hitbox.Size == Vector2.Zero || !_world.ApplySwordHit(this, hitbox) ||
            !_doubleEdgedDamagePending)
            return;
        // swordParent.s applies $f8 (four quarter-hearts) once after the first
        // accepted enemy contact, and clears var3a so later overlap frames do
        // not hurt Link again. The health >= $05 check occurs at swing start.
        ApplyUnmodifiedDamage(4);
        _doubleEdgedDamagePending = false;
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

    private void DrawSword()
    {
        int animation = _swordState == SwordActionState.Spin
            ? GetSwordArcIndex() - 16
            : SwordAnimationIndices[(int)_facing, GetSwordPosePhase()];
        DrawTextureRectRegion(
            SwordUsesChargedPalette ? _chargedSwordTexture : _swordTexture,
            new Rect2(SwordSpritePosition - new Vector2(16, 16), new Vector2(32, 32)),
            new Rect2(animation * 32, 0, 32, 32));
    }

    internal Vector2 AttackSpriteOrigin
    {
        get
        {
            Facing poseFacing = GetSwordPoseFacing();
            int phase = GetSwordPosePhase();
            Vector2 poseOffset = _swordState == SwordActionState.Spin || phase == 2
                ? AttackPoseOffsets[(int)poseFacing]
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
        if ((uint)arcIndex >= SwordArcs.Length)
            throw new ArgumentOutOfRangeException(nameof(arcIndex));
        SwordArc arc = SwordArcs[arcIndex];
        // itemInitializeFromLinkPosition uses the table offset for yh, then
        // gives the child sword zh = Link.zh - 2. Apply that visual height
        // separately; its collision center remains at the table's raw Y/X.
        return new Vector2(arc.OffsetX, arc.OffsetY - 2);
    }

    private int GetSwordPosePhase()
    {
        return _swordState switch
        {
            SwordActionState.Swing => _swordStateFrame < 3 ? 0
                : _swordStateFrame < 6 ? 1
                : _swordStateFrame < 14 ? 2
                : 3,
            SwordActionState.Poke => _swordStateFrame < 6 ? 2 : 3,
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
        int frame = _swordStateFrame;
        return frame < 3 ? 0
            : frame < 5 ? 1
            : frame < 8 ? 2
            : frame < 10 ? 3
            : frame < 13 ? 4
            : frame < 15 ? 5
            : frame < 18 ? 6
            : frame < 20 ? 7
            : 0;
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

    private static Texture2D[,] BuildBraceletActionTextures(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        var result = new Texture2D[3, 4];
        int[,] offsets =
        {
            { 0x0a40, 0x0b80, 0x0ac0, 0x0b80 },
            { 0x0a80, 0x0bc0, 0x0ae0, 0x0bc0 },
            { 0x1040, 0x02c0, 0x10c0, 0x02c0 }
        };
        string[,] oam =
        {
            {
                "8,0,0,0;8,8,2,0",
                "8,0,2,32;8,8,0,32",
                "8,0,0,0;8,8,0,32",
                "8,0,0,0;8,8,2,0"
            },
            {
                "10,0,0,0;10,8,0,32",
                "8,252,2,32;8,4,0,32",
                "7,0,0,0;7,8,0,32",
                "8,4,0,0;8,12,2,0"
            },
            {
                "8,0,0,0;8,8,2,0",
                "8,0,2,32;8,8,0,32",
                "8,0,0,0;8,8,2,0",
                "8,0,0,0;8,8,2,0"
            }
        };
        for (int pose = 0; pose < result.GetLength(0); pose++)
        for (int direction = 0; direction < result.GetLength(1); direction++)
        {
            result[pose, direction] = NpcCharacter.BuildOamTexture(
                source,
                oam[pose, direction],
                offsets[pose, direction] / 16,
                basePalette: damagePalette ? 5 : 0);
        }
        return result;
    }

    private static Texture2D BuildShieldLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(128, 64, false, Image.Format.Rgba8);
        int[,,] offsets =
        {
            { { 0x0400, 0x0440 }, { 0x0500, 0x0540 },
              { 0x0480, 0x04c0 }, { 0x0080, 0x00c0 } },
            { { 0x0400, 0x0440 }, { 0x0580, 0x05c0 },
              { 0x0480, 0x04c0 }, { 0x0080, 0x00c0 } },
            { { 0x0600, 0x0640 }, { 0x0780, 0x07c0 },
              { 0x0680, 0x06c0 }, { 0x0700, 0x0740 } },
            { { 0x0600, 0x0640 }, { 0x0780, 0x07c0 },
              { 0x0880, 0x08c0 }, { 0x0700, 0x0740 } }
        };

        // func_4553 selects variants $05/$06 while the shield is merely
        // equipped and $07/$08 while wUsingShield is nonzero. Added to walk
        // frames $54/$80, these are $68-$77 and $94-$a3. Every entry uses
        // special-object OAM $00, so each source pair retains its native order.
        for (int variant = 0; variant < 4; variant++)
        for (int facing = 0; facing < 4; facing++)
        for (int phase = 0; phase < 2; phase++)
        {
            WriteLinkFrame(
                output, source,
                variant * 32 + phase * 16, facing * 16,
                offsets[variant, facing, phase], false, damagePalette);
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

    private static Texture2D BuildAttackLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(48, 64, false, Image.Format.Rgba8);
        int[,] offsets =
        {
            { 0x1000, 0x1040, 0x1040 },
            { 0x1100, 0x02c0, 0x02c0 },
            { 0x1080, 0x10c0, 0x10c0 },
            { 0x1100, 0x02c0, 0x02c0 }
        };
        bool[] mirrored = { false, true, false, false };
        for (int facing = 0; facing < 4; facing++)
        for (int phase = 0; phase < 3; phase++)
        {
            WriteLinkFrame(
                output, source, phase * 16, facing * 16,
                offsets[facing, phase], mirrored[facing], damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildShovelLinkTexture(bool damagePalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_DIG_2 ($1a) selects $f8-$ff. The first and second
        // columns are the $f8-$fb and $fc-$ff phases respectively.
        int[,] offsets =
        {
            { 0x1400, 0x1440 },
            { 0x1500, 0x1540 },
            { 0x1480, 0x14c0 },
            { 0x1500, 0x1540 }
        };
        bool[] mirrored = { false, true, false, false };
        for (int facing = 0; facing < 4; facing++)
        for (int phase = 0; phase < 2; phase++)
        {
            WriteLinkFrame(
                output, source, phase * 16, facing * 16,
                offsets[facing, phase], mirrored[facing], damagePalette);
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildSwordTexture(bool chargedPalette)
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_swords.png");
        Image output = Image.CreateEmpty(8 * 32, 32, false, Image.Format.Rgba8);

        for (int animation = 0; animation < SwordOam.Length; animation++)
        foreach (SwordPart part in SwordOam[animation])
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

    private static readonly Vector2[] AttackPoseOffsets =
    {
        // Frames $b4-$b7 use OAM records $08-$0b. Their part coordinates
        // move the rendered pose three pixels through the swing while Link's
        // object position remains fixed.
        new(0, -3), new(3, 0), new(0, 3), new(-3, 0)
    };

    private static readonly Vector2[] ShovelOffsets =
    {
        // shovelParent.itemOffsets, stored as signed Y/X pairs.
        new(0, -8), new(6, 4), new(0, 7), new(-7, 4)
    };

    private static readonly Vector2[] ShieldCenterOffsets =
    {
        // checkEnemyAndPartCollisions.@shieldPositionOffsets, converted from
        // signed Y/X to Godot X/Y coordinates.
        new(1, -7), new(6, 0), new(-1, 6), new(-7, 0)
    };

    private static readonly Vector2[] ShieldCollisionRadii =
    {
        new(6, 1), new(1, 7), new(6, 1), new(1, 7)
    };

    private static readonly int[,] SwordAnimationIndices =
    {
        { 2, 1, 0, 0 },
        { 0, 1, 2, 2 },
        { 6, 5, 4, 4 },
        { 0, 7, 6, 6 }
    };

    private static readonly int[] SwordSlashSounds =
    {
        OracleSoundEngine.SndSwordSlash,
        OracleSoundEngine.SndUnknown5,
        OracleSoundEngine.SndBoomerang,
        OracleSoundEngine.SndSwordSlash,
        OracleSoundEngine.SndSwordSlash,
        OracleSoundEngine.SndUnknown5,
        OracleSoundEngine.SndSwordSlash,
        OracleSoundEngine.SndSwordSlash
    };

    private static readonly SwordArc[] SwordArcs =
    {
        new(9, 6, -2, 16), new(6, 9, -14, 0), new(9, 6, 0, -15), new(6, 9, -14, 0),
        new(7, 7, -11, 13), new(7, 7, -11, 13), new(7, 7, 17, -13), new(7, 7, -11, -13),
        new(9, 6, -17, -4), new(6, 9, 2, 19), new(9, 6, 21, 3), new(6, 9, 2, -19),
        new(9, 6, -10, -4), new(4, 9, 2, 12), new(9, 6, 16, 3), new(6, 9, 2, -12),
        new(9, 9, -17, -4), new(9, 9, -14, 16), new(9, 9, 2, 19), new(9, 9, 18, 16),
        new(9, 9, 21, 3), new(9, 9, 17, -13), new(9, 9, 2, -19), new(9, 9, -11, -13),
        // itemCode02Post selects swordArcData direction+$18 for punches.
        new(5, 5, -12, -3), new(5, 5, 0, 12),
        new(5, 5, 12, 3), new(5, 5, 0, -12)
    };

    private static readonly SwordPart[][] SwordOam =
    {
        new[] { new SwordPart(8, 4, 4) },
        new[] { new SwordPart(8, 0, 8, true), new SwordPart(8, 8, 6, true) },
        new[] { new SwordPart(8, 0, 2, true), new SwordPart(8, 8, 0, true) },
        new[] { new SwordPart(8, 0, 8, true, true), new SwordPart(8, 8, 6, true, true) },
        new[] { new SwordPart(8, 4, 4, false, true) },
        new[] { new SwordPart(8, 0, 6, false, true), new SwordPart(8, 8, 8, false, true) },
        new[] { new SwordPart(8, 0, 0), new SwordPart(8, 8, 2) },
        new[] { new SwordPart(8, 0, 6), new SwordPart(8, 8, 8) }
    };
}

internal readonly record struct SwordPart(int Y, int X, int Tile, bool FlipX = false, bool FlipY = false);

internal readonly record struct SwordArc(int RadiusY, int RadiusX, int OffsetY, int OffsetX);

internal enum SwordActionState
{
    None,
    Swing,
    Held,
    Charged,
    Poke,
    Spin
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
