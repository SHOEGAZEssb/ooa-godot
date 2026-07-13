using Godot;
using System;

namespace oracleofages;

public partial class Player : Node2D
{
    private enum Facing { Up, Right, Down, Left }

    private const float Speed = 60.0f;
    private static readonly Vector2 NormalSpriteOrigin = new(-8, -8);
    private const float AttackDuration = 17.0f / 60.0f;
    private const float SwordBreakTime = 6.0f / 60.0f;
    private const float DrownAnimationDuration = 22.0f / 60.0f;
    private const float DrownInvisibleDuration = 2.0f / 60.0f;
    private const float FallInHoleAnimationDuration = 36.0f / 60.0f;
    private const float FallInHoleInvisibleDuration = 2.0f / 60.0f;
    private const float HazardRecoveryDuration = 16.0f / 60.0f;
    private static readonly Vector2 DrownSpriteOrigin = new(-8, -4);
    private const int StartingHealthQuarters = 12;
    private const int TerrainHazardDamageQuarters = 2;
    private IPlayerWorld _world = null!;
    private Texture2D _texture = null!;
    private Texture2D _attackTexture = null!;
    private Texture2D _swordTexture = null!;
    private Texture2D _drownTexture = null!;
    private Texture2D _fallInHoleTexture = null!;
    private Vector2 _precisePosition;
    private Vector2 _lastSafePosition;
    private Vector2 _ledgeStart;
    private Vector2 _ledgeEnd;
    private OracleRoomData.HazardType _drowningHazard;
    private Facing _facing = Facing.Down;
    private float _walkTime;
    private float _attackTime;
    private float _drownTime;
    private float _drownInvisibleTime;
    private float _hazardRecoveryTime;
    private float _fallInHoleTime;
    private float _fallInHoleInvisibleTime;
    private float _ledgeHopTime;
    private Vector2 _holePullCenter;
    private int _holePullCounter;
    private int _holePullPackedPosition = -1;
    private int _healthQuarters = StartingHealthQuarters;
    private bool _attackHitApplied;
    private bool _walking;
    private bool _ledgeHopping;
    private bool _pullingIntoHole;
    private bool _drowning;
    private bool _drownRespawning;
    private bool _fallingInHole;
    private bool _fallInHoleRespawning;

    public event Action? HealthChanged;
    public event Action? RupeesChanged;

    public int HealthQuarters => _healthQuarters;
    public int Rupees { get; private set; }
    public int MaxHealthQuarters { get; private set; } = StartingHealthQuarters;
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
    public bool IsAttacking => _attackTime > 0.0f;

    public void Initialize(IPlayerWorld world, Vector2 spawn)
    {
        _world = world;
        _texture = BuildLinkTexture();
        _attackTexture = BuildAttackLinkTexture();
        _swordTexture = BuildSwordTexture();
        _drownTexture = BuildDrownTexture();
        _fallInHoleTexture = BuildFallInHoleTexture();
        _precisePosition = spawn;
        _lastSafePosition = spawn;
        Position = ToObjectPixelPosition(spawn);
        QueueRedraw();
    }

    public void WarpTo(Vector2 position, bool recordSafe = true)
    {
        _drownTime = 0.0f;
        _drownInvisibleTime = 0.0f;
        _hazardRecoveryTime = 0.0f;
        _fallInHoleTime = 0.0f;
        _fallInHoleInvisibleTime = 0.0f;
        _holePullCounter = 0;
        _holePullPackedPosition = -1;
        _pullingIntoHole = false;
        _drowningHazard = OracleRoomData.HazardType.None;
        _drowning = false;
        _drownRespawning = false;
        _fallingInHole = false;
        _fallInHoleRespawning = false;
        _precisePosition = position;
        if (recordSafe)
            _lastSafePosition = position;
        Position = ToObjectPixelPosition(position);
        Visible = true;
        QueueRedraw();
    }

    public void BeginScrollingTransition(Vector2 position, Vector2I direction)
    {
        _precisePosition = position;
        Position = ToObjectPixelPosition(position);
        Face(direction);
        _walking = false;
        _attackTime = 0.0f;
        QueueRedraw();
    }

    public void SetScrollingTransitionPosition(Vector2 logicalPosition, Vector2 screenScroll)
    {
        _precisePosition = logicalPosition;
        Position = ToObjectPixelPosition(logicalPosition - screenScroll);
        QueueRedraw();
    }

    public void FinishScrollingTransition(Vector2 position)
    {
        WarpTo(position);
        _walking = false;
        QueueRedraw();
    }

    public void BeginRoomWarpTransition()
    {
        _walking = false;
        _attackTime = 0.0f;
        _attackHitApplied = false;
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
        Position = ToObjectPixelPosition(position);
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
        if (quarters <= 0 || _healthQuarters <= 0)
            return false;

        int previous = _healthQuarters;
        _healthQuarters = Mathf.Max(0, _healthQuarters - quarters);
        if (_healthQuarters == previous)
            return false;

        HealthChanged?.Invoke();
        return true;
    }

    public bool Heal(int quarters)
    {
        if (quarters <= 0)
            return false;

        int previous = _healthQuarters;
        _healthQuarters = Mathf.Min(MaxHealthQuarters, _healthQuarters + quarters);
        if (_healthQuarters == previous)
            return false;

        HealthChanged?.Invoke();
        return true;
    }

    public void RefillHealth()
    {
        if (_healthQuarters == MaxHealthQuarters)
            return;

        _healthQuarters = MaxHealthQuarters;
        HealthChanged?.Invoke();
    }

    public void AddRupees(int amount)
    {
        int previous = Rupees;
        Rupees = Mathf.Clamp(Rupees + amount, 0, 999);
        if (Rupees != previous)
            RupeesChanged?.Invoke();
    }

    public void StartLedgeHop(Vector2 destination)
    {
        _ledgeStart = _precisePosition;
        _ledgeEnd = destination;
        _ledgeHopTime = 0.0f;
        _ledgeHopping = true;
        _walking = false;
        _attackTime = 0.0f;
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
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
            _attackTime = 0.0f;
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

        if (_world.IsTransitioning)
            return;

        if (_world.DialogueOpen)
        {
            _walking = false;
            _attackTime = 0.0f;
            QueueRedraw();
            return;
        }

        if (Input.IsActionJustPressed("attack") && _attackTime <= 0.0f)
        {
            if (_world.TryInteract(this))
                return;
            StartSwordAttack();
        }

        Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        _walking = input.LengthSquared() > 0.01f && _attackTime <= 0.0f;
        if (_walking)
        {
            UpdateFacing(input);
            Vector2 movement = input * Speed * GetTerrainSpeedMultiplier() * (float)delta;
            TryMove(movement, allowWallSlide: true);
            _walkTime += (float)delta;
        }

        Vector2 terrainPush = _world.GetTerrainPush(Position) * (float)delta;
        if (terrainPush != Vector2.Zero)
        {
            TryMove(terrainPush, allowWallSlide: false);
        }

        Position = ToObjectPixelPosition(_precisePosition);
        if (!_world.CheckTileWarp(this))
            _world.CheckRoomExit(this);
        if (!_world.IsTransitioning)
            ApplyTerrainAtFeet();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_drowning || _fallingInHole || _hazardRecoveryTime > 0.0f ||
            (_pullingIntoHole && _holePullCounter >= 16))
        {
            _attackTime = 0.0f;
            return;
        }

        if (_attackTime > 0.0f)
        {
            _attackTime = Mathf.Max(0.0f, _attackTime - (float)delta);
            float elapsed = AttackDuration - _attackTime;
            if (!_attackHitApplied && elapsed >= SwordBreakTime)
            {
                _attackHitApplied = true;
                _world.ApplySwordHit(this, GetSwordHitbox());
            }
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_drowning && !_drownRespawning)
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
        else if (IsAttacking)
        {
            int phase = GetAttackPhase();
            int texturePhase = phase == 3 ? 1 : phase;
            DrawTextureRectRegion(
                _attackTexture,
                new Rect2(AttackSpriteOrigin, new Vector2(16, 16)),
                new Rect2(texturePhase * 16, (int)_facing * 16, 16, 16));
            DrawSword(phase);
        }
        else
        {
            int frame = _walking && ((int)(_walkTime / 0.10f) & 1) == 1 ? 1 : 0;
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

        if (_attackTime <= 0.0f && _world.TryStartLedgeHop(this, _precisePosition, movement))
            return;
    }

    // The original object coordinates are 8.8 fixed point. Collision uses the
    // high bytes (xh/yh), and OAM rendering uses those same bytes; it never
    // rounds the fractional position to the nearest pixel.
    private static Vector2 ToObjectPixelPosition(Vector2 position) => new(
        Mathf.Floor(position.X),
        Mathf.Floor(position.Y));

    private void UpdateFacing(Vector2 input)
    {
        if (Mathf.Abs(input.X) > Mathf.Abs(input.Y))
            _facing = input.X > 0 ? Facing.Right : Facing.Left;
        else
            _facing = input.Y > 0 ? Facing.Down : Facing.Up;
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
        _attackTime = 0.0f;
        _attackHitApplied = false;
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

        Position = ToObjectPixelPosition(_precisePosition);

        if (Mathf.Abs(_precisePosition.X - _holePullCenter.X) < 3.0f &&
            Mathf.Abs(_precisePosition.Y - _holePullCenter.Y) < 3.0f)
        {
            StartFallInHole(_holePullCenter);
            return true;
        }

        if (_holePullCounter >= 16)
        {
            _walking = false;
            _attackTime = 0.0f;
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
        _attackTime = 0.0f;
        _attackHitApplied = false;

        // The active hazard tile is selected by the same +5px sample used by
        // objectGetRelativeTile($0500). Carry its center through explicitly so
        // rounded-vs-precise coordinates cannot recenter Link on a neighboring
        // solid tile at tile boundaries.
        _precisePosition = holeCenter;
        Position = ToObjectPixelPosition(_precisePosition);
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
        _attackTime = 0.0f;
        _attackHitApplied = false;
        Visible = true;
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
        _attackTime = 0.0f;
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
        _attackTime = 0.0f;
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
        Position = ToObjectPixelPosition(_precisePosition);
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
        if (!IsAttacking)
            return new Rect2(Position, Vector2.Zero);
        SwordArc arc = SwordArcs[GetSwordArcIndex(GetAttackPhase())];
        Vector2 center = Position + new Vector2(arc.OffsetX, arc.OffsetY);
        return new Rect2(
            center - new Vector2(arc.RadiusX, arc.RadiusY),
            new Vector2(arc.RadiusX * 2, arc.RadiusY * 2));
    }

    public void StartSwordAttack()
    {
        if (IsAttacking)
            return;
        _attackTime = AttackDuration;
        _attackHitApplied = false;
        _walking = false;
        QueueRedraw();
    }

    private void DrawSword(int phase)
    {
        int animation = SwordAnimationIndices[(int)_facing, phase];
        DrawTextureRectRegion(
            _swordTexture,
            new Rect2(SwordSpritePosition - new Vector2(16, 16), new Vector2(32, 32)),
            new Rect2(animation * 32, 0, 32, 32));
    }

    internal Vector2 AttackSpriteOrigin
    {
        get
        {
            int phase = GetAttackPhase();
            Vector2 poseOffset = phase == 2 ? AttackPoseOffsets[(int)_facing] : Vector2.Zero;
            return NormalSpriteOrigin + poseOffset;
        }
    }

    internal Vector2 SwordSpritePosition
    {
        get
        {
            SwordArc arc = SwordArcs[GetSwordArcIndex(GetAttackPhase())];
            return new Vector2(arc.OffsetX, arc.OffsetY);
        }
    }

    private int GetAttackPhase()
    {
        float elapsed = AttackDuration - _attackTime;
        return elapsed < 3.0f / 60.0f ? 0
            : elapsed < 6.0f / 60.0f ? 1
            : elapsed < 14.0f / 60.0f ? 2
            : 3;
    }

    private int GetSwordArcIndex(int phase)
    {
        return (int)_facing + phase * 4;
    }

    private static Texture2D BuildLinkTexture()
    {
        Texture2D sourceTexture = GD.Load<Texture2D>("res://assets/oracle/gfx/spr_link.png");
        Image source = sourceTexture.GetImage();
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

    private static Texture2D BuildAttackLinkTexture()
    {
        Texture2D sourceTexture = GD.Load<Texture2D>("res://assets/oracle/gfx/spr_link.png");
        Image source = sourceTexture.GetImage();
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

    private static Texture2D BuildSwordTexture()
    {
        Texture2D sourceTexture = GD.Load<Texture2D>("res://assets/oracle/gfx/spr_swords.png");
        Image source = sourceTexture.GetImage();
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
                Color pixel = RecolorSwordPixel(source.GetPixel(readX, readY));
                if (pixel.A > 0.0f)
                    output.SetPixel(destinationX + x, destinationY + y, pixel);
            }
        }
        return ImageTexture.CreateFromImage(output);
    }

    private static Texture2D BuildDrownTexture()
    {
        Texture2D sourceTexture = GD.Load<Texture2D>("res://assets/oracle/gfx/spr_link.png");
        Image source = sourceTexture.GetImage();
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
        Texture2D sourceTexture = GD.Load<Texture2D>("res://assets/oracle/gfx/spr_link.png");
        Image source = sourceTexture.GetImage();
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

    private static Color RecolorSwordPixel(Color source)
    {
        float value = source.R;
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

    private static readonly SwordArc[] SwordArcs =
    {
        new(9, 6, -2, 16), new(6, 9, -14, 0), new(9, 6, 0, -15), new(6, 9, -14, 0),
        new(7, 7, -11, 13), new(7, 7, -11, 13), new(7, 7, 17, -13), new(7, 7, -11, -13),
        new(9, 6, -17, -4), new(6, 9, 2, 19), new(9, 6, 21, 3), new(6, 9, 2, -19),
        new(9, 6, -10, -4), new(4, 9, 2, 12), new(9, 6, 16, 3), new(6, 9, 2, -12)
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
