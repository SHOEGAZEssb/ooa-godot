using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// SPECIALOBJECT_RICKY $0b owner used by the room $0:$6a handoff. It preserves
/// source mount, ridden movement/hopping, punch and tornado charging,
/// dismount/remount, and scrolling ownership.
/// </summary>
internal sealed partial class RickyCompanionRoomEntity : TransitionOffsetNode2D,
    IRoomEntity,
    IFixedRoomEntity,
    IPlayerRestriction,
    IPlayerForcedMovement,
    IPlayerRideableRoomEntity,
    IPlayerScreenTransitionRoomEntity,
    IRoomEntityLifetime,
    ICompanionBarrierTarget
{
    private static readonly Vector2[] CollisionSamples =
    [
        new(-3, -5), new(4, -5), new(-3, 8), new(4, 8),
        new(-5, -3), new(-5, 6), new(6, -3), new(6, 6)
    ];

    private readonly RickyGlovesEventRecord _record;
    private readonly RickyCompanionVisualRecord _visual;
    private readonly RickyCompanionBehaviorRecord _behavior;
    private readonly OracleSaveData? _saveData;
    private readonly OracleRuntimeState _runtime;
    private readonly OracleRandom _random;
    private readonly LedgeJumpDatabase _ledgeJumps;
    private readonly Action<int> _playSound;
    private readonly Action<int, string, Vector2> _dialogueRequested;
    private readonly Func<bool> _dialogueOpen;
    private readonly EnemyAnimationPlayer _animation;
    private readonly Texture2D[] _linkTextures;
    private readonly Texture2D[] _chargeLinkTextures;
    private readonly Texture2D[] _damageLinkTextures;
    private readonly Vector2[] _linkTextureOffsets;
    private OracleRoomData _room;
    private OracleRoomData? _transitionDestination;
    private Vector2 _precisePosition;
    private Vector2 _dismountPreviousLinkPosition;
    private int _group;
    private int _roomId;
    private int _direction;
    private int _angle = 0xff;
    private int _zFixed;
    private int _speedZ;
    private int _hopCounter;
    private int _landingCounter;
    private int _airborneDelay;
    private int _airborneSpeed;
    private int _wallCrossingMask;
    private int _chargeCounter;
    private bool _mountStarted;
    private bool _attackPressed;
    private bool _attackJustPressed;
    private bool _itemJustPressed;
    private bool _attackEdgeObserved;
    private bool _itemEdgeObserved;
    private bool _chargePaletteActive;
    private bool _dismountInitialized;
    private bool _dismountLandingObserved;
    private bool _screenTransitionsDisabled;
    private bool _hazardAnimationStarted;
    private bool _finished;
    private int _tingleDepartureStage;
    private int _tingleDepartureCounter;
    private string _tingleDepartureMessage = string.Empty;
    private HazardType _hazard;
    private Vector2 _hazardCenter;
    private RickyCompanionPhase _phase;

    public Node2D Node => this;
    public bool DisablesSword => LinkRiding ||
        _phase is RickyCompanionPhase.Mounting or
            RickyCompanionPhase.TingleDeparture;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => _phase == RickyCompanionPhase.TingleDeparture;
    public bool DisablesScreenTransitions => _screenTransitionsDisabled ||
        _phase is RickyCompanionPhase.Mounting or
            RickyCompanionPhase.Dismounting;
    public bool Finished => _finished;
    public bool LinkRiding => _phase is
        RickyCompanionPhase.Riding or
        RickyCompanionPhase.Hopping or
        RickyCompanionPhase.Landing or
        RickyCompanionPhase.JumpingUpCliff or
        RickyCompanionPhase.JumpingOverHole or
        RickyCompanionPhase.JumpingDownCliff or
        RickyCompanionPhase.HazardFalling or
        RickyCompanionPhase.Punching or
        RickyCompanionPhase.Charging ||
        (_phase == RickyCompanionPhase.Dismounting &&
            !_dismountInitialized);
    public bool ControlsPlayerScreenTransition => LinkRiding;
    public bool BypassesScreenTransitionInputGate => false;
    public Vector2 ScreenTransitionPosition => _precisePosition;
    int ICompanionBarrierTarget.CompanionId => CompanionRuntimeState.RickyId;
    bool ICompanionBarrierTarget.BarrierMounted => LinkRiding;
    Vector2 ICompanionBarrierTarget.BarrierPosition => _precisePosition;

    internal RickyCompanionPhase Phase => _phase;
    internal int Direction => _direction;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int AnimationFrameIndex => _animation.FrameIndex;
    internal Vector2 PrecisePosition => _precisePosition;
    // func_410d uses bc=$0000 for Ricky. Unlike Moosh, Link's riding object
    // position is exactly Ricky's xyz position; the riding OAM carries the
    // visual offset itself.
    internal Vector2 RidingLinkPosition => _precisePosition;
    internal int ZFixed => _zFixed;
    internal int HopCounter => _hopCounter;
    internal int AirborneDelay => _airborneDelay;
    internal int ChargeCounter => _chargeCounter;
    internal bool ChargePaletteActive => _chargePaletteActive;
    internal int LinkAnimationParameter => _animation.CurrentParameter & 0x3f;
    internal ulong RickyTexturePixelHash => OracleGraphicsCache.PixelHash(
        CurrentRickyTexture.GetImage());
    internal ulong NormalRickyTexturePixelHash => OracleGraphicsCache.PixelHash(
        _animation.CurrentTexture.GetImage());
    internal ulong LinkTexturePixelHash => OracleGraphicsCache.PixelHash(
        CurrentLinkTexture(LinkAnimationParameter).GetImage());
    internal ulong NormalLinkTexturePixelHash => OracleGraphicsCache.PixelHash(
        _linkTextures[LinkAnimationParameter].GetImage());
    internal bool ChargeLinkTextureSelected =>
        ReferenceEquals(
            CurrentLinkTexture(LinkAnimationParameter),
            _chargeLinkTextures[LinkAnimationParameter]);

    internal RickyCompanionRoomEntity(
        RickyCompanionSpawn spawn,
        OracleRoomData room,
        RickyGlovesEventDatabase database,
        OracleSaveData? saveData,
        OracleRuntimeState runtime,
        OracleRandom random,
        LedgeJumpDatabase ledgeJumps,
        Action<int> playSound,
        Action<int, string, Vector2> dialogueRequested,
        Func<bool> dialogueOpen)
    {
        _record = database.Record;
        _visual = database.Visual;
        _behavior = database.Behavior;
        _saveData = saveData;
        _runtime = runtime;
        _random = random;
        _ledgeJumps = ledgeJumps;
        _playSound = playSound;
        _dialogueRequested = dialogueRequested;
        _dialogueOpen = dialogueOpen;
        _room = room;
        _group = spawn.Group;
        _roomId = spawn.Room;
        _precisePosition = spawn.Position;
        _direction = spawn.Direction;
        _phase = spawn.ForceMount
            ? RickyCompanionPhase.Mounting
            : spawn.Riding
                ? RickyCompanionPhase.Riding
                : RickyCompanionPhase.Waiting;

        _animation = new EnemyAnimationPlayer(this, _visual.Animations.Length);
        _animation.Load(
            OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{_visual.Sprite}.png"),
            _visual.Animations,
            _visual.TileBase,
            _visual.Palette,
            positionedOam: true,
            paletteVariants: [2],
            animationSourceOffsets: _visual.AnimationSourceOffsets);
        (_linkTextures, _chargeLinkTextures, _damageLinkTextures,
            _linkTextureOffsets) =
            LoadLinkFrames(_visual);

        int initialAnimation = spawn.ForceMount
            ? _record.InitialAnimation
            : spawn.Riding
                ? _behavior.IdleAnimation + _direction
                : 0x17;
        _hopCounter = _behavior.HopDelay;
        SetAnimation(initialAnimation);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        Name = $"Ricky_{_group:x1}_{_roomId:x2}";
        ZIndex = LinkRiding
            ? NpcCharacter.BehindLinkZIndex
            : Player.NormalZIndex;
        Visible = true;
    }

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (_phase == RickyCompanionPhase.Mounting && _mountStarted)
            player.NudgeCompanionMountToward(_precisePosition);
        else if (LinkRiding)
            SynchronizePlayer(player, Vector2.Zero);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        _attackPressed = Input.IsActionPressed("attack");
        bool attackEdge = Input.IsActionJustPressed("attack");
        bool itemEdge = Input.IsActionJustPressed("item");
        if (Input.OriginalUpdateActive)
        {
            _attackJustPressed = attackEdge;
            _itemJustPressed = itemEdge;
            _attackEdgeObserved = false;
            _itemEdgeObserved = false;
        }
        else
        {
            _attackJustPressed = attackEdge && !_attackEdgeObserved;
            _itemJustPressed = itemEdge && !_itemEdgeObserved;
            _attackEdgeObserved = attackEdge;
            _itemEdgeObserved = itemEdge;
        }
        _chargePaletteActive = false;

        switch (_phase)
        {
            case RickyCompanionPhase.Waiting:
                UpdateWaiting(frame.Player);
                break;
            case RickyCompanionPhase.Mounting:
                UpdateMounting(frame.Player);
                break;
            case RickyCompanionPhase.Riding:
                UpdateRiding(frame.Player, spawns);
                break;
            case RickyCompanionPhase.Hopping:
                UpdateHopping(spawns);
                break;
            case RickyCompanionPhase.Landing:
                UpdateLanding(frame.Player, spawns);
                break;
            case RickyCompanionPhase.JumpingUpCliff:
                UpdateJumpingUpCliff(spawns);
                break;
            case RickyCompanionPhase.JumpingOverHole:
                UpdateJumpingOverHole(spawns);
                break;
            case RickyCompanionPhase.JumpingDownCliff:
                UpdateJumpingDownCliff(spawns);
                break;
            case RickyCompanionPhase.HazardFalling:
                UpdateHazardFalling(frame.Player);
                break;
            case RickyCompanionPhase.Punching:
                UpdatePunching(spawns);
                break;
            case RickyCompanionPhase.Charging:
                UpdateCharging(frame.Counter, spawns);
                break;
            case RickyCompanionPhase.Dismounting:
                UpdateDismounting(frame.Player);
                break;
            case RickyCompanionPhase.AwaitingDistance:
                UpdateAwaitingDistance(frame.Player);
                break;
            case RickyCompanionPhase.TingleDeparture:
                UpdateTingleDeparture(frame.Player);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Ricky phase {_phase}.");
        }

        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (LinkRiding)
        {
            CompanionRuntimeState.Update(
                _runtime,
                CompanionRuntimeState.RickyId,
                _roomId,
                _precisePosition,
                _direction);
            SynchronizePlayer(frame.Player, Vector2.Zero);
        }
        UpdateDrawPriority(frame.Player);
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        DrawTexture(
            CurrentRickyTexture,
            _animation.CurrentOffset +
            new Vector2(0, _zFixed >> 8) +
            SourceOamDrawOffset);
    }

    private void UpdateWaiting(Player player)
    {
        _animation.Advance();
        if (!player.TopDownAirborne && !player.IsDying &&
            !player.IsDrowning && !player.IsFallingInHole &&
            LinkWithinMountDistance(player.PrecisePosition))
        {
            _phase = RickyCompanionPhase.Mounting;
            _mountStarted = false;
            return;
        }

        // State $01 reads the high animParameter bits after
        // specialObjectAnimate. Parameter $80 starts the small $17 idle hop
        // at -$0100; parameter $40 integrates it with $40 gravity. Advancing
        // the OAM without this Z motion leaves Ricky's airborne frames
        // splayed across the ground.
        int signal = _animation.CurrentParameter & 0xc0;
        if (signal == 0x80)
        {
            _speedZ = _record.JumpSpeedZ;
        }
        else if (signal == 0x40)
        {
            OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, _record.JumpGravity);
        }
    }

    private void UpdateMounting(Player player)
    {
        // rickyState3 continues objectUpdateSpeedZ_paramC while Link performs
        // the mounting jump, including when mounting began during Ricky's
        // parameter-driven idle hop.
        OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _record.JumpGravity);
        if (!_mountStarted)
        {
            player.BeginCompanionMount(player.PrecisePosition);
            _mountStarted = true;
            return;
        }
        player.NudgeCompanionMountToward(_precisePosition);
        if (!player.CompanionJumpReadyToRide)
            return;

        _phase = RickyCompanionPhase.Riding;
        _angle = 0xff;
        _zFixed = 0;
        _speedZ = 0;
        _hopCounter = _behavior.HopDelay;
        _screenTransitionsDisabled = false;
        ZIndex = NpcCharacter.BehindLinkZIndex;
        SetAnimation(_behavior.IdleAnimation + _direction);
        CompanionRuntimeState.Begin(
            _runtime,
            CompanionRuntimeState.RickyId,
            _roomId,
            _precisePosition,
            _direction);
        _playSound(_record.RickySound);
        SynchronizePlayer(player, Vector2.Zero, finishMount: true);
    }

    private void UpdateRiding(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_attackJustPressed)
        {
            StartPunch(spawns);
            return;
        }
        if (_itemJustPressed)
        {
            _phase = RickyCompanionPhase.Dismounting;
            _dismountInitialized = false;
            _dismountLandingObserved = false;
            return;
        }

        Vector2 input = Input.GetVector(
            "move_left", "move_right", "move_up", "move_down");
        int angle = AngleForInput(input);
        if (angle == 0xff)
        {
            _angle = angle;
            _hopCounter = _behavior.HopDelay;
            // The stationary branch calls companionSetAnimation every
            // update, restoring the direction's first ridden pose instead of
            // retaining whichever walking frame happened to be current.
            SetAnimation(_behavior.IdleAnimation + _direction);
            TryBeginHazard();
            return;
        }

        _angle = angle;
        if (_hopCounter == 0)
        {
            StartPeriodicJump();
            return;
        }
        _hopCounter--;
        SetDirectionAnimation(_behavior.IdleAnimation, animate: true);
        int walls = CalculateAdjacentWallsBitset();
        if (TryStartHoleJump() ||
            TryStartJumpDownCliff(walls, periodic: false) ||
            TryStartJumpUpCliff(walls))
        {
            return;
        }

        ApplyCompanionMovement(_behavior.GroundSpeed);
        spawns.Add(new RickyTileBreakSpawn(
            _precisePosition + new Vector2(0, 5),
            BreakableTileDatabase.SourceCompanionMovement,
            _group,
            _roomId));
        TryBeginHazard();
    }

    private void StartPeriodicJump()
    {
        _direction = (_angle >> 3) & 0x03;
        StartLongJumpSetup(disableScreenTransitions: true);
        int walls = CalculateAdjacentWallsBitset();
        if ((_angle & 0x04) == 0)
        {
            if (TryStartJumpDownCliff(walls, periodic: true))
            {
                _screenTransitionsDisabled = false;
                SetAnimation(_behavior.LongJumpAnimation + _direction);
                return;
            }
            if (TryStartJumpUpCliff(walls))
                return;
        }
        if (TryStartHoleJump())
            return;

        _phase = RickyCompanionPhase.Hopping;
        _speedZ = _behavior.HopSpeedZ;
        _landingCounter = _behavior.LandingDelay;
        _airborneDelay = 0;
        SetAnimation(_behavior.HopAnimation + _direction);
        _playSound((_random.Next().Value & 0x0f) == 0
            ? _record.RickySound
            : _behavior.JumpSound);
    }

    private void UpdateHopping(ICollection<RoomEntitySpawn> spawns)
    {
        if (_attackJustPressed)
        {
            StartPunch(spawns);
            return;
        }

        Vector2 input = Input.GetVector(
            "move_left", "move_right", "move_up", "move_down");
        int angle = AngleForInput(input);
        if (angle != 0xff)
        {
            bool angleChanged = angle != _angle;
            _angle = angle;
            _direction = (_angle >> 3) & 0x03;
            if (angleChanged)
                SetAnimation(_behavior.HopAnimation + _direction);
        }

        if (!OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, _behavior.HopGravity))
        {
            bool diagonal = (_angle & 0x04) != 0;
            if (diagonal || !IsHoleAt(_precisePosition +
                    _behavior.HoleOffsets[_direction]))
            {
                ApplyCompanionMovement(_behavior.HopSpeed);
            }
            return;
        }

        _zFixed = 0;
        _animation.Advance();
        if (_landingCounter > 0)
            _landingCounter--;
        if (_landingCounter == 0)
            StopUntilLanded(spawns);
    }

    private void UpdateLanding(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_zFixed != 0 && !OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, _behavior.HopGravity))
        {
            return;
        }
        if (_zFixed != 0)
            _zFixed = 0;
        _animation.Advance();
        if (_landingCounter > 0 && --_landingCounter > 0)
            return;

        foreach (Vector2 offset in _behavior.LandingProbes)
        {
            spawns.Add(new RickyTileBreakSpawn(
                _precisePosition + offset,
                BreakableTileDatabase.SourceRickyLanded,
                _group,
                _roomId));
        }
        _phase = RickyCompanionPhase.Riding;
        _screenTransitionsDisabled = false;
        // rickyStopUntilLandedOnGround restores var39 to $10 at a screen
        // edge. Without that grounded window a boundary clamp can make Ricky
        // immediately hop across the edge again while transitions are locked.
        if (IsNearScreenEdge())
            _hopCounter = _behavior.HopDelay;
        SetAnimation(_behavior.IdleAnimation + _direction);
        TryBeginHazard();
    }

    private void StartLongJumpSetup(bool disableScreenTransitions)
    {
        _zFixed = 0;
        _speedZ = _behavior.LongJumpSpeedZ;
        _airborneDelay = _behavior.LongJumpDelay;
        _airborneSpeed = _behavior.LongJumpSpeed;
        _wallCrossingMask = 0;
        _screenTransitionsDisabled = disableScreenTransitions;
        SetAnimation(_behavior.LongJumpAnimation + _direction);
    }

    private bool TryStartHoleJump()
    {
        if ((_angle & 0x04) != 0)
            return false;
        Vector2 offset = _behavior.HoleOffsets[_direction];
        if (!IsHoleAt(_precisePosition + offset))
            return false;

        if (_phase == RickyCompanionPhase.Riding)
            StartLongJumpSetup(disableScreenTransitions: true);
        _phase = RickyCompanionPhase.JumpingOverHole;
        _screenTransitionsDisabled = true;
        return true;
    }

    private bool TryStartJumpUpCliff(int walls)
    {
        if ((walls & 0xc0) != 0xc0 || _angle != 0x00)
            return false;
        bool oneTile = IsCliffUpProbe(_behavior.CliffUpProbes[0]) &&
            IsCliffUpProbe(_behavior.CliffUpProbes[1]);
        bool twoTiles = IsCliffUpProbe(_behavior.CliffUpProbes[2]) &&
            IsCliffUpProbe(_behavior.CliffUpProbes[3]);
        if (!oneTile && !twoTiles)
            return false;

        if (_phase == RickyCompanionPhase.Riding)
            StartLongJumpSetup(disableScreenTransitions: true);
        _phase = RickyCompanionPhase.JumpingUpCliff;
        _wallCrossingMask = 0;
        return true;
    }

    private bool TryStartJumpDownCliff(int walls, bool periodic)
    {
        if ((_angle & 0x07) != 0)
            return false;
        int towardWall = FacingWallMask(_angle, walls);
        if (towardWall is not (0x03 or 0x0c or 0x30))
            return false;

        Vector2[] probes =
        [
            new(0, -6), new(4, 0), new(0, 8), new(-5, 0)
        ];
        byte tile = _room.GetMetatile(
            _precisePosition + probes[_direction]);
        int desiredAngle = tile == _behavior.VineTopTile
            ? 0x10
            : _angle;
        if (desiredAngle != _angle ||
            (tile != _behavior.VineTopTile &&
                !_ledgeJumps.IsCliffTile(
                    _room.ActiveCollisions, tile, _angle)))
        {
            return false;
        }

        _phase = RickyCompanionPhase.JumpingDownCliff;
        _wallCrossingMask = 0;
        _speedZ = periodic
            ? _behavior.CliffDownSpeedZ
            : _behavior.LongJumpSpeedZ;
        _airborneDelay = periodic
            ? _behavior.CliffDownDelay
            : _behavior.LongJumpDelay;
        _airborneSpeed = periodic
            ? _behavior.HopSpeed
            : _behavior.LongJumpSpeed;
        SetAnimation(_behavior.LongJumpAnimation + _direction);
        return true;
    }

    private void UpdateJumpingUpCliff(
        ICollection<RoomEntitySpawn> spawns)
    {
        if (DecrementLongJumpDelay())
            return;
        OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _behavior.HopGravity);
        _animation.Advance();
        ApplyRawMovement(_airborneSpeed);

        int lowerWalls = CalculateAdjacentWallsBitset() & 0x0f;
        if (lowerWalls != 0)
        {
            _wallCrossingMask = lowerWalls;
            return;
        }
        if (_wallCrossingMask != 0)
            StopUntilLanded(spawns);
    }

    private void UpdateJumpingOverHole(
        ICollection<RoomEntitySpawn> spawns)
    {
        if (DecrementLongJumpDelay())
            return;
        if (OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, _behavior.HopGravity))
        {
            StopUntilLanded(spawns);
            return;
        }
        _animation.Advance();
        ApplyCompanionMovement(_airborneSpeed);
        int walls = CalculateAdjacentWallsBitset();
        if (FacingWallMask(_angle, walls) != 0)
            StopUntilLanded(spawns);
    }

    private void UpdateJumpingDownCliff(
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_airborneDelay > 0)
        {
            _airborneDelay--;
            if (_airborneDelay == 0)
                _playSound(_behavior.JumpSound);
            return;
        }

        _animation.Advance();
        ApplyRawMovement(_airborneSpeed);
        OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _behavior.HopGravity);
        int walls = CalculateAdjacentWallsBitset();
        int movingAway = FacingWallMask((_angle + 0x10) & 0x1f, walls);
        if (movingAway != 0)
        {
            _wallCrossingMask = movingAway;
            return;
        }
        if (_wallCrossingMask != 0)
            StopUntilLanded(spawns);
    }

    private bool DecrementLongJumpDelay()
    {
        if (_airborneDelay == 0)
            return false;
        _airborneDelay--;
        if (_airborneDelay != 0)
            return true;
        _playSound(_record.RickySound);
        return false;
    }

    private void StopUntilLanded(ICollection<RoomEntitySpawn> spawns)
    {
        _screenTransitionsDisabled = false;
        _phase = RickyCompanionPhase.Landing;
        _landingCounter = 0;
        if (IsNearScreenEdge())
            _hopCounter = _behavior.HopDelay;
        if (!TryBeginHazard())
            SetAnimation(_behavior.IdleAnimation + _direction);
        _ = spawns;
    }

    private bool IsCliffUpProbe(Vector2 offset)
    {
        TerrainInfo terrain = _room.GetTerrainInfo(_precisePosition + offset);
        return terrain.Collision == 0x03 ||
            terrain.Tile == _behavior.VineTopTile;
    }

    private bool IsHoleAt(Vector2 point)
    {
        byte tile = _room.GetMetatile(point);
        return Array.IndexOf(_behavior.HoleTiles, (int)tile) >= 0;
    }

    private void StartPunch(ICollection<RoomEntitySpawn> spawns)
    {
        _phase = RickyCompanionPhase.Punching;
        _chargeCounter = 1;
        SetAnimation(_behavior.PunchAnimation + _direction);
        spawns.Add(new RickyPunchAttackSpawn(this, _group, _roomId));
        _playSound(_behavior.SwordSlashSound);
    }

    private void UpdatePunching(ICollection<RoomEntitySpawn> spawns)
    {
        bool onGround = _zFixed == 0;
        if (_zFixed != 0)
        {
            onGround = OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, _behavior.HopGravity);
            if (onGround)
            {
                _zFixed = 0;
            }
            else
            {
                ApplyCompanionMovement(_behavior.HopSpeed);
            }
        }
        if (onGround)
        {
            spawns.Add(new RickyTileBreakSpawn(
                _precisePosition + new Vector2(0, 5),
                BreakableTileDatabase.SourceCompanionMovement,
                _group,
                _roomId));
            TryBeginHazard();
        }

        _animation.Advance();
        int signal = _animation.CurrentParameter & 0xc0;
        if (signal == 0)
            return;
        if (signal == 0x40)
        {
            _playSound(_behavior.PunchCueSound);
            return;
        }
        if (_phase != RickyCompanionPhase.Punching)
            return;
        if (_zFixed != 0)
            return;
        if (!_attackPressed)
        {
            ReturnToRiding(_behavior.IdleAnimation);
            return;
        }

        _phase = RickyCompanionPhase.Charging;
        SetAnimation(_behavior.ChargeAnimation + _direction);
    }

    private void UpdateCharging(
        int frameCounter,
        ICollection<RoomEntitySpawn> spawns)
    {
        Vector2 input = Input.GetVector(
            "move_left", "move_right", "move_up", "move_down");
        int angle = AngleForInput(input);
        if (angle != 0xff)
        {
            _angle = angle;
            SetDirectionAnimation(_behavior.ChargeAnimation, animate: true);
        }
        else
        {
            _animation.Advance();
        }

        if (!_attackPressed)
        {
            if (_chargeCounter >= _behavior.ChargeUpdates)
            {
                spawns.Add(new RickyTornadoSpawn(
                    _precisePosition,
                    _direction,
                    _zFixed,
                    _group,
                    _roomId));
                _playSound(OracleSoundEngine.SndCtrlStopSfx);
                _playSound(_behavior.SwordSpinSound);
                StartPunch(spawns);
            }
            else
            {
                ReturnToRiding(_behavior.CancelAnimation);
            }
            return;
        }

        if (_chargeCounter < _behavior.ChargeUpdates)
        {
            _chargeCounter++;
            if (_chargeCounter == _behavior.ChargeUpdates)
                _playSound(_behavior.ChargeSound);
            return;
        }
        spawns.Add(new RickyTileBreakSpawn(
            _precisePosition + new Vector2(0, 5),
            BreakableTileDatabase.SourceCompanionMovement,
            _group,
            _roomId));
        TryBeginHazard();
        _chargePaletteActive = (frameCounter & 0x04) == 0;
    }

    private void ReturnToRiding(int animationBase)
    {
        _phase = RickyCompanionPhase.Riding;
        _chargeCounter = 0;
        _hopCounter = _behavior.HopDelay;
        SetAnimation(animationBase + _direction);
    }

    private void UpdateDismounting(Player player)
    {
        if (!_dismountInitialized)
        {
            _dismountInitialized = true;
            _screenTransitionsDisabled = false;
            CompanionRuntimeState.Remember(
                _runtime,
                CompanionRuntimeState.RickyId,
                _group,
                _roomId,
                _precisePosition);
            CompanionRuntimeState.Clear(
                _runtime,
                CompanionRuntimeState.RickyId);
            player.BeginCompanionDismount(_precisePosition, _direction);
            SetAnimation(0x17);
            return;
        }
        if (player.CompanionJumpActive)
            return;
        if (!_dismountLandingObserved)
        {
            _dismountLandingObserved = true;
            return;
        }

        _dismountPreviousLinkPosition = player.PrecisePosition;
        _phase = RickyCompanionPhase.AwaitingDistance;
    }

    private void UpdateAwaitingDistance(Player player)
    {
        // State $06 substate $02 deliberately does not call
        // specialObjectAnimate. The $17 idle clock resumes only after Link
        // has moved far enough away and Ricky returns to state $01.
        Vector2 sourceOrderLinkPosition = _dismountPreviousLinkPosition;
        _dismountPreviousLinkPosition = player.PrecisePosition;
        if (LinkWithinMountDistance(sourceOrderLinkPosition))
            return;
        _phase = RickyCompanionPhase.Waiting;
    }

    internal void BeginTingleDeparture(string message)
    {
        if (_finished || _phase == RickyCompanionPhase.TingleDeparture)
            return;
        if (LinkRiding || string.IsNullOrWhiteSpace(message))
        {
            throw new InvalidOperationException(
                "tingleScript can only retire a dismounted live Ricky with TX_2006.");
        }

        _tingleDepartureMessage = message;
        _tingleDepartureStage = 0;
        _tingleDepartureCounter = 0;
        _phase = RickyCompanionPhase.TingleDeparture;
    }

    private void UpdateTingleDeparture(Player player)
    {
        switch (_tingleDepartureStage)
        {
            case 0:
                if (!OracleObjectMath.UpdateSpeedZ(
                        ref _zFixed, ref _speedZ, _record.JumpGravity))
                {
                    _animation.Advance();
                    return;
                }
                _zFixed = 0;
                _direction = _precisePosition.Y < player.Position.Y ? 2 : 0;
                SetAnimation(_behavior.HopAnimation + _direction);
                _dialogueRequested(0x2006, _tingleDepartureMessage, _precisePosition);
                _tingleDepartureStage = 1;
                return;
            case 1:
                if (_dialogueOpen())
                    return;
                _direction = 2;
                _angle = 0x14;
                _speedZ = _behavior.HopSpeedZ;
                _tingleDepartureCounter = 24;
                SetAnimation(_behavior.HopAnimation + _direction);
                _tingleDepartureStage = 2;
                return;
            case 2:
                _animation.Advance();
                if (_tingleDepartureCounter > 0)
                {
                    _tingleDepartureCounter--;
                    if (_tingleDepartureCounter == 0)
                        _angle = 0x10;
                }
                ApplyRawMovement(_behavior.HopSpeed);
                if (OracleObjectMath.UpdateSpeedZ(
                        ref _zFixed, ref _speedZ, _behavior.HopGravity))
                {
                    _zFixed = 0;
                    _speedZ = _behavior.HopSpeedZ;
                    _playSound(_behavior.JumpSound);
                }
                if (_precisePosition.Y < _room.Height + 16)
                    return;

                CompanionRuntimeState.Clear(
                    _runtime, CompanionRuntimeState.RickyId);
                CompanionRuntimeState.ForgetRemembered(_runtime);
                if (_saveData is not null)
                {
                    using (_saveData.BeginMutation())
                    {
                        _saveData.WriteWramByte(
                            0xc646,
                            (byte)(_saveData.ReadWramByte(0xc646) | 0x40));
                    }
                }
                _finished = true;
                Visible = false;
                return;
            default:
                throw new InvalidOperationException(
                    $"Ricky's Tingle departure entered substate ${_tingleDepartureStage:x2}.");
        }
    }

    void ICompanionBarrierTarget.ClampToLowerY(int y)
    {
        if (y is < 0 or > byte.MaxValue || _precisePosition.Y <= y)
            return;
        _precisePosition.Y = y;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    private bool LinkWithinMountDistance(Vector2 linkPosition)
    {
        int deltaX = Math.Abs(
            Mathf.FloorToInt(linkPosition.X) -
            Mathf.FloorToInt(_precisePosition.X));
        int deltaY = Math.Abs(
            Mathf.FloorToInt(linkPosition.Y) -
            Mathf.FloorToInt(_precisePosition.Y));
        return deltaX + deltaY < 9;
    }

    private void SetDirectionAnimation(int animationBase, bool animate)
    {
        int direction = DirectionForAngle(_angle, _direction);
        if (direction != _direction)
        {
            _direction = direction;
            SetAnimation(animationBase + _direction);
        }
        else if (animate)
        {
            _animation.Advance();
        }
    }

    private void ApplyRawMovement(int speed)
    {
        OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, speed, _angle);
    }

    private void ApplyCompanionMovement(int speed)
    {
        if (_angle == 0xff)
            return;
        int walls = CalculateAdjacentWallsBitset();
        int movementAngle = AdjustAngleForTileEdge(_angle, walls) ?? _angle;
        int[] bitsToCheck =
        [
            0xcf, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3,
            0xf3, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33,
            0x3f, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c,
            0xfc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc
        ];
        int blocked = walls & bitsToCheck[movementAngle];
        Vector2 candidate = _precisePosition;
        OracleObjectMovement.Shared.ApplySpeed(
            ref candidate, speed, movementAngle);
        Vector2 movement = candidate - _precisePosition;
        if ((blocked & 0xf0) != 0)
            movement.Y = 0;
        if ((blocked & 0x0f) != 0)
            movement.X = 0;
        _precisePosition += movement;
    }

    private int CalculateAdjacentWallsBitset()
    {
        int walls = 0;
        for (int index = 0; index < CollisionSamples.Length; index++)
        {
            if (IsCompanionCollision(
                    _precisePosition + CollisionSamples[index]))
            {
                walls |= 1 << (7 - index);
            }
        }
        return walls;
    }

    private bool IsCompanionCollision(Vector2 point)
    {
        if (point.X < 0 || point.X >= _room.Width ||
            point.Y < 0 || point.Y >= _room.Height)
        {
            return false;
        }
        byte tile = _room.GetMetatile(point);
        if (tile == _behavior.VineTopTile + 1 ||
            tile == _behavior.VineTopTile + 2)
        {
            return true;
        }
        if (IsHoleAt(point))
            return false;
        if (_room.GetTerrainInfo(point).Hazard == HazardType.Hole)
            return false;
        return _room.IsSolid(point);
    }

    private static int FacingWallMask(int angle, int walls)
    {
        if (angle == 0xff)
            return 0;
        int mask = 0;
        if (angle is not (0x08 or 0x18))
            mask |= ((angle >> 3) & 0x03) == 0 ? 0xc0 : 0x30;
        if ((angle & 0x0f) != 0)
            mask |= (angle & 0x10) == 0 ? 0x03 : 0x0c;
        return walls & mask;
    }

    private static int? AdjustAngleForTileEdge(int angle, int walls)
    {
        int[] table =
        [
            0x80, 0x80, 0x01, 0x02, 0x02, 0x02, 0x03, 0x24,
            0x24, 0x24, 0x05, 0x06, 0x06, 0x06, 0x07, 0x48,
            0x48, 0x48, 0x09, 0x0a, 0x0a, 0x0a, 0x0b, 0x1c,
            0x1c, 0x1c, 0x0d, 0x0e, 0x0e, 0x0e, 0x0f, 0x80
        ];
        int entry = table[angle];
        if ((entry & 0x03) != 0)
            return null;
        if ((entry & 0x80) != 0)
        {
            if ((walls & 0xc3) == 0x80) return 0x08;
            if ((walls & 0xcc) == 0x40) return 0x18;
            return null;
        }
        if ((entry & 0x40) != 0)
        {
            if ((walls & 0x33) == 0x20) return 0x08;
            if ((walls & 0x3c) == 0x10) return 0x18;
            return null;
        }
        if ((entry & 0x20) != 0)
        {
            if ((walls & 0xc3) == 0x01) return 0x00;
            if ((walls & 0x33) == 0x02) return 0x10;
            return null;
        }
        if ((walls & 0xcc) == 0x04) return 0x00;
        if ((walls & 0x3c) == 0x08) return 0x10;
        return null;
    }

    private bool CanOccupy(Vector2 position)
    {
        foreach (Vector2 sampleOffset in CollisionSamples)
        {
            Vector2 sample = position + sampleOffset;
            if (sample.X < 0 || sample.X >= _room.Width ||
                sample.Y < 0 || sample.Y >= _room.Height)
            {
                continue;
            }
            if (_room.IsSolid(sample))
                return false;
        }
        return true;
    }

    private bool TryBeginHazard()
    {
        if (_zFixed != 0)
            return false;
        TerrainInfo terrain = _room.GetTerrainInfo(
            _precisePosition + new Vector2(0, 5));
        if (terrain.Hazard == HazardType.None)
            return false;

        int packed = _room.GetPackedPosition(
            _precisePosition + new Vector2(0, 5));
        _hazard = terrain.Hazard;
        _hazardCenter = new Vector2(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        _hazardAnimationStarted = false;
        _screenTransitionsDisabled = true;
        _phase = RickyCompanionPhase.HazardFalling;
        _zFixed = 0;
        _speedZ = 0;
        _playSound(OracleSoundEngine.SndSplash);
        return true;
    }

    private void UpdateHazardFalling(Player player)
    {
        bool water = _hazard == HazardType.Water;
        if (!water && !DragToHazardCenter())
            return;

        if (!_hazardAnimationStarted)
        {
            _hazardAnimationStarted = true;
            SetAnimation(water
                ? _behavior.WaterAnimation
                : _behavior.HoleAnimation);
            if (!water)
            {
                _playSound(OracleSoundEngine.SndLinkFall);
                return;
            }
        }

        _animation.Advance();
        if ((_animation.CurrentParameter & 0x80) == 0)
            return;

        HazardType completedHazard = _hazard;
        Vector2 respawn = ResolveHazardRespawn(player);
        _precisePosition = respawn;
        _hazard = HazardType.None;
        _hazardAnimationStarted = false;
        _screenTransitionsDisabled = false;
        _phase = RickyCompanionPhase.Riding;
        _angle = 0xff;
        _hopCounter = _behavior.HopDelay;
        player.ApplyCompanionHazardDamage(completedHazard);
        SetAnimation(_behavior.CancelAnimation + _direction);
    }

    private bool DragToHazardCenter()
    {
        bool centered = true;
        if (Mathf.FloorToInt(_precisePosition.X) !=
            Mathf.FloorToInt(_hazardCenter.X))
        {
            _precisePosition.X += _precisePosition.X < _hazardCenter.X
                ? 0.25f
                : -0.25f;
            centered = false;
        }
        if (Mathf.FloorToInt(_precisePosition.Y) !=
            Mathf.FloorToInt(_hazardCenter.Y))
        {
            _precisePosition.Y += _precisePosition.Y < _hazardCenter.Y
                ? 0.25f
                : -0.25f;
            centered = false;
        }
        return centered;
    }

    private Vector2 ResolveHazardRespawn(Player player)
    {
        Vector2 localRespawn = player.LocalRespawnPosition;
        if (CanRespawnAt(localRespawn))
            return localRespawn;

        // companionRespawn does not validate this second position. The
        // scrolling finisher keeps the shared mount point in the active room.
        Vector2 lastMount =
            CompanionRuntimeState.ReadLastAnimalMountPosition(_runtime);
        player.SetLocalRespawnCoordinates(lastMount);
        return lastMount;
    }

    private bool CanRespawnAt(Vector2 position) =>
        CanOccupy(position) &&
        _room.GetTerrainInfo(position + new Vector2(0, 5)).Hazard ==
            HazardType.None;

    private bool IsNearScreenEdge()
    {
        Vector2 position = OracleObjectMath.ToPixelPosition(_precisePosition);
        return position.Y <= 6 ||
            position.Y >= _room.Height - 7 ||
            position.X <= 6 ||
            position.X >= _room.Width - 6;
    }

    private void SynchronizePlayer(
        Player player,
        Vector2 screenOffset,
        bool finishMount = false)
    {
        int parameter = _animation.CurrentParameter & 0x3f;
        if (parameter < 0 || parameter >= _linkTextures.Length)
        {
            throw new InvalidOperationException(
                $"Ricky animation ${_animation.AnimationIndex:x2} emitted " +
                $"unsupported Link graphics parameter ${parameter:x2}.");
        }
        Texture2D linkTexture = CurrentLinkTexture(parameter);
        if (finishMount)
        {
            player.FinishCompanionMount(
                _precisePosition,
                Vector2.Zero,
                _direction,
                _zFixed,
                linkTexture,
                _damageLinkTextures[parameter],
                _linkTextureOffsets[parameter]);
        }
        else
        {
            player.SetCompanionRidePosition(
                _precisePosition,
                Vector2.Zero,
                _direction,
                _zFixed,
                linkTexture,
                _damageLinkTextures[parameter],
                _linkTextureOffsets[parameter],
                screenOffset);
        }
    }

    private void SetAnimation(int index)
    {
        _animation.SetAnimation(index);
        QueueRedraw();
    }

    private void UpdateDrawPriority(Player player)
    {
        if (LinkRiding)
        {
            ZIndex = NpcCharacter.BehindLinkZIndex;
            return;
        }
        ZIndex = Position.Y > player.Position.Y + NpcCharacter.LinkPriorityYOffset
            ? NpcCharacter.InFrontOfLinkZIndex
            : NpcCharacter.BehindLinkZIndex;
    }

    public void SetScreenTransitionBoundaryCoordinate(
        bool horizontal,
        int coordinate,
        Player player)
    {
        if (!ControlsPlayerScreenTransition)
        {
            throw new InvalidOperationException(
                "An unmounted Ricky cannot own a screen boundary.");
        }
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
        CompanionRuntimeState.Update(
            _runtime,
            CompanionRuntimeState.RickyId,
            _roomId,
            _precisePosition,
            _direction);
        SynchronizePlayer(player, Vector2.Zero);
    }

    public void BeginScreenTransition(OracleRoomData destination)
    {
        if (!ControlsPlayerScreenTransition || _transitionDestination is not null)
            throw new InvalidOperationException(
                "Ricky received an invalid scrolling handoff.");
        _transitionDestination = destination;
    }

    public void SetScreenTransitionPosition(
        Vector2 position,
        Vector2 screenOffset,
        Player player)
    {
        if (_transitionDestination is null)
        {
            throw new InvalidOperationException(
                "Ricky moved without a transition destination.");
        }
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        SynchronizePlayer(player, screenOffset);
    }

    public void FinishScreenTransition(Vector2 position, Player player)
    {
        OracleRoomData destination = _transitionDestination ??
            throw new InvalidOperationException(
                "Ricky finished scrolling without a destination.");
        _transitionDestination = null;
        _room = destination;
        _roomId = destination.Id;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        Vector2 respawn = OracleObjectMath.ToPixelPosition(position);
        player.SetLocalRespawnPosition(respawn);
        CompanionRuntimeState.SetLastAnimalMountPosition(_runtime, respawn);
        CompanionRuntimeState.Update(
            _runtime,
            CompanionRuntimeState.RickyId,
            _roomId,
            position,
            _direction);
        SynchronizePlayer(player, Vector2.Zero);
    }

    private static int AngleForInput(Vector2 input)
    {
        int x = Math.Sign(input.X);
        int y = Math.Sign(input.Y);
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

    private static int DirectionForAngle(int angle, int currentDirection)
    {
        if (angle == 0xff)
            return currentDirection;
        int firstDirection = (angle >> 3) & 0x03;
        if ((angle & 0x04) == 0)
            return firstDirection;
        int secondDirection = (firstDirection + 1) & 0x03;
        return currentDirection == firstDirection ||
            currentDirection == secondDirection
                ? currentDirection
                : firstDirection;
    }

    private Texture2D CurrentRickyTexture => _chargePaletteActive
        ? _animation.CurrentTextureForPalette(2)
        : _animation.CurrentTexture;

    private Texture2D CurrentLinkTexture(int parameter) =>
        _chargePaletteActive
            ? _chargeLinkTextures[parameter]
            : _linkTextures[parameter];

    private static (
        Texture2D[] Textures,
        Texture2D[] ChargeTextures,
        Texture2D[] DamageTextures,
        Vector2[] Offsets) LoadLinkFrames(RickyCompanionVisualRecord visual)
    {
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.LinkSprite}.png");
        var textures = new Texture2D[visual.LinkFrames.Length];
        var chargeTextures = new Texture2D[visual.LinkFrames.Length];
        var damageTextures = new Texture2D[visual.LinkFrames.Length];
        var offsets = new Vector2[visual.LinkFrames.Length];
        for (int index = 0; index < visual.LinkFrames.Length; index++)
        {
            AnimationFrameDefinition frame =
                OracleGraphicsCache.GetAnimationDefinition(
                    visual.LinkFrames[index]).Frames[0];
            (textures[index], offsets[index]) =
                NpcCharacter.BuildPositionedOamTexture(
                    source,
                    frame.EncodedOam,
                    0,
                    visual.LinkPalette,
                    paletteOverride: null,
                    sourceGrayscaleInverted: true,
                    sourceOffset: visual.LinkSourceOffsets[index]);
            (damageTextures[index], Vector2 damageOffset) =
                NpcCharacter.BuildPositionedOamTexture(
                    source,
                    frame.EncodedOam,
                    0,
                    visual.LinkPalette,
                    NpcCharacter.GetStandardSpritePalette(5),
                    sourceGrayscaleInverted: true,
                    sourceOffset: visual.LinkSourceOffsets[index]);
            (chargeTextures[index], Vector2 chargeOffset) =
                NpcCharacter.BuildPositionedOamTexture(
                    source,
                    frame.EncodedOam,
                    0,
                    visual.LinkPalette,
                    NpcCharacter.GetStandardSpritePalette(2),
                    sourceGrayscaleInverted: true,
                    sourceOffset: visual.LinkSourceOffsets[index]);
            if (damageOffset != offsets[index] ||
                chargeOffset != offsets[index])
            {
                throw new InvalidOperationException(
                    "Ricky Link damage palette changed the OAM origin.");
            }
        }
        return (textures, chargeTextures, damageTextures, offsets);
    }
}

internal enum RickyCompanionPhase
{
    Waiting,
    Mounting,
    Riding,
    Hopping,
    Landing,
    JumpingUpCliff,
    JumpingOverHole,
    JumpingDownCliff,
    HazardFalling,
    Punching,
    Charging,
    Dismounting,
    AwaitingDistance,
    TingleDeparture
}

internal sealed record RickyCompanionSpawn(
    Vector2 Position,
    int Direction,
    int Group,
    int Room,
    bool ForceMount = false,
    bool Riding = false) : RoomEntitySpawn;
