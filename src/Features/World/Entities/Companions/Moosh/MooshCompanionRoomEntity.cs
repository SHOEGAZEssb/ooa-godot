using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>
/// Shared SPECIALOBJECT_MOOSH $0d body; native initialization/story states
/// belong to the selected game's binding.
/// </summary>
internal sealed partial class MooshCompanionRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IPlayerRestriction,
    IPlayerForcedMovement, IPlayerRideableRoomEntity,
    IPlayerScreenTransitionRoomEntity, IRoomEntityLifetime,
    ICompanionBarrierTarget, IRoomInitializedCompanion, INativeSpecialObjectRoomEntity
{
    int INativeSpecialObjectRoomEntity.NativeSpecialObjectId => SpecialObjectId.Moosh;
    bool INativeSpecialObjectRoomEntity.SupportsDebugReconstruction => _zFixed == 0 &&
        _runtime.ReadWramByte(0xd104) is 1 or 5;
    int INativeSpecialObjectRoomEntity.NativeSpecialObjectDirection => _direction;
    Vector2 INativeSpecialObjectRoomEntity.NativeSpecialObjectPosition
    {
        get => _precisePosition;
        set
        {
            bool entering = _runtime.ReadWramByte(0xd104) == 0x0c;
            if (value.X >= 248 && (_precisePosition.X < 0 || entering && _direction == 1)) value.X -= 256;
            if (value.Y >= 248 && (_precisePosition.Y < 0 || entering && _direction == 2)) value.Y -= 256;
            _precisePosition = value; Position = OracleObjectMath.ToPixelPosition(value);
        }
    }
    public CompanionRoomInitialization? RoomInitialization { get; set; }
    void INativeSpecialObjectRoomEntity.OnNativeSpecialObjectBound()=>RestoreNativePresentation();
    void INativeSpecialObjectRoomEntity.RestoreNativePresentation()=>RestoreNativePresentation();
    private void RestoreNativePresentation()
    {
        if(_runtime.ReadWramByte(0xd104) is not (1 or 5) ||
            !CompanionNativeAnimationDatabase.For(AssetRoot).TryRestoreAnimation(_runtime,_animation))return;
        _zFixed=unchecked((short)(_runtime.ReadWramByte(0xd10e)|_runtime.ReadWramByte(0xd10f)<<8));
        _speedZ=unchecked((short)(_runtime.ReadWramByte(0xd114)|_runtime.ReadWramByte(0xd115)<<8));
        _adjacentWalls=_runtime.ReadWramByte(0xd133);
        Visible=(_runtime.ReadWramByte(0xd11a)&0x80)!=0;
        ZIndex=LinkRiding?ObjectDrawPriority.BehindLinkZIndex:ObjectDrawPriority.FromVisible(_runtime.ReadWramByte(0xd11a));
    }

    internal void ContinueNativePresentation(Player player, int frame, int counter, bool mounting)
    {
        _direction = _runtime.ReadWramByte(0xd108);
        _angle = _runtime.ReadWramByte(0xd109);
        _zFixed = unchecked((short)(_runtime.ReadWramByte(0xd10e) | _runtime.ReadWramByte(0xd10f) << 8));
        _speedZ = unchecked((short)(_runtime.ReadWramByte(0xd114) | _runtime.ReadWramByte(0xd115) << 8));
        _animation.SetAnimation(_runtime.ReadWramByte(0xd130));
        _animation.RestoreFrame(frame, counter);
        if (!mounting) return;
        _mountStarted = true;
        player.BeginCompanionMount(player.PrecisePosition);
    }

    private bool UpdateFluteEntrance(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_runtime.ReadWramByte(0xd103) == 0)
        {
            SpecialObjectOamDatabase.For(AssetRoot).InitializeCompanion(_runtime);
            _runtime.SetWramByte(0xd103, 1);
            _runtime.SetWramByte(0xd124, 0x80);
            _runtime.SetWramByte(0xd11a, 0xc1);
            _runtime.SetWramByte(0xd107, 0x3c);
            _playSound(SoundId.SndMoosh);
            SetAnimation(0x0f + _direction);
            return false;
        }
        if (_runtime.ReadWramByte(0xd103) != 1)
            throw new NotSupportedException($"moosh.s: state$0c parameter${_runtime.ReadWramByte(0xd103):x2} has no native dispatch.");
        _animation.Advance();
        Vector2 before = _precisePosition;
        _speed = 0x1e; // mooshStateCSubstate1 rewrites speed on each walk update.
        SpecialObjectMovement.ApplySpeed(_runtime, ref _precisePosition, _speed, _angle, AdjacentWalls());
        if (before.X < 0 && _precisePosition.X > 128) _precisePosition.X -= 256;
        if (before.Y < 0 && _precisePosition.Y > 128) _precisePosition.Y -= 256;
        if ((_zFixed >> 8) == 0) BreakGroundTile(spawns);
        Vector2 offset = _direction switch
        {
            0 => new(0, -8), 1 => new(8, 0), 2 => new(0, 8), _ => new(-8, 0)
        };
        // mooshStateC/companionRetIfNotFinishedWalkingIn stop at a nonzero
        // collision tile or the sixtieth clear-tile update.
        if (_room.GetTerrainInfo(_precisePosition + offset).Collision == 0)
        {
            byte counter = unchecked((byte)(_runtime.ReadWramByte(0xd107) - 1));
            _runtime.SetWramByte(0xd107, counter);
            if (counter != 0) return false;
        }
        _runtime.SetWramByte(0xd103, 0);
        CompanionRuntimeState.UpdatePosition(_runtime, SpecialObjectId.Moosh, _precisePosition);
        _initialize(this, frame, spawns);
        return true;
    }

    private static readonly Vector2[] CollisionSamples =
    [
        new(-3, -5), new(4, -5), new(-3, 8), new(4, 8),
        new(-5, -3), new(-5, 6), new(6, -3), new(6, 6)
    ];

    private readonly MooshDatabase _data;
    private readonly CompanionVisualDatabase _visual;
    private readonly OracleSaveData _saveData;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly Action<INativeSpecialObjectRoomEntity, RoomEntityFrame, ICollection<RoomEntitySpawn>> _initialize;
    private readonly Action<int> _screenShakeRequested;
    private readonly EnemyAnimationPlayer _animation;
    private CompanionTerrainDatabase? _terrainSourceCache;
    private CompanionTerrainDatabase _terrain => _terrainSourceCache ??= new(AssetRoot);
    private LedgeJumpDatabase? _ledgesSourceCache;
    private LedgeJumpDatabase _ledges => _ledgesSourceCache ??= new(AssetRoot);
    private readonly Func<OracleRoomData, CompanionAttackTileBreaker> _createTileBreaker;
    private CompanionAttackTileBreaker _tileBreaker;
    private int _cliffCounter { get => _runtime.ReadWramByte(0xd106); set => _runtime.SetWramByte(0xd106, unchecked((byte)value)); }
    private int _cliffWalls { get => _runtime.ReadWramByte(0xd107); set => _runtime.SetWramByte(0xd107, unchecked((byte)value)); }
    private readonly Texture2D[] _linkTextures;
    private readonly Texture2D[] _chargeLinkTextures;
    private readonly Texture2D[] _damageLinkTextures;
    private readonly Vector2[] _linkTextureOffsets;
    private OracleRoomData _room;
    private OracleRoomData? _transitionDestination;
    private Vector2 _precisePosition;
    private int _group;
    private int _roomId;
    private int _direction { get => _runtime.ReadWramByte(0xd108); set => _runtime.SetWramByte(0xd108, checked((byte)value)); }
    private int _angle { get => _runtime.ReadWramByte(0xd109); set => _runtime.SetWramByte(0xd109, checked((byte)value)); }
    private int _zFixed;
    private int _speedZ;
    private int _speed { get => _runtime.ReadWramByte(0xd110); set => _runtime.SetWramByte(0xd110, checked((byte)value)); }
    private int _gravityDelay { get => _runtime.ReadWramByte(0xd139); set => _runtime.SetWramByte(0xd139, unchecked((byte)value)); }
    private int _flapCount { get => _runtime.ReadWramByte(0xd13a); set => _runtime.SetWramByte(0xd13a, unchecked((byte)value)); }
    private int _chargeCounter { get => _runtime.ReadWramByte(0xd13b); set => _runtime.SetWramByte(0xd13b, unchecked((byte)value)); }
    private int _waterHoverCounter { get => _runtime.ReadWramByte(0xd106); set => _runtime.SetWramByte(0xd106, unchecked((byte)value)); }
    private CompanionHazard _hazard;
    private bool _hazardMounted;
    private bool _stompContactDisabled;
    private bool _mountStarted;
    private int _adjacentWalls;
    private bool _attackPressed;
    private bool _itemPressed;
    private bool _attackJustPressed;
    private bool _itemJustPressed;
    private ComponentInputEdge _attackEdge;
    private ComponentInputEdge _itemEdge;
    private bool _chargePaletteActive;
    private bool _finished;
    // The native state/substate union also receives writes from scripts and items.
    private MooshCompanionPhase _phase
    {
        get => _runtime.ReadWramByte(0xd104) switch
        {
            1 => MooshCompanionPhase.Waiting,
            2 or 9 or 0x0b => MooshCompanionPhase.Inactive,
            3 => MooshCompanionPhase.Mounting,
            4 => MooshCompanionPhase.HazardFalling,
            5 => MooshCompanionPhase.Riding,
            6 => _runtime.ReadWramByte(0xd105) switch
            {
                0 or 1 => MooshCompanionPhase.Dismounting,
                2 => MooshCompanionPhase.AwaitingDistance,
                var sub => throw new NotSupportedException($"moosh.s: state$06 substate${sub:x2} has no native dispatch.")
            },
            7 => MooshCompanionPhase.CliffJump,
            8 => _runtime.ReadWramByte(0xd105) switch
            {
                0 or 1 => MooshCompanionPhase.Airborne,
                2 => MooshCompanionPhase.Charging,
                3 => MooshCompanionPhase.Falling,
                4 => MooshCompanionPhase.StompRecovery,
                5 => MooshCompanionPhase.HoveringOverWater,
                var sub => throw new NotSupportedException($"moosh.s: state$08 substate${sub:x2} has no native dispatch.")
            },
            0x0c => MooshCompanionPhase.FluteEntering,
            var state => throw new NotSupportedException($"moosh.s: {_runtime.Game} body state${state:x2} requires its native initialization/story binding.")
        };
        set
        {
            byte state = value switch
            {
                MooshCompanionPhase.Waiting => 1, MooshCompanionPhase.Mounting => 3,
                MooshCompanionPhase.HazardFalling => 4, MooshCompanionPhase.Riding => 5,
                MooshCompanionPhase.Dismounting or MooshCompanionPhase.AwaitingDistance => 6,
                MooshCompanionPhase.CliffJump => 7,
                MooshCompanionPhase.Airborne or MooshCompanionPhase.Charging or MooshCompanionPhase.Falling or
                    MooshCompanionPhase.StompRecovery or MooshCompanionPhase.HoveringOverWater => 8,
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
            _runtime.SetWramByte(0xd104, state);
            if (state is 5 or 6 or 7 or 8)
                _runtime.SetWramByte(0xd105, value switch
                {
                    MooshCompanionPhase.AwaitingDistance or MooshCompanionPhase.Charging => 2,
                    MooshCompanionPhase.Falling => 3, MooshCompanionPhase.StompRecovery => 4,
                    MooshCompanionPhase.HoveringOverWater => 5, _ => 0
                });
        }
    }

    public Node2D Node => this;
    public bool DisablesSword => LinkRiding || _phase == MooshCompanionPhase.Mounting;
    public bool DisablesItems => DisablesSword;
    public bool DisablesPlayerContact => LinkRiding && _stompContactDisabled;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesScreenTransitions => _phase is
        MooshCompanionPhase.Mounting or
        MooshCompanionPhase.Airborne or
        MooshCompanionPhase.HoveringOverWater or
        MooshCompanionPhase.Charging or
        MooshCompanionPhase.Falling or
        MooshCompanionPhase.StompRecovery or
        MooshCompanionPhase.HazardFalling;
    public bool Finished => _finished;
    public bool LinkRiding => _runtime.ReadWramByte(0xd104) is 5 or 7 or 8 ||
        _runtime.ReadWramByte(0xd104) is 2 or 4 or 6 or 9 or 0x0b && _runtime.ReadField("wLinkObjectIndex") == 0xd1;
    public bool ControlsPlayerScreenTransition => LinkRiding;
    public bool BypassesScreenTransitionInputGate => false;
    public Vector2 ScreenTransitionPosition => _precisePosition;
    int ICompanionBarrierTarget.CompanionId => CompanionRuntimeState.MooshId;
    bool ICompanionBarrierTarget.BarrierMounted => LinkRiding;
    Vector2 ICompanionBarrierTarget.BarrierPosition => _precisePosition;

    internal bool Mounted => _phase == MooshCompanionPhase.Riding;
    internal MooshCompanionPhase Phase => _phase;
    internal int Direction => _direction;
    internal int Angle => _angle;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int ChargeCounter => _chargeCounter;
    internal int WaterHoverCounter => _waterHoverCounter;
    internal int FlapCount => _flapCount;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int LinkAnimationParameter => _animation.CurrentParameter & 0x3f;
    internal bool ChargePaletteActive => _chargePaletteActive;
    internal HazardType Hazard => _hazard.Type;
    internal ulong MooshTexturePixelHash => OracleGraphicsCache.PixelHash(
        CurrentMooshTexture.GetImage());
    internal ulong NormalMooshTexturePixelHash => OracleGraphicsCache.PixelHash(
        _animation.CurrentTexture.GetImage());
    internal ulong LinkTexturePixelHash => OracleGraphicsCache.PixelHash(
        CurrentLinkTexture(LinkAnimationParameter).GetImage());
    internal ulong NormalLinkTexturePixelHash => OracleGraphicsCache.PixelHash(
        _linkTextures[LinkAnimationParameter].GetImage());
    internal Vector2 LinkTextureOffset =>
        _linkTextureOffsets[LinkAnimationParameter];
    private Vector2 RidingLinkOffset => (_direction & 1) == 0
        ? new Vector2(0, -14)
        : new Vector2(0, -16);

    internal MooshCompanionRoomEntity(
        MooshCompanionSpawn spawn,
        OracleRoomData room,
        MooshDatabase database,
        OracleSaveData? saveData,
        OracleRuntimeState runtime,
        Action<int> playSound,
        Action<int> screenShakeRequested,
        Func<OracleRoomData, CompanionAttackTileBreaker> createTileBreaker,
        Action<INativeSpecialObjectRoomEntity, RoomEntityFrame, ICollection<RoomEntitySpawn>> initialize)
    {
        AssetRoot = room.AssetRoot;
        _data = database;
        _visual = database.Visual;
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        _runtime = runtime;
        _playSound = playSound;
        _initialize = initialize;
        _screenShakeRequested = screenShakeRequested;
        _room = room;
        _createTileBreaker = createTileBreaker;
        _tileBreaker = createTileBreaker(room);
        _group = spawn.Group;
        _roomId = spawn.Room;
        _precisePosition = spawn.Position;
        Position = spawn.Position;
        if (spawn.FluteDestination is Vector2 destination)
        {
            CompanionRuntimeState.SetLastAnimalMountPosition(_saveData, destination);
        }
        _zFixed = unchecked((short)(_runtime.ReadWramByte(0xd10e) | _runtime.ReadWramByte(0xd10f) << 8));
        _speedZ = unchecked((short)(_runtime.ReadWramByte(0xd114) | _runtime.ReadWramByte(0xd115) << 8));

        CompanionVisualDatabase visual = _visual;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            OracleGraphicsCache.LoadImage(
                AssetRoot + $"gfx/{visual.Sprite}.png"),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            positionedOam: true,
            paletteVariants: [2], animationSourceOffsets: visual.SourceOffsets);
        (_linkTextures, _chargeLinkTextures, _damageLinkTextures,
            _linkTextureOffsets) =
            CompanionLinkFrames.Load(AssetRoot,
                visual.LinkSprite, visual.LinkPalette,
                visual.LinkFrames, visual.LinkSourceOffsets);
        SetAnimation(LinkRiding
                ? 0x13 + _direction
                : 0x01 + _direction);
        Name = $"Moosh_{_group:x1}_{_roomId:x2}";
        ZIndex = LinkRiding
            ? ObjectDrawPriority.BehindLinkZIndex
            : Player.NormalZIndex;
        Visible = (_runtime.ReadWramByte(0xd11a) & 0x80) != 0;
    }

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (_phase == MooshCompanionPhase.Mounting && _mountStarted)
            player.NudgeCompanionMountToward(_precisePosition);
        else if (LinkRiding)
            SynchronizePlayer(player, Vector2.Zero);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (RoomInitialization is { Pending: true } initialization)
        {
            if (initialization.Advance(ref _precisePosition))
            {
                _finished = initialization.Rejected;
                _angle = 2;
                _direction = 2;
                if (!_finished)
                {
                    CompanionRuntimeState.Update(_runtime, SpecialObjectId.Moosh, _precisePosition, _direction);
                    _runtime.SetWramByte(0xd109, 2);
                }
                Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                _animation.SetAnimation(_animation.AnimationIndex);
                Visible = !_finished;
            }
            return;
        }
        _attackPressed = Input.IsActionPressed("attack");
        _itemPressed = Input.IsActionPressed("item");
        // mooshState5/state8 read wGameKeysJustPressed. Do not reconstruct
        // that edge from Moosh's last entity update: ordinary entities pause
        // while a textbox owns input, so doing so turns the held button which
        // dismissed the text into a fresh companion action after the pause.
        _attackJustPressed = _attackEdge.Read(Input.IsActionJustPressed("attack"));
        _itemJustPressed = _itemEdge.Read(Input.IsActionJustPressed("item"));
        _chargePaletteActive = false;

        switch (_phase)
        {
            case MooshCompanionPhase.FluteEntering:
                if (UpdateFluteEntrance(frame, spawns)) return;
                break;
            case MooshCompanionPhase.Inactive:
                break;
            case MooshCompanionPhase.Waiting:
                UpdateWaiting(frame.Player);
                break;
            case MooshCompanionPhase.Mounting:
                UpdateMounting(frame.Player);
                break;
            case MooshCompanionPhase.Riding:
                UpdateRiding(frame.Player, spawns);
                break;
            case MooshCompanionPhase.CliffJump:
                UpdateCliffJump();
                break;
            case MooshCompanionPhase.Airborne:
                UpdateAirborne(frame.Player, spawns);
                break;
            case MooshCompanionPhase.HoveringOverWater:
                UpdateHoveringOverWater(frame.Player, spawns);
                break;
            case MooshCompanionPhase.Charging:
                UpdateCharging(frame.Player, frame.Counter);
                break;
            case MooshCompanionPhase.Falling:
                UpdateFalling(frame.Player, spawns);
                break;
            case MooshCompanionPhase.StompRecovery:
                UpdateStompRecovery();
                break;
            case MooshCompanionPhase.HazardFalling:
                UpdateHazardFalling(frame.Player);
                break;
            case MooshCompanionPhase.Dismounting:
                UpdateDismounting(frame.Player);
                break;
            case MooshCompanionPhase.AwaitingDistance:
                UpdateAwaitingDistance(frame.Player);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Moosh phase {_phase}.");
        }

        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (LinkRiding || CompanionRuntimeState.IsActive(_runtime, CompanionRuntimeState.MooshId))
            CompanionRuntimeState.Update(
                _runtime, CompanionRuntimeState.MooshId, _precisePosition, _direction);
        if (LinkRiding)
        {
            SynchronizePlayer(frame.Player, Vector2.Zero);
        }
        UpdateDrawPriority(frame.Player);
        if (CompanionRuntimeState.IsActive(_runtime, SpecialObjectId.Moosh))
        {
            _runtime.SetWramByte(0xd109, (byte)_angle);
            _runtime.SetWramByte(0xd10e, unchecked((byte)_zFixed));
            _runtime.SetWramByte(0xd10f, unchecked((byte)(_zFixed >> 8)));
            _runtime.SetWramByte(0xd114, unchecked((byte)_speedZ));
            _runtime.SetWramByte(0xd115, unchecked((byte)(_speedZ >> 8)));
            CompanionNativeAnimationDatabase.For(_visual.AssetRoot).PublishAnimation(_runtime,SpecialObjectId.Moosh,
                _animation.AnimationIndex,_animation.FrameIndex,_animation.RemainingUpdates,_animation.CurrentParameter);
        }
        Visible = (_runtime.ReadWramByte(0xd11a) & 0x80) != 0;
        QueueRedraw();
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) =>
        SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        DrawTexture(
            CurrentMooshTexture,
            _animation.CurrentOffset +
            new Vector2(0, _zFixed >> 8) +
            SourceOamDrawOffset);
    }

    private void UpdateWaiting(Player player)
    {
        _animation.Advance();
        if (CompanionRuntimeState.MountingDisabled(_runtime) || !player.CanMountCompanion ||
            !LinkWithinMountDistance(player))
        {
            // mooshState1 falls through to hazards only outside the mount
            // radius; a rejected mount returns from companionTryToMount.
            if (!LinkWithinMountDistance(player)) TryBeginHazard();
            return;
        }
        _phase = MooshCompanionPhase.Mounting;
        player.BeginCompanionMount(player.PrecisePosition);
        _mountStarted = true;
    }

    private void UpdateMounting(Player player)
    {
        if (CompanionRuntimeState.MountingDisabled(_runtime) || player.CompanionMountInterrupted)
        {
            _phase = MooshCompanionPhase.Waiting;
            _mountStarted = false;
            return;
        }
        if (!_mountStarted)
        {
            player.BeginCompanionMount(player.PrecisePosition);
            _mountStarted = true;
            return;
        }
        player.NudgeCompanionMountToward(_precisePosition);
        if (!player.CompanionJumpReadyToRide)
            return;

        _phase = MooshCompanionPhase.Riding;
        _angle = 0xff;
        _zFixed = 0;
        _speedZ = 0;
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        SetAnimation(0x13 + _direction);
        CompanionRuntimeState.BeginAnimalRide(
            _runtime, _saveData, CompanionRuntimeState.MooshId, _precisePosition, _direction);
        SynchronizePlayer(player, Vector2.Zero, finishMount: true);
    }

    private void UpdateRiding(Player player, ICollection<RoomEntitySpawn> spawns)
    {
        if (!OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x10)) return;
        if (TryBeginHazard())
            return;
        if (_attackJustPressed)
        {
            _phase = MooshCompanionPhase.Airborne;
            _playSound(SoundId.SndJump);
            return;
        }
        if (_itemJustPressed)
        {
            TryBeginDismount(player);
            return;
        }
        UpdateRidingMovement(spawns);
    }

    private void UpdateAirborne(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = player;
        if (_runtime.ReadWramByte(0xd105) == 0)
        {
            // mooshPressedAButton only selects state $08/substate $00. The
            // following update initializes speedZ, counters, and animation
            // before yielding without vertical movement.
            _runtime.SetWramByte(0xd105, 1);
            _speedZ = -0x0140;
            _speed = 0x28;
            _gravityDelay = 4;
            _flapCount = 0;
            _chargeCounter = 0;
            SetAnimation(0x09 + _direction);
            return;
        }
        if (TryBeginWaterHover(spawns))
            return;
        UpdateAirborneMovement(spawns);
        bool movingUp = _speedZ < 0;
        if (!movingUp)
        {
            _chargeCounter = _attackPressed
                ? _chargeCounter + 1
                : 0;
            if (_chargeCounter >= 10)
            {
                _phase = MooshCompanionPhase.Charging;
                return;
            }

            if (_attackJustPressed && _flapCount != 0x10)
            {
                _flapCount++;
                _gravityDelay += 8;
                _playSound(SoundId.SndJump);
                _animation.SetFrameCounter(1);
                _animation.Advance();
            }
        }

        if (movingUp)
        {
            UpdateDirectionAndAnimation(0x09);
        }
        else if (_gravityDelay > 0)
        {
            _gravityDelay--;
            // State $08 substate $01 writes $0f before calling
            // companionUpdateDirectionAndAnimate, freezing the flutter frame
            // while var39 delays gravity unless the direction changes.
            _animation.SetFrameCounter(0x0f);
            UpdateDirectionAndAnimation(0x09);
            return;
        }

        if (OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, 0x10))
        {
            BreakGroundTile(spawns);
            LandAndCheckGround(spawns);
            return;
        }
    }

    private bool TryBeginWaterHover(ICollection<RoomEntitySpawn> spawns)
    {
        HazardType hazard = _room.GetTerrainInfo(
            _precisePosition + new Vector2(0, 5)).Hazard;
        if ((int)hazard != _data.WaterHazard)
            return false;

        _phase = MooshCompanionPhase.HoveringOverWater;
        _speedZ = 0;
        _waterHoverCounter = _data.WaterHoverFrames;
        spawns.Add(new MooshHoverExclamationSpawn(
            _precisePosition,
            _zFixed));
        return true;
    }

    private void UpdateHoveringOverWater(Player player, ICollection<RoomEntitySpawn> spawns)
    {
        _ = player;
        if (_waterHoverCounter > 0)
        {
            _waterHoverCounter--;
            if (_waterHoverCounter > 0)
            {
                _animation.Advance();
                return;
            }
        }

        if (OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, 0x10))
        {
            _waterHoverCounter = 0;
            LandAndCheckGround(spawns);
        }
    }

    private void UpdateCharging(Player player, int frameCounter)
    {
        _ = player;
        _animation.Advance();
        if (_attackPressed)
        {
            if (_chargeCounter >= 40)
                _chargePaletteActive = (frameCounter & 0x04) == 0;
            _chargeCounter++;
            if (_chargeCounter < 40) return;
            if (_chargeCounter == 40)
            {
                _playSound(SoundId.SndChargeSword);
                return;
            }
            // State $08:$02 returns immediately at 40; collision bit 7 is
            // first cleared at 41 and stays clear until recovery or a hazard.
            if (_chargeCounter > 40) _stompContactDisabled = true;
            if (_chargeCounter != 120)
                return;
        }
        _chargePaletteActive = false;
        _phase = MooshCompanionPhase.Falling;
        if (_chargeCounter >= 40)
            SetAnimation(0x17 + _direction);
    }

    private void UpdateFalling(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, 0x80))
        {
            return;
        }
        if (_chargeCounter < 40)
        {
            LandAndCheckGround(spawns);
            return;
        }
        if (TryBeginHazard())
            return;

        _phase = MooshCompanionPhase.StompRecovery;
        if (_saveData is not null)
            _saveData.WriteField("wCompanionTutorialTextShown", (byte)(_saveData.ReadField("wCompanionTutorialTextShown") | 0x20));
        _screenShakeRequested(0x0f);
        _playSound(SoundId.SndCtrlStopSfx);
        _playSound(SoundId.SndScentSeed);
        spawns.Add(new MooshStompAttackSpawn(
            player.Position + new Vector2(0, 16),
            _group,
            _roomId));
    }

    private void UpdateStompRecovery()
    {
        _animation.Advance();
        if ((_animation.CurrentParameter & 0x80) != 0)
            LandNormally(checkHazards: false);
    }

    internal bool TryBeginDismount(Player player)
    {
        if (_phase != MooshCompanionPhase.Riding)
            return false;
        _phase = MooshCompanionPhase.Dismounting;
        return true;
    }

    private void UpdateDismounting(Player player)
    {
        if (_runtime.ReadWramByte(0xd105) == 0)
        {
            // The B-button update only changes Moosh to state $06. On the
            // following companion pass, substate 0 performs the dismount,
            // saves both respawn anchors, and selects animation $01+direction.
            _runtime.SetWramByte(0xd105, 1);
            CompanionRuntimeState.DismountAnimal(
                _runtime, CompanionRuntimeState.MooshId);
            player.BeginCompanionDismount(_precisePosition, _direction);
            CompanionRuntimeState.RememberAfterDismount(_runtime,player.Inventory,SpecialObjectId.Moosh,_group,_roomId,_precisePosition);
            SetAnimation(0x01 + _direction);
            return;
        }
        if (player.CompanionJumpActive)
            return;

        _phase = MooshCompanionPhase.AwaitingDistance;
    }

    private void UpdateAwaitingDistance(Player player)
    {
        if (LinkWithinMountDistance(player.PrecisePosition))
        {
            TryBeginHazard();
            return;
        }
        _phase = MooshCompanionPhase.Waiting;
        _runtime.SetWramByte(0xd105, 0);
    }

    private bool LinkWithinMountDistance(Player player) =>
        LinkWithinMountDistance(player.PrecisePosition);

    private bool LinkWithinMountDistance(Vector2 linkPosition)
    {
        // objectCheckLinkWithinDistance subtracts the absolute Y difference
        // from c before comparing X, so c=$09 is a strict Manhattan radius
        // over the objects' high-byte coordinates.
        int deltaX = Math.Abs(
            Mathf.FloorToInt(linkPosition.X) -
            Mathf.FloorToInt(_precisePosition.X));
        int deltaY = Math.Abs(
            Mathf.FloorToInt(linkPosition.Y) -
            Mathf.FloorToInt(_precisePosition.Y));
        return deltaX + deltaY < 9;
    }

    private void LandNormally(bool checkHazards = true)
    {
        _stompContactDisabled = false;
        _phase = MooshCompanionPhase.Riding;
        SetAnimation(0x13 + _direction);
        if (checkHazards)
            TryBeginHazard();
    }

    private void LandAndCheckGround(ICollection<RoomEntitySpawn> spawns)
    {
        LandNormally(checkHazards: false);
        BreakGroundTile(spawns);
        if (!TryBeginHazard()) UpdateDirectionAndAnimation(0x13);
    }

    private bool TryBeginHazard()
    {
        if (_zFixed != 0) return false;
        if (!CompanionHazard.TryCreate(
                _room, _precisePosition, out _hazard))
            return false;
        _hazardMounted = LinkRiding;
        _stompContactDisabled = false;
        _phase = MooshCompanionPhase.HazardFalling;
        _runtime.SetWramByte(0xd106,0);
        _runtime.SetWramByte(0xd137,_hazard.Type==HazardType.Water?(byte)0x0d:(byte)0x0e);
        _zFixed = 0;
        _speedZ = 0;
        _playSound(SoundId.SndSplash);
        return true;
    }

    private void UpdateHazardFalling(Player player)
    {
        if (!_hazard.Advance(
                ref _precisePosition,
                _animation,
                _runtime,0xff,_runtime.ReadWramByte(0xd137)==0x0d,
                waterAnimation: 0x0d,
                holeAnimation: 0x0e,
                SetAnimation,
                _playSound))
        {
            return;
        }

        HazardType completedHazard = _hazard.Type;
        int respawnAngle = _angle;
        Vector2 respawn = CompanionHazard.ResolveRespawn(
            player, _saveData, _runtime, ref respawnAngle, CanRespawnAt,_room,_precisePosition);
        _angle = respawnAngle;
        _precisePosition = respawn;
        _hazard = default;
        _phase = _hazardMounted ? MooshCompanionPhase.Riding : MooshCompanionPhase.Waiting;
        _direction = (_angle >> 3) & 3;
        if (_hazardMounted) player.ApplyCompanionHazardDamage(completedHazard);
        SetAnimation((_hazardMounted ? 0x13 : 1) + _direction);
    }

    private bool CanRespawnAt(Vector2 position) =>
        CanOccupy(position) &&
        _room.GetTerrainInfo(position + new Vector2(0, 5)).Hazard ==
            HazardType.None;

    private void UpdateRidingMovement(ICollection<RoomEntitySpawn> spawns)
    {
        Vector2 input = Input.GetVector(
            "move_left", "move_right", "move_up", "move_down");
        int angle = CompanionMovement.AngleForInput(input);
        if (angle == 0xff)
        {
            return;
        }

        bool angleChanged = angle != _angle;
        _angle = angle;
        if (angleChanged)
        {
            // mooshState5 yields after companionUpdateDirectionAndAnimate on
            // every exact wLinkAngle change. No SPEED_100 movement occurs on
            // that update.
            UpdateDirectionAndAnimation(0x13);
            return;
        }

        if (TryStartCliffJump()) return;
        _speed = 0x28;
        ApplyMovement(spawns);
        BreakGroundTile(spawns);
        if (!TryBeginHazard()) UpdateDirectionAndAnimation(0x13);
    }

    private void UpdateAirborneMovement(ICollection<RoomEntitySpawn> spawns)
    {
        Vector2 input = Input.GetVector(
            "move_left", "move_right", "move_up", "move_down");
        int angle = CompanionMovement.AngleForInput(input);
        if (angle == 0xff)
        {
            return;
        }
        _angle = angle;
        ApplyMovement(spawns);
    }

    private void ApplyMovement(ICollection<RoomEntitySpawn> spawns)
    {
        SpecialObjectMovement.ApplySpeed(_runtime, ref _precisePosition, _speed, _angle, _adjacentWalls = AdjacentWalls());
        if ((_zFixed >> 8) == 0) BreakGroundTile(spawns);
    }

    private void BreakGroundTile(ICollection<RoomEntitySpawn> spawns) =>
        _tileBreaker.TryBreak(_precisePosition + new Vector2(0, 5),
            BreakableTileDatabase.SourceCompanionMovement, spawns);

    private int AdjacentWalls()
    {
        int walls = 0;
        for (int index = 0; index < CollisionSamples.Length; index++)
        {
            Vector2 point = _precisePosition + CollisionSamples[index];
            if (_terrain.IsSolid(_room, point))
                walls |= 1 << (7 - index);
        }
        return walls;
    }

    private bool TryStartCliffJump()
    {
        if ((_angle & 0xe7) != 0 || CompanionMovement.FacingWallMask(_angle, _adjacentWalls) is not (3 or 0x0c or 0x30)) return false;
        byte tile = _room.GetMetatile(_precisePosition + _terrain.Probes("cliff")[_direction]);
        if (tile == _terrain.VineTop ? _angle != ObjectAngle.Down : !_ledges.IsCliffTile(_room.ActiveCollisions, tile, _angle)) return false;
        _phase = MooshCompanionPhase.CliffJump;
        _speed = 0x50;
        _speedZ = -0x2c0;
        _cliffCounter = 0x14;
        _cliffWalls = 0;
        return true;
    }

    private void UpdateCliffJump()
    {
        if (_cliffCounter > 0)
        {
            if (--_cliffCounter == 0)
            {
                _playSound(SoundId.SndJump);
                SetAnimation(0x09 + _direction);
            }
            return;
        }
        _animation.Advance();
        NativeObjectMovement.ApplySpeed(_runtime, ref _precisePosition, _speed, _angle);
        OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x40);
        int away = CompanionMovement.FacingWallMask((_angle + 0x10) & ObjectAngle.Mask, AdjacentWalls());
        if (away != 0) _cliffWalls = away;
        else if (_cliffWalls != 0)
        {
            // mooshState7 resumes state $05 without resetting the falling arc.
            _phase = MooshCompanionPhase.Riding;
            SetAnimation(0x13 + _direction);
        }
    }

    void ICompanionBarrierTarget.SetBarrierCoordinate(bool horizontal, int coordinate)
    {
        if (horizontal)
            _precisePosition.X = coordinate + _precisePosition.X - Mathf.Floor(_precisePosition.X);
        else
            _precisePosition.Y = coordinate + _precisePosition.Y - Mathf.Floor(_precisePosition.Y);
        _speed = 0; // companionScript_restrict*: writes yh/xh only, then SPEED_0.
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        CompanionRuntimeState.Update(_runtime, CompanionRuntimeState.MooshId, _precisePosition, _direction);
    }

    void ICompanionBarrierTarget.SynchronizeRiderAfterObjects(Player player) =>
        SynchronizePlayer(player, Vector2.Zero);

    private void UpdateDirectionAndAnimation(int animationBase)
    {
        int direction = CompanionMovement.DirectionForAngle(
            _angle, _direction);
        if (direction == _direction)
        {
            _animation.Advance();
            return;
        }

        _direction = direction;
        SetAnimation(animationBase + _direction);
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
            if (_terrain.IsSolid(_room, sample))
                return false;
        }
        return true;
    }

    private void SynchronizePlayer(
        Player player,
        Vector2 screenOffset,
        bool finishMount = false)
    {
        // SPECIALOBJECT_LINK_RIDING_ANIMAL copies animParameter & $3f to
        // w1Link.var31. Charged-stomp terminal parameters use bit 7 as their
        // completion signal while retaining Link frame $2b-$2e.
        int parameter = _animation.CurrentParameter & 0x3f;
        if (parameter < 0 || parameter >= _linkTextures.Length)
        {
            throw new InvalidOperationException(
                $"Moosh animation ${_animation.AnimationIndex:x2} emitted " +
                $"unsupported Link graphics parameter ${parameter:x2}.");
        }
        Texture2D linkTexture = CurrentLinkTexture(parameter);
        if (finishMount)
        {
            player.FinishCompanionMount(
                _precisePosition,
                RidingLinkOffset,
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
                RidingLinkOffset,
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
        if (_runtime.ReadWramByte(0xd104) != 1)
        {
            ZIndex = ObjectDrawPriority.FromVisible(_runtime.ReadWramByte(0xd11a));
            return;
        }
        if (LinkRiding)
        {
            ZIndex = ObjectDrawPriority.BehindLinkZIndex;
            return;
        }
        ZIndex = ObjectDrawPriority.CompanionRelativeToLink(Position.Y, player.Position.Y, _zFixed >> 8);
        _runtime.SetWramByte(0xd11a, (byte)(0xc0 | ObjectDrawPriority.ToVisibleBucket(ZIndex)));
    }

    public void SetScreenTransitionBoundaryCoordinate(
        bool horizontal,
        int coordinate,
        Player player)
    {
        if (!ControlsPlayerScreenTransition)
            throw new InvalidOperationException(
                "An unmounted Moosh cannot own a screen boundary.");
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
            _runtime, CompanionRuntimeState.MooshId, _precisePosition, _direction);
        SynchronizePlayer(player, Vector2.Zero);
    }

    public void BeginScreenTransition(OracleRoomData destination)
    {
        if (!ControlsPlayerScreenTransition || _transitionDestination is not null)
            throw new InvalidOperationException(
                "Moosh received an invalid scrolling handoff.");
        _transitionDestination = destination;
    }

    public void SetScreenTransitionPosition(
        Vector2 position,
        Vector2 screenOffset,
        Player player)
    {
        if (_transitionDestination is null)
            throw new InvalidOperationException(
                "Moosh moved without a transition destination.");
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        SynchronizePlayer(player, screenOffset);
    }

    public void FinishScreenTransition(Vector2 position, Player player)
    {
        OracleRoomData destination = _transitionDestination ??
            throw new InvalidOperationException(
                "Moosh finished scrolling without a destination.");
        _transitionDestination = null;
        _room = destination;
        _tileBreaker = _createTileBreaker(destination);
        _roomId = destination.Id;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        Vector2 respawn = OracleObjectMath.ToPixelPosition(position);
        player.SetLocalRespawnPosition(respawn);
        CompanionRuntimeState.SetLastAnimalMountPosition(_saveData, respawn);
        CompanionRuntimeState.Update(
            _runtime, CompanionRuntimeState.MooshId, position, _direction);
        SynchronizePlayer(player, Vector2.Zero);
    }

    private Texture2D CurrentMooshTexture => _chargePaletteActive
        ? _animation.CurrentTextureForPalette(2)
        : _animation.CurrentTexture;

    private Texture2D CurrentLinkTexture(int parameter) =>
        _chargePaletteActive
            ? _chargeLinkTextures[parameter]
            : _linkTextures[parameter];

}

internal enum MooshCompanionPhase
{
    Waiting,
    Mounting,
    Riding,
    Airborne,
    HoveringOverWater,
    Charging,
    Falling,
    StompRecovery,
    HazardFalling,
    Dismounting,
    AwaitingDistance,
    CliffJump,
    Inactive,
    FluteEntering
}

internal sealed record MooshCompanionSpawn(
    Vector2 Position,
    int Direction,
    int Group,
    int Room,
    bool ForceMount = false,
    bool Riding = false,
    Vector2? FluteDestination = null) : RoomEntitySpawn;

internal sealed record MooshStompAttackSpawn(
    Vector2 Position,
    int Group,
    int Room) : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record MooshHoverExclamationSpawn(
    Vector2 Position,
    int ZFixed) : RoomEntitySpawn;
