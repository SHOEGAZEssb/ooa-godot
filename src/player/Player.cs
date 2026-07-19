using Godot;
using System;

namespace oracleofages;

public partial class Player : Node2D
{
    private enum Facing { Up, Right, Down, Left }
    internal enum SwordActionState { None, Swing, Held, Charged, Poke, Spin }

    private const float Speed = 60.0f;
    private static readonly Vector2 NormalSpriteOrigin = new(-8, -8);
    private const int SwordSwingFrames = 17;
    private const int SwordChargeCounter = 0x28;
    private const int SwordPokeFrames = 12;
    private const int SwordSpinFrames = 23;
    private const float DrownAnimationDuration = 22.0f / 60.0f;
    private const float DrownInvisibleDuration = 2.0f / 60.0f;
    private const float FallInHoleAnimationDuration = 36.0f / 60.0f;
    private const float FallInHoleInvisibleDuration = 2.0f / 60.0f;
    private const float HazardRecoveryDuration = 16.0f / 60.0f;
    private const float EnemyKnockbackSpeed = 1.25f;
    private const int EnemyInvincibilityFrames = 0x22;
    private const int EnemyKnockbackFrames = 0x0f;
    private const int NewGameSlowFallGravity = 0x0c;
    private static readonly Vector2 DrownSpriteOrigin = new(-8, -4);
    private const int TerrainHazardDamageQuarters = 2;
    private IPlayerWorld _world = null!;
    private InventoryState _inventory = null!;
    private OracleRandom _random = null!;
    private Texture2D _texture = null!;
    private Texture2D _getItemOneHandTexture = null!;
    private Texture2D _getItemTwoHandTexture = null!;
    private Texture2D _pushTexture = null!;
    private Texture2D _attackTexture = null!;
    private Texture2D _swordTexture = null!;
    private Texture2D _chargedSwordTexture = null!;
    private Texture2D _drownTexture = null!;
    private Texture2D _fallInHoleTexture = null!;
    private Vector2 _precisePosition;
    private Vector2 _lastSafePosition;
    private Vector2 _ledgeStart;
    private Vector2 _ledgeEnd;
    private OracleRoomData.HazardType _drowningHazard;
    private Facing _facing = Facing.Down;
    private float _walkTime;
    private double _swordFrameAccumulator;
    private float _drownTime;
    private float _drownInvisibleTime;
    private float _hazardRecoveryTime;
    private float _fallInHoleTime;
    private float _fallInHoleInvisibleTime;
    private float _ledgeHopTime;
    private float _enemyInvincibilityFrames;
    private float _enemyKnockbackFrames;
    private Vector2 _enemyKnockbackDirection;
    private Vector2 _holePullCenter;
    private int _holePullCounter;
    private int _holePullPackedPosition = -1;
    private SwordActionState _swordState;
    private int _swordStateFrame;
    private int _swordChargeCounter;
    private string? _swordButtonAction;
    private Vector2 _lastMovementInput;
    private bool _walking;
    private bool _pushing;
    private bool _ledgeHopping;
    private bool _pullingIntoHole;
    private bool _drowning;
    private bool _drownRespawning;
    private bool _fallingInHole;
    private bool _fallInHoleRespawning;
    private bool _cutsceneControlled;
    private bool _getItemOneHandPose;
    private bool _getItemTwoHandPose;
    private CutsceneSpriteRenderer? _newGameFallRenderer;
    private NewGameIntroDatabase.IntroSpriteFrame[]? _newGameFallFrames;
    private int _newGameFallFrame;
    private int _newGameFallFrameTicks;
    private int _newGameFallZFixed;
    private int _newGameFallSpeedZ;
    private bool _newGameSlowFalling;

    public int HealthQuarters => _inventory.HealthQuarters;
    public int Rupees => _inventory.Rupees;
    public int MaxHealthQuarters => _inventory.MaxHealthQuarters;
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
    internal bool IsPushing => _pushing;
    internal SwordActionState SwordState => _swordState;
    internal int SwordStateFrame => _swordStateFrame;
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
    internal bool IsNewGameSlowFalling => _newGameSlowFalling;
    internal int NewGameSlowFallFrame => _newGameFallFrame;
    internal int NewGameSlowFallZ => _newGameFallZFixed >> 8;
    internal bool IsHoldingItemOneHand => _getItemOneHandPose;
    internal bool IsHoldingItemTwoHands => _getItemTwoHandPose;

    internal void Initialize(
        IPlayerWorld world,
        InventoryState inventory,
        Vector2 spawn,
        OracleRandom random)
    {
        _world = world;
        _inventory = inventory;
        _random = random;
        _texture = BuildLinkTexture();
        _getItemOneHandTexture = BuildGetItemOneHandTexture();
        _getItemTwoHandTexture = BuildGetItemTwoHandTexture();
        _pushTexture = BuildPushLinkTexture();
        _attackTexture = BuildAttackLinkTexture();
        _swordTexture = BuildSwordTexture(chargedPalette: false);
        _chargedSwordTexture = BuildSwordTexture(chargedPalette: true);
        _drownTexture = BuildDrownTexture();
        _fallInHoleTexture = BuildFallInHoleTexture();
        EndNewGameSlowFall();
        _precisePosition = spawn;
        _lastSafePosition = spawn;
        Position = OracleObjectMath.ToPixelPosition(spawn);
        QueueRedraw();
    }

    public void WarpTo(
        Vector2 position,
        bool recordSafe = true,
        bool preserveSword = false)
    {
        if (!preserveSword)
            CancelSwordAttack();
        EndNewGameSlowFall();
        _drownTime = 0.0f;
        _drownInvisibleTime = 0.0f;
        _hazardRecoveryTime = 0.0f;
        _fallInHoleTime = 0.0f;
        _fallInHoleInvisibleTime = 0.0f;
        _enemyInvincibilityFrames = 0.0f;
        _enemyKnockbackFrames = 0.0f;
        _holePullCounter = 0;
        _holePullPackedPosition = -1;
        _pullingIntoHole = false;
        _drowningHazard = OracleRoomData.HazardType.None;
        _drowning = false;
        _drownRespawning = false;
        _fallingInHole = false;
        _fallInHoleRespawning = false;
        _getItemOneHandPose = false;
        _getItemTwoHandPose = false;
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
        // transition moves Link without changing his sword-locked direction.
        if (!IsAttacking)
            Face(direction);
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
        WarpTo(position, preserveSword: true);
        _walking = false;
        QueueRedraw();
    }

    public void BeginRoomWarpTransition()
    {
        _cutsceneControlled = false;
        _walking = false;
        CancelSwordAttack();
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
        OracleRoomData.HazardType hazard = activeTerrain.Terrain.Hazard;
        if (_pullingIntoHole || _drowning || _fallingInHole)
            return;

        if (hazard == OracleRoomData.HazardType.Hole)
        {
            StartPullIntoHole(activeTerrain);
            return;
        }

        StartDrowning(hazard);
    }

    public bool ApplyDamage(int quarters)
    {
        return _inventory.ApplyDamage(quarters);
    }

    public bool ApplyEnemyContactDamage(Vector2 sourcePosition, int quarters)
    {
        if (_enemyInvincibilityFrames > 0.0f || quarters <= 0)
            return false;
        if (!ApplyDamage(quarters))
            return false;

        // LINKDMG_04 selects SND_DAMAGE_LINK ($5f) when the collision is
        // accepted. Rejected contacts during Link's invincibility do not
        // enqueue another request.
        _world.PlaySound(OracleSoundEngine.SndDamageLink);
        _enemyInvincibilityFrames = EnemyInvincibilityFrames;
        _enemyKnockbackFrames = EnemyKnockbackFrames;
        _enemyKnockbackDirection = Position - sourcePosition;
        if (_enemyKnockbackDirection.LengthSquared() < 0.01f)
            _enemyKnockbackDirection = -(Vector2)FacingVector;
        int angle = OracleObjectMath.AngleToward(Vector2.Zero, _enemyKnockbackDirection);
        _enemyKnockbackDirection = OracleObjectMath.VectorFromAngle32(angle);
        _walking = false;
        _pushing = false;
        CancelSwordAttack();
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

    public void StartLedgeHop(Vector2 destination)
    {
        _ledgeStart = _precisePosition;
        _ledgeEnd = destination;
        _ledgeHopTime = 0.0f;
        _ledgeHopping = true;
        _walking = false;
        CancelSwordAttack();
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        _pushing = false;
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
            QueueRedraw();
            return;
        }

        if (_pullingIntoHole && UpdatePullIntoHole())
            return;

        if (_ledgeHopping)
        {
            UpdateLedgeHop((float)delta);
            return;
        }

        if (_enemyKnockbackFrames > 0.0f)
        {
            float frameDelta = (float)delta * 60.0f;
            Vector2 movement = _enemyKnockbackDirection * EnemyKnockbackSpeed * frameDelta;
            TryMove(movement, allowWallSlide: false);
            _enemyKnockbackFrames = Mathf.Max(0.0f, _enemyKnockbackFrames - frameDelta);
            _walking = false;
            CancelSwordAttack();
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            QueueRedraw();
            return;
        }

        if (_world.IsTransitioning)
            return;

        if (_world.DialogueOpen)
        {
            _walking = false;
            CancelSwordAttack();
            QueueRedraw();
            return;
        }

        Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (_world.MovementDisabled)
            input = Vector2.Zero;
        _lastMovementInput = input;

        if (Input.IsActionJustPressed("attack") && !_world.SwordDisabled)
        {
            if (!IsAttacking)
            {
                if (_inventory.EquippedA == InventoryState.ItemBracelet && _world.TryUseBracelet(this))
                    return;
                if (_world.TryInteract(this))
                    return;
            }
            if (_inventory.EquippedA == InventoryState.ItemSword)
                StartSwordAttack("attack", input);
        }
        else if (Input.IsActionJustPressed("item") && !_world.SwordDisabled)
        {
            if (!IsAttacking && _inventory.EquippedB == InventoryState.ItemBracelet)
            {
                _world.TryUseBracelet(this);
            }
            else if (_inventory.EquippedB == InventoryState.ItemSword)
            {
                StartSwordAttack("item", input);
            }
        }

        bool movementAllowed = !IsAttacking || SwordAllowsMovement;
        _walking = input.LengthSquared() > 0.01f && movementAllowed;
        if (_walking)
        {
            // parentItemLoadAnimationAndIncState disables Link's turning for
            // the sword's full lifetime, even after state 6 re-enables movement.
            if (!IsAttacking)
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

        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (!_world.CheckTileWarp(this))
            _world.CheckRoomExit(this);
        if (!_world.IsTransitioning)
            ApplyTerrainAtFeet();
        QueueRedraw();
    }

    internal void BeginCutsceneControl()
    {
        _cutsceneControlled = true;
        _walking = false;
        _pushing = false;
        CancelSwordAttack();
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
        QueueRedraw();
    }

    internal void EndGetItemTwoHandPose()
    {
        if (!_getItemTwoHandPose)
            return;
        _getItemTwoHandPose = false;
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

    internal void SetScriptedPosition(Vector2 position)
    {
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        QueueRedraw();
    }

    internal void SetScriptedCoordinateHigh(bool horizontal, int coordinate)
    {
        // preventObjectHFromPassingObjectD overwrites only Object.xh/yh. Keep
        // the 8.8 fractional byte accumulated by linkCutscene6 intact.
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

        // updateItems skips initialized items while wScrollMode is $08. Room
        // warps cancel the sword synchronously in BeginRoomWarpTransition.
        if (_world.IsTransitioning)
            return;

        if (_world.SwordDisabled)
            CancelSwordAttack();

        if (_drowning || _fallingInHole || _hazardRecoveryTime > 0.0f ||
            (_pullingIntoHole && _holePullCounter >= 16))
        {
            CancelSwordAttack();
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
    }

    public override void _Draw()
    {
        if (_newGameSlowFalling &&
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
                _drownTexture,
                new Rect2(DrownSpriteOrigin, new Vector2(16, 16)),
                new Rect2(frame * 16, (int)_facing * 16, 16, 16));
        }
        else if (_fallingInHole && !_fallInHoleRespawning)
        {
            int frame = GetFallInHoleFrame();
            DrawTextureRectRegion(
                _fallInHoleTexture,
                new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                new Rect2(frame * 16, 0, 16, 16));
        }
        else if (_getItemTwoHandPose)
        {
            DrawTexture(_getItemTwoHandTexture, NormalSpriteOrigin);
        }
        else if (_getItemOneHandPose)
        {
            DrawTexture(_getItemOneHandTexture, NormalSpriteOrigin);
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
                    _texture,
                    new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                    GetFrame(_facing, heldBodyFrame));
            }
            else
            {
                Facing poseFacing = GetSwordPoseFacing();
                int phase = GetSwordPosePhase();
                int texturePhase = _swordState == SwordActionState.Spin || phase == 3 ? 1 : phase;
                DrawTextureRectRegion(
                    _attackTexture,
                    new Rect2(AttackSpriteOrigin, new Vector2(16, 16)),
                    new Rect2(texturePhase * 16, (int)poseFacing * 16, 16, 16));
            }
        }
        else if (_pushing)
        {
            int frame = GetWalkAnimationFrame();
            DrawTextureRectRegion(
                _pushTexture,
                new Rect2(NormalSpriteOrigin, new Vector2(16, 16)),
                new Rect2(frame * 16, (int)_facing * 16, 16, 16));
        }
        else
        {
            int frame = GetWalkAnimationFrame();
            Rect2 source = GetFrame(_facing, frame);
            DrawTextureRectRegion(
                _texture,
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

        if (!IsAttacking && _world.TryStartLedgeHop(this, _precisePosition, movement))
            return;
    }

    internal void UpdatePushingState(Vector2 movementInput)
    {
        _pushing = movementInput.LengthSquared() > 0.01f && !IsAttacking &&
            _world.IsPushingAgainstWall(_precisePosition, FacingVector, movementInput);
    }

    private int GetWalkAnimationFrame() => GetWalkAnimationFrame(_walking, _walkTime);

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
        OracleRoomData.TerrainType terrain = _world.GetActiveTerrain(Position).Terrain.Type;
        return terrain switch
        {
            OracleRoomData.TerrainType.Grass or OracleRoomData.TerrainType.Puddle => 0.75f,
            OracleRoomData.TerrainType.Stairs or OracleRoomData.TerrainType.Vines => 0.5f,
            _ => 1.0f
        };
    }

    private void ApplyTerrainAtFeet()
    {
        ActiveTerrainInfo activeTerrain = _world.GetActiveTerrain(Position);
        OracleRoomData.TerrainInfo terrain = activeTerrain.Terrain;
        if (terrain.Hazard != OracleRoomData.HazardType.None)
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

    private void StartPullIntoHole(ActiveTerrainInfo activeTerrain)
    {
        _pullingIntoHole = true;
        _holePullCenter = activeTerrain.TileCenter;
        _holePullPackedPosition = activeTerrain.PackedPosition;
        _holePullCounter = 0;
        _walking = false;
        CancelSwordAttack();
        QueueRedraw();
    }

    private bool UpdatePullIntoHole()
    {
        ActiveTerrainInfo activeTerrain = _world.GetActiveTerrain(Position);
        if (activeTerrain.Terrain.Hazard == OracleRoomData.HazardType.Hole)
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

    private static int GetTerrainHazardDamageQuarters(OracleRoomData.HazardType hazard)
    {
        // The original LINK_STATE_RESPAWNING path applies damageToApply=$fc
        // after Link reappears; linkApplyDamage consumes that as two
        // quarter-hearts, ie. a half-heart.
        return hazard == OracleRoomData.HazardType.None ? 0 : TerrainHazardDamageQuarters;
    }

    private void StartDrowning(OracleRoomData.HazardType hazard)
    {
        _drowningHazard = hazard;
        _drowning = true;
        _drownRespawning = false;
        _drownTime = 0.0f;
        _drownInvisibleTime = 0.0f;
        _walking = false;
        CancelSwordAttack();
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

        ApplyDamage(GetTerrainHazardDamageQuarters(_drowningHazard));
        WarpTo(_lastSafePosition);
        _hazardRecoveryTime = HazardRecoveryDuration;
        _walking = false;
        CancelSwordAttack();
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

        ApplyDamage(GetTerrainHazardDamageQuarters(OracleRoomData.HazardType.Hole));
        WarpTo(_lastSafePosition);
        _hazardRecoveryTime = HazardRecoveryDuration;
        _walking = false;
        CancelSwordAttack();
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

    private void UpdateLedgeHop(float delta)
    {
        _ledgeHopTime += delta;
        float t = Mathf.Min(_ledgeHopTime / 0.32f, 1.0f);
        float eased = t * t * (3.0f - 2.0f * t);
        _precisePosition = _ledgeStart.Lerp(_ledgeEnd, eased);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (t >= 1.0f)
        {
            _ledgeHopping = false;
            ApplyTerrainAtFeet();
        }
        QueueRedraw();
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
        if (changed)
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
                    _world.ApplySwordTileHit(this, (int)_facing * 2, swordPoke: false);
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
                _swordChargeCounter--;
                if (_swordChargeCounter < 0)
                {
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
                if (_swordStateFrame >= SwordSpinFrames)
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
        if (hitbox.Size != Vector2.Zero)
            _world.ApplySwordHit(this, hitbox);
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

    private static Texture2D BuildLinkTexture()
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_WALK uses base gfx indices $54 and $80, then adds
        // direction (UP, RIGHT, DOWN, LEFT). These resolve to the offsets and
        // OAM compositions below in specialObjectAnimationData.s. Up/down
        // alternate a mirrored composition of the same source tiles; they are
        // not neighboring 16x16 crops.
        WriteWalkFrame(output, source, Facing.Up, 0, 0x0000, false); // gfx $54, OAM $00
        WriteWalkFrame(output, source, Facing.Up, 1, 0x0000, true);  // gfx $80, OAM $01
        WriteWalkFrame(output, source, Facing.Right, 0, 0x0080, true); // gfx $55
        WriteWalkFrame(output, source, Facing.Right, 1, 0x00c0, true); // gfx $81
        WriteWalkFrame(output, source, Facing.Down, 0, 0x0200, false); // gfx $56
        WriteWalkFrame(output, source, Facing.Down, 1, 0x0200, true);  // gfx $82
        WriteWalkFrame(output, source, Facing.Left, 0, 0x0080, false); // gfx $57
        WriteWalkFrame(output, source, Facing.Left, 1, 0x00c0, false); // gfx $83

        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildGetItemOneHandTexture()
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_GETITEM1HAND ($0e) is the static graphics frame $05:
        // OAM $00, spr_link+$0da0, four tiles. The frame is below $54, so
        // loadLinkAndCompanionAnimationFrame_body does not add Link's direction.
        WriteLinkFrame(output, source, 0, 0, 0x0da0, false);
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildGetItemTwoHandTexture()
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_GETITEM2HAND ($0f) is static graphics frame $06:
        // OAM $04 mirrors the single spr_link+$0de0 cell into a 16-pixel body.
        WriteSymmetricLinkCell(output, source, 0, 0, 0x0de0);
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildPushLinkTexture()
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // The pushing walking variant adds $10 to LINK_ANIM_MODE_WALK's
        // gfx indices, producing frames $64-$67 and $90-$93. The source
        // offsets and compositions below come from specialObjectAnimationData.s.
        WriteLinkFrame(output, source, 0, (int)Facing.Up * 16, 0x0a00, false);       // $64, OAM $00
        WriteLinkFrame(output, source, 0, (int)Facing.Right * 16, 0x0b00, true);    // $65, OAM $01
        WriteSymmetricLinkCell(output, source, 0, (int)Facing.Down * 16, 0x0aa0);   // $66, OAM $04
        WriteLinkFrame(output, source, 0, (int)Facing.Left * 16, 0x0b00, false);    // $67, OAM $00
        WriteLinkFrame(output, source, 16, (int)Facing.Up * 16, 0x0a40, false);     // $90, OAM $00
        WriteLinkFrame(output, source, 16, (int)Facing.Right * 16, 0x0b40, true);  // $91, OAM $01
        WriteSymmetricLinkCell(output, source, 16, (int)Facing.Down * 16, 0x0ac0); // $92, OAM $04
        WriteLinkFrame(output, source, 16, (int)Facing.Left * 16, 0x0b40, false);  // $93, OAM $00

        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildAttackLinkTexture()
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
                offsets[facing, phase], mirrored[facing]);
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

    private static Texture2D BuildDrownTexture()
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(32, 64, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_DROWN ($0a) uses directional graphics $d4-$d7 for
        // six updates. Their OAM records $10-$12 place both 8x16 cells at
        // y=$0c. The final sixteen updates use graphics $0b with OAM $12.
        WriteLinkFrame(output, source, 0, (int)Facing.Up * 16, 0x0e00, false);    // $d4, OAM $10
        WriteLinkFrame(output, source, 0, (int)Facing.Right * 16, 0x0ec0, true); // $d5, OAM $11
        WriteSymmetricLinkCell(output, source, 0, (int)Facing.Down * 16, 0x0e80); // $d6, OAM $12
        WriteLinkFrame(output, source, 0, (int)Facing.Left * 16, 0x0ec0, false); // $d7, OAM $10

        for (int facing = 0; facing < 4; facing++)
            WriteSymmetricLinkCell(output, source, 16, facing * 16, 0x0f40); // $0b, OAM $12

        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildFallInHoleTexture()
    {
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_link.png");
        Image output = Image.CreateEmpty(48, 16, false, Image.Format.Rgba8);

        // LINK_ANIM_MODE_FALLINHOLE (mode $0d) uses frames $08, $09,
        // and $0a. In Ages' specialObjectAnimationData.s these resolve to:
        //   $08: OAM $00, spr_link+$0100, 4 tiles, duration $10
        //   $09: OAM $06, spr_link+$0140, 2 tiles, duration $0a
        //   $0a: OAM $06, spr_link+$0160, 2 tiles, duration $0a
        WriteLinkFrame(output, source, 0, 0, 0x0100, false);
        WriteCenteredSingleLinkCell(output, source, 16, 0, 0x0140);
        WriteCenteredSingleLinkCell(output, source, 32, 0, 0x0160);

        return ImageTexture.CreateFromImage(output);
    }

    private static void WriteWalkFrame(
        Image output,
        Image source,
        Facing facing,
        int frame,
        int byteOffset,
        bool mirroredOam)
    {
        // spr_link.png is interleaved as 8x16 cells (32 bytes each). OAM $00
        // draws cells 0/1 normally; OAM $01 swaps them and flips both on X.
        WriteLinkFrame(output, source, frame * 16, (int)facing * 16, byteOffset, mirroredOam);
    }

    private static void WriteLinkFrame(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset,
        bool mirroredOam)
    {
        int firstCell = byteOffset / 32;

        for (int destinationPart = 0; destinationPart < 2; destinationPart++)
        {
            int sourcePart = mirroredOam ? 1 - destinationPart : destinationPart;
            int cell = firstCell + sourcePart;
            int cellX = (cell % 16) * 8;
            int cellY = (cell / 16) * 16;

            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int sourceX = cellX + (mirroredOam ? 7 - x : x);
                Color sourceColor = source.GetPixel(sourceX, cellY + y);
                output.SetPixel(
                    destinationX + destinationPart * 8 + x,
                    destinationY + y,
                    RecolorLinkPixel(sourceColor));
            }
        }
    }

    private static void WriteCenteredSingleLinkCell(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset)
    {
        int cell = byteOffset / 32;
        int cellX = (cell % 16) * 8;
        int cellY = (cell / 16) * 16;

        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            Color sourceColor = source.GetPixel(cellX + x, cellY + y);
            output.SetPixel(destinationX + 4 + x, destinationY + y, RecolorLinkPixel(sourceColor));
        }
    }

    private static void WriteSymmetricLinkCell(
        Image output,
        Image source,
        int destinationX,
        int destinationY,
        int byteOffset)
    {
        int cell = byteOffset / 32;
        int cellX = (cell % 16) * 8;
        int cellY = (cell / 16) * 16;

        for (int destinationPart = 0; destinationPart < 2; destinationPart++)
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 8; x++)
        {
            int sourceX = cellX + (destinationPart == 0 ? x : 7 - x);
            Color sourceColor = source.GetPixel(sourceX, cellY + y);
            output.SetPixel(
                destinationX + destinationPart * 8 + x,
                destinationY + y,
                RecolorLinkPixel(sourceColor));
        }
    }

    internal static Color RecolorLinkPixel(Color source)
    {
        float value = source.R;
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

    private readonly record struct SwordArc(int RadiusY, int RadiusX, int OffsetY, int OffsetX);
    private readonly record struct SwordPart(int Y, int X, int Tile, bool FlipX = false, bool FlipY = false);

    private static readonly Vector2[] AttackPoseOffsets =
    {
        // Frames $b4-$b7 use OAM records $08-$0b. Their part coordinates
        // move the rendered pose three pixels through the swing while Link's
        // object position remains fixed.
        new(0, -3), new(3, 0), new(0, 3), new(-3, 0)
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
        new(9, 9, 21, 3), new(9, 9, 17, -13), new(9, 9, 2, -19), new(9, 9, -11, -13)
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
