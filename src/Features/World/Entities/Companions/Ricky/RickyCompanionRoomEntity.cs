
namespace Oracles;

/// <summary>
/// Shared SPECIALOBJECT_RICKY $0b body. It preserves
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
    ICompanionBarrierTarget, IRoomInitializedCompanion, INativeSpecialObjectRoomEntity
{
    int INativeSpecialObjectRoomEntity.NativeSpecialObjectId => SpecialObjectId.Ricky;
    bool INativeSpecialObjectRoomEntity.SupportsDebugReconstruction => !ScriptedState && _zFixed == 0 &&
        _phase is RickyCompanionPhase.Waiting or RickyCompanionPhase.Riding;
    int INativeSpecialObjectRoomEntity.NativeSpecialObjectDirection => _direction;
    Vector2 INativeSpecialObjectRoomEntity.NativeSpecialObjectPosition
    {
        get => _precisePosition;
        set
        {
            bool entering=_runtime.ReadWramByte(0xd104)==0x0c;
            if(value.X>=248 && (_precisePosition.X<0 || entering&&_direction==1))value.X-=256;
            if(value.Y>=248 && (_precisePosition.Y<0 || entering&&_direction==2))value.Y-=256;
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
        Visible=(_runtime.ReadWramByte(0xd11a)&0x80)!=0;
        ZIndex=LinkRiding?ObjectDrawPriority.BehindLinkZIndex:ObjectDrawPriority.FromVisible(_runtime.ReadWramByte(0xd11a));
    }

    internal void ContinueNativePresentation(Player player, int frame, int counter, bool mounting)
    {
        _zFixed = unchecked((short)(_runtime.ReadWramByte(0xd10e) | _runtime.ReadWramByte(0xd10f) << 8));
        _speedZ = unchecked((short)(_runtime.ReadWramByte(0xd114) | _runtime.ReadWramByte(0xd115) << 8));
        _animation.SetAnimation(_runtime.ReadWramByte(0xd130));
        _animation.RestoreFrame(frame, counter);
        if (!mounting) return;
        _mountStarted = true;
        player.BeginCompanionMount(player.PrecisePosition);
    }

    private bool _fluteEntrance => _runtime.ReadWramByte(0xd104)==0x0c;
    private bool _hazardMounted;
    private bool _flutePending => _fluteEntrance && _runtime.ReadWramByte(0xd103)==0;
    private int _fluteHops {get=>_runtime.ReadWramByte(0xd107);set=>_runtime.SetWramByte(0xd107,(byte)value);}

    // rickyStateC uses two cutscene hops, retaining state-5 landing/hazard rules.
    private bool UpdateFluteEntrance(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if(_runtime.ReadWramByte(0xd103)>1)
            throw new NotSupportedException($"ricky.s: state$0c var03${_runtime.ReadWramByte(0xd103):x2} has no native dispatch.");
        if (_flutePending)
        {
            SpecialObjectOamDatabase.For(AssetRoot).InitializeCompanion(_runtime);
            _runtime.SetWramByte(0xd103,1);_runtime.SetWramByte(0xd124,0x80);_runtime.SetWramByte(0xd11a,0xc1);
            Visible=true;
            _fluteHops = 2;
            StartFluteHop();
            _playSound(SoundId.SndRicky);
            return false;
        }
        switch(_runtime.ReadWramByte(0xd105))
        {
            case 0: UpdateRiding(frame.Player,spawns); break;
            case 1: UpdateHopping(spawns); break;
            case 2: UpdateJumpingOverHole(spawns); break;
            case 3: UpdateLanding(frame.Player,spawns); break;
            default: throw new NotSupportedException($"ricky.s: state$0c state5 substate${_runtime.ReadWramByte(0xd105):x2} has no native dispatch.");
        }
        if(_runtime.ReadWramByte(0xd104)==4)return false;
        _runtime.SetWramByte(0xd104,0x0c);
        if(_runtime.ReadWramByte(0xd105)!=3)return false;
        foreach (var offset in _behavior.LandingProbes)
            spawns.Add(new RickyTileBreakSpawn(_precisePosition + offset,
                BreakableTileDatabase.SourceRickyLanded, _group, _roomId));
        if (_room.GetTerrainInfo(_precisePosition + _behavior.HoleOffsets[_direction]).Collision == 0 &&
            --_fluteHops != 0)
        {
            StartFluteHop();
            return false;
        }
        _runtime.SetWramByte(0xd103,0);
        _initialize(this,frame,spawns);
        return true;
    }

    private void StartFluteHop()
    {
        _speed = _behavior.HopSpeed;
        _speedZ = -0x180;
        _landingCounter = 8;
        _runtime.SetWramByte(0xd105,1);
        SetAnimation(1 + _direction);
    }

    private static readonly Vector2[] CollisionSamples =
    [
        new(-3, -5), new(4, -5), new(-3, 8), new(4, 8),
        new(-5, -3), new(-5, 6), new(6, -3), new(6, 6)
    ];

    private readonly RickyDatabase _data;
    private readonly Action<INativeSpecialObjectRoomEntity,RoomEntityFrame,ICollection<RoomEntitySpawn>> _initialize;
    private readonly Action<RickyCompanionRoomEntity,RoomEntityFrame>? _scriptedState;
    private bool ScriptedState => _runtime.ReadWramByte(0xd104)==0x0a;
    internal EnemyAnimationPlayer NativeAnimation => _animation;
    private readonly CompanionVisualDatabase _visual;
    private readonly RickyCompanionBehaviorRecord _behavior;
    private readonly OracleSaveData _saveData;
    private readonly OracleRuntimeState _runtime;
    private readonly OracleRandom _random;
    private readonly LedgeJumpDatabase _ledgeJumps;
    private readonly Action<int> _playSound;
    private readonly EnemyAnimationPlayer _animation;
    private readonly Texture2D[] _linkTextures;
    private readonly Texture2D[] _chargeLinkTextures;
    private readonly Texture2D[] _damageLinkTextures;
    private readonly Vector2[] _linkTextureOffsets;
    private OracleRoomData _room;
    private OracleRoomData? _transitionDestination;
    private Vector2 _precisePosition;
    private int _group;
    private int _roomId;
    private int _direction {get=>_runtime.ReadWramByte(0xd108);set=>_runtime.SetWramByte(0xd108,checked((byte)value));}
    private int _angle {get=>_runtime.ReadWramByte(0xd109);set=>_runtime.SetWramByte(0xd109,checked((byte)value));}
    private int _zFixed;
    private int _speedZ;
    private int _hopCounter {get=>_runtime.ReadWramByte(0xd139);set=>_runtime.SetWramByte(0xd139,unchecked((byte)value));}
    private int _landingCounter {get=>_runtime.ReadWramByte(0xd106);set=>_runtime.SetWramByte(0xd106,checked((byte)value));}
    private int _airborneDelay {get=>_runtime.ReadWramByte(0xd106);set=>_runtime.SetWramByte(0xd106,checked((byte)value));}
    private int _speed {get=>_runtime.ReadWramByte(0xd110);set=>_runtime.SetWramByte(0xd110,checked((byte)value));}
    private int _wallCrossingMask {get=>_runtime.ReadWramByte(0xd107);set=>_runtime.SetWramByte(0xd107,checked((byte)value));}
    private int _chargeCounter {get=>_runtime.ReadWramByte(0xd135);set=>_runtime.SetWramByte(0xd135,unchecked((byte)value));}
    private bool _mountStarted;
    private int _adjacentWalls {get=>_runtime.ReadWramByte(0xd133);set=>_runtime.SetWramByte(0xd133,checked((byte)value));}
    private bool _attackPressed;
    private bool _attackJustPressed;
    private bool _itemJustPressed;
    private ComponentInputEdge _attackEdge;
    private ComponentInputEdge _itemEdge;
    private bool _chargePaletteActive;
    private bool _screenTransitionsDisabled;
    private bool _finished;
    private CompanionHazard _hazard;
    // Scripts and items dispatch through this same native state/substate union.
    private RickyCompanionPhase _phase
    {
        get => _runtime.ReadWramByte(0xd104) switch
        {
            0 or 0x0b => RickyCompanionPhase.Initializing,
            1 => RickyCompanionPhase.Waiting,
            2 => RickyCompanionPhase.JumpingUpCliff,
            3 => RickyCompanionPhase.Mounting,
            4 => RickyCompanionPhase.HazardFalling,
            5 => _runtime.ReadWramByte(0xd105) switch
            {
                0 => RickyCompanionPhase.Riding, 1 => RickyCompanionPhase.Hopping,
                2 => RickyCompanionPhase.JumpingOverHole, 3 => RickyCompanionPhase.Landing,
                var sub => throw new NotSupportedException($"ricky.s: state$05 substate${sub:x2} has no native dispatch.")
            },
            6 => _runtime.ReadWramByte(0xd105) switch
            {
                0 or 1 => RickyCompanionPhase.Dismounting,
                2 => RickyCompanionPhase.AwaitingDistance,
                var sub => throw new NotSupportedException($"ricky.s: state$06 substate${sub:x2} has no native dispatch.")
            },
            7 => RickyCompanionPhase.JumpingDownCliff,
            8 => _runtime.ReadWramByte(0xd105) switch
            {
                0 => RickyCompanionPhase.Punching, 1 => RickyCompanionPhase.Charging,
                var sub => throw new NotSupportedException($"ricky.s: state$08 substate${sub:x2} has no native dispatch.")
            },
            9 => RickyCompanionPhase.Inactive,
            0x0a => RickyCompanionPhase.Scripted,
            0x0c => RickyCompanionPhase.FluteEntering,
            var state => throw new NotSupportedException($"ricky.s: {_runtime.Game} state${state:x2} has no native dispatch.")
        };
        set
        {
            byte state = value switch
            {
                RickyCompanionPhase.Waiting => 1, RickyCompanionPhase.JumpingUpCliff => 2,
                RickyCompanionPhase.Mounting => 3, RickyCompanionPhase.HazardFalling => 4,
                RickyCompanionPhase.Riding or RickyCompanionPhase.Hopping or
                    RickyCompanionPhase.JumpingOverHole or RickyCompanionPhase.Landing => 5,
                RickyCompanionPhase.Dismounting or RickyCompanionPhase.AwaitingDistance => 6,
                RickyCompanionPhase.JumpingDownCliff => 7,
                RickyCompanionPhase.Punching or RickyCompanionPhase.Charging => 8,
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
            _runtime.SetWramByte(0xd104,state);
            if(state is 2 or 4 or 5 or 6 or 7 or 8)
                _runtime.SetWramByte(0xd105,value switch
                {
                    RickyCompanionPhase.Hopping or RickyCompanionPhase.Charging => 1,
                    RickyCompanionPhase.JumpingOverHole or RickyCompanionPhase.AwaitingDistance => 2,
                    RickyCompanionPhase.Landing => 3, _ => 0
                });
        }
    }

    public Node2D Node => this;
    public bool DisablesSword => LinkRiding || ScriptedState || _phase==RickyCompanionPhase.Mounting;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => ScriptedState;
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
        RickyCompanionPhase.Punching or
        RickyCompanionPhase.Charging ||
        (_phase == RickyCompanionPhase.HazardFalling && _hazardMounted) ||
        (_phase == RickyCompanionPhase.Dismounting &&
            _runtime.ReadWramByte(0xd105)==0);
    public bool ControlsPlayerScreenTransition => LinkRiding;
    public bool BypassesScreenTransitionInputGate => false;
    public Vector2 ScreenTransitionPosition => _precisePosition;
    int ICompanionBarrierTarget.CompanionId => CompanionRuntimeState.RickyId;
    bool ICompanionBarrierTarget.BarrierMounted => LinkRiding;
    Vector2 ICompanionBarrierTarget.BarrierPosition => _precisePosition;

    internal RickyCompanionPhase Phase => ScriptedState?RickyCompanionPhase.Scripted:_phase;
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
    internal bool ChargeLinkTextureSelected =>
        ReferenceEquals(
            CurrentLinkTexture(LinkAnimationParameter),
            _chargeLinkTextures[LinkAnimationParameter]);

    internal RickyCompanionRoomEntity(
        RickyCompanionSpawn spawn,
        OracleRoomData room,
        RickyDatabase database,
        OracleSaveData? saveData,
        OracleRuntimeState runtime,
        OracleRandom random,
        LedgeJumpDatabase ledgeJumps,
        Action<int> playSound,
        Action<INativeSpecialObjectRoomEntity,RoomEntityFrame,ICollection<RoomEntitySpawn>> initialize,
        Action<RickyCompanionRoomEntity,RoomEntityFrame>? scriptedState = null)
    {
        AssetRoot = ledgeJumps.AssetRoot;
        _data = database; _scriptedState = scriptedState;
        _initialize=initialize;
        _visual = database.Visual;
        _behavior = database.Behavior;
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        _runtime = runtime;
        _random = random;
        _ledgeJumps = ledgeJumps;
        _playSound = playSound;
        _room = room;
        _group = spawn.Group;
        _roomId = spawn.Room;
        _precisePosition = spawn.Position;
        if (spawn.FluteDestination is Vector2 destination)
        {
            CompanionRuntimeState.SetLastAnimalMountPosition(_saveData, destination);
        }

        _animation = new EnemyAnimationPlayer(this, _visual.Animations.Length);
        _animation.Load(
            OracleGraphicsCache.LoadImage(
                AssetRoot + $"gfx/{_visual.Sprite}.png"),
            _visual.Animations,
            _visual.TileBase,
            _visual.Palette,
            positionedOam: true,
            paletteVariants: [2],
            animationSourceOffsets: _visual.SourceOffsets);
        (_linkTextures, _chargeLinkTextures, _damageLinkTextures,
            _linkTextureOffsets) =
            CompanionLinkFrames.Load(AssetRoot,
                _visual.LinkSprite, _visual.LinkPalette,
                _visual.LinkFrames, _visual.LinkSourceOffsets);

        int initialAnimation = spawn.ForceMount
            ? 0
            : spawn.Riding
                ? _behavior.IdleAnimation + _direction
                : 0x17;
        SetAnimation(initialAnimation);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        Name = $"Ricky_{_group:x1}_{_roomId:x2}";
        ZIndex = LinkRiding
            ? ObjectDrawPriority.BehindLinkZIndex
            : Player.NormalZIndex;
        Visible = !_flutePending;
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
        if (RoomInitialization is { Pending: true } initialization)
        {
            if (initialization.Advance(ref _precisePosition))
            {
                _finished = initialization.Rejected;
                if (!_finished)
                {
                    _angle = 2;
                    _direction = 2;
                    CompanionRuntimeState.Update(_runtime, SpecialObjectId.Ricky, _precisePosition, _direction);
                }
                Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                _animation.SetAnimation(_animation.AnimationIndex);
                if (!_finished)
                {
                    // rickyState0 installs animation/var39 before objectSetVisiblec1.
                    _runtime.SetWramByte(0xd139, 0x10);
                    _runtime.SetWramByte(0xd120, (byte)_animation.RemainingUpdates);
                    _runtime.SetWramByte(0xd121, (byte)_animation.CurrentParameter);
                    _runtime.SetWramByte(0xd130, (byte)_animation.AnimationIndex);
                    _runtime.SetWramByte(0xd11a, 0xc1);
                }
                Visible = !_finished;
            }
            return;
        }
        _ = spawns;
        _attackPressed = Input.IsActionPressed("attack");
        _attackJustPressed = _attackEdge.Read(Input.IsActionJustPressed("attack"));
        _itemJustPressed = _itemEdge.Read(Input.IsActionJustPressed("item"));
        _chargePaletteActive = false;

        if(ScriptedState)
        {
            if(_scriptedState is null)throw new NotSupportedException($"ricky.s: {_runtime.Game} state$0a var03${_runtime.ReadWramByte(0xd103):x2} requires its native content binding.");
            _scriptedState(this,frame);
            if(CompanionRuntimeState.IsActive(_runtime,SpecialObjectId.Ricky))
            {
                _zFixed=unchecked((short)(_runtime.ReadWramByte(0xd10e)|_runtime.ReadWramByte(0xd10f)<<8));
                _speedZ=unchecked((short)(_runtime.ReadWramByte(0xd114)|_runtime.ReadWramByte(0xd115)<<8));
            }
            else{_finished=true;Visible=false;}
        }
        else if (_fluteEntrance) {if(UpdateFluteEntrance(frame,spawns))return;}
        else switch (_phase)
        {
            case RickyCompanionPhase.Initializing:
                _initialize(this,frame,spawns);
                return;
            case RickyCompanionPhase.Inactive:
                break;
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
            default:
                throw new InvalidOperationException(
                    $"Unsupported Ricky phase {_phase}.");
        }

        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (LinkRiding || CompanionRuntimeState.IsActive(_runtime, CompanionRuntimeState.RickyId))
        {
            CompanionRuntimeState.Update(
                _runtime,
                CompanionRuntimeState.RickyId,
                _precisePosition,
                _direction);
            _runtime.SetWramByte(0xd10e, unchecked((byte)_zFixed));
            _runtime.SetWramByte(0xd10f, unchecked((byte)(_zFixed >> 8)));
            _runtime.SetWramByte(0xd114, unchecked((byte)_speedZ));
            _runtime.SetWramByte(0xd115, unchecked((byte)(_speedZ >> 8)));
            CompanionNativeAnimationDatabase.For(AssetRoot).PublishAnimation(_runtime,SpecialObjectId.Ricky,
                _animation.AnimationIndex,_animation.FrameIndex,_animation.RemainingUpdates,_animation.CurrentParameter);
        }
        if (LinkRiding)
        {
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
        int priority=ObjectDrawPriority.CompanionRelativeToLink(_precisePosition.Y,player.Position.Y,_zFixed>>8);
        _runtime.SetWramByte(0xd11a,(byte)(0xc0|ObjectDrawPriority.ToVisibleBucket(priority)));
        if (!CompanionRuntimeState.MountingDisabled(_runtime) && player.CanMountCompanion &&
            LinkWithinMountDistance(player.PrecisePosition))
        {
            _phase = RickyCompanionPhase.Mounting;
            player.BeginCompanionMount(player.PrecisePosition);
            _mountStarted = true;
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
            _speedZ = _data.IdleHopSpeedZ;
        }
        else if (signal == 0x40)
        {
            OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, _data.IdleGravity);
            return;
        }
        TryBeginHazard();
    }

    internal void StopAtCarpenterSearchBoundary()
    {
        // carpenter.s:@ricky writes state-$05 substate $03.
        if (_phase != RickyCompanionPhase.Hopping) return;
        _phase = RickyCompanionPhase.Landing;
    }

    private void UpdateMounting(Player player)
    {
        // rickyState3 continues objectUpdateSpeedZ_paramC while Link performs
        // the mounting jump, including when mounting began during Ricky's
        // parameter-driven idle hop.
        OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _data.IdleGravity);
        // companionCheckMountingComplete cancels before nudging Link.
        if (CompanionRuntimeState.MountingDisabled(_runtime) || player.CompanionMountInterrupted)
        {
            _phase = RickyCompanionPhase.Waiting;
            _mountStarted = false;
            _screenTransitionsDisabled = false;
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

        _runtime.SetWramByte(0xd104,5); // companionFinalizeMounting retains substate.
        _angle = 0xff;
        _screenTransitionsDisabled = false;
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        SetAnimation(_behavior.IdleAnimation + _direction);
        CompanionRuntimeState.BeginAnimalRide(
            _runtime, _saveData,
            CompanionRuntimeState.RickyId,
            _precisePosition,
            _direction);
        _playSound(SoundId.SndRicky);
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
            return;
        }

        Vector2 input = Input.GetVector(
            "move_left", "move_right", "move_up", "move_down");
        int angle = CompanionMovement.AngleForInput(input);
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
        _speed = _behavior.GroundSpeed;
        SetDirectionAnimation(_behavior.IdleAnimation, animate: true);
        int walls = _adjacentWalls;
        if (TryStartHoleJump() ||
            TryStartJumpDownCliff(walls, periodic: false) ||
            TryStartJumpUpCliff(walls))
        {
            return;
        }

        ApplyCompanionMovement(_speed);
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
        int walls = _adjacentWalls;
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
        if (TryStartHoleJump(periodic: true))
            return;

        _phase = RickyCompanionPhase.Hopping;
        _speed = _behavior.HopSpeed;
        _speedZ = _behavior.HopSpeedZ;
        _landingCounter = _behavior.LandingDelay;
        SetAnimation(_behavior.HopAnimation + _direction);
        _playSound((_random.Next().Value & 0x0f) == 0
            ? SoundId.SndRicky
            : _behavior.JumpSound);
    }

    private void UpdateHopping(ICollection<RoomEntitySpawn> spawns)
    {
        bool riddenState=_runtime.ReadWramByte(0xd104)==5;
        if (riddenState && _attackJustPressed)
        {
            StartPunch(spawns);
            return;
        }

        Vector2 input = Input.GetVector(
            "move_left", "move_right", "move_up", "move_down");
        int angle = CompanionMovement.AngleForInput(input);
        if (riddenState && angle != 0xff)
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
            bool diagonal = (_runtime.ReadField("wLinkObjectIndex")&1)!=0 && (_angle & 0x04) != 0;
            if (diagonal || !IsHoleAt(_precisePosition +
                    _behavior.HoleOffsets[_direction]))
            {
                Vector2 before=_precisePosition;
                ApplyCompanionMovement(_speed);
                if(_fluteEntrance)
                {
                    if(before.X<0 && _precisePosition.X>128)_precisePosition.X-=256;
                    if(before.Y<0 && _precisePosition.Y>128)_precisePosition.Y-=256;
                }
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
        _speed = _behavior.LongJumpSpeed;
        _screenTransitionsDisabled = disableScreenTransitions;
        SetAnimation(_behavior.LongJumpAnimation + _direction);
    }

    private bool TryStartHoleJump(bool periodic = false)
    {
        if ((_angle & 0x04) != 0)
            return false;
        Vector2 offset = _behavior.HoleOffsets[_direction];
        if (!IsHoleAt(_precisePosition + offset))
            return false;

        if (_phase == RickyCompanionPhase.Riding)
            StartLongJumpSetup(disableScreenTransitions: true);
        // The walking branch only prepares the arc. State $05:$02 is
        // selected by the zero-hop-counter branch, after var39 expires.
        if (periodic) _phase = RickyCompanionPhase.JumpingOverHole;
        _screenTransitionsDisabled = true;
        return true;
    }

    private bool TryStartJumpUpCliff(int walls)
    {
        if ((walls & 0xc0) != 0xc0 || _angle != ObjectAngle.Up)
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
        int towardWall = CompanionMovement.FacingWallMask(_angle, walls);
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
        _speed = periodic
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
        ApplyRawMovement(_speed);

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
        ApplyCompanionMovement(_speed);
        int walls = CalculateAdjacentWallsBitset();
        if (CompanionMovement.FacingWallMask(_angle, walls) != 0)
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
        ApplyRawMovement(_speed);
        OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _behavior.HopGravity);
        int walls = CalculateAdjacentWallsBitset();
        int movingAway = CompanionMovement.FacingWallMask((_angle + 0x10) & ObjectAngle.Mask, walls);
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
        _playSound(SoundId.SndRicky);
        return false;
    }

    private void StopUntilLanded(ICollection<RoomEntitySpawn> spawns)
    {
        _screenTransitionsDisabled = false;
        _phase = RickyCompanionPhase.Landing;
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
                ApplyCompanionMovement(_speed);
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
            // rickyStopUntilLandedOnGround selects state $05:$03. The next
            // update performs landing probes before accepting new input.
            StopUntilLanded(spawns);
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
        int angle = CompanionMovement.AngleForInput(input);
        if (angle != 0xff && angle != _angle)
        {
            _angle = angle;
            SetDirectionAnimation(_behavior.ChargeAnimation, animate: true);
        }
        // rickyState8 advances once unconditionally, in addition to the
        // direction helper on an exact angle change.
        _animation.Advance();

        if (!_attackPressed)
        {
            if (_chargeCounter == _behavior.ChargeUpdates)
            {
                spawns.Add(new RickyTornadoSpawn(
                    _precisePosition,
                    _direction,
                    _zFixed,
                    _group,
                    _roomId));
                _playSound(SoundId.SndCtrlStopSfx);
                _playSound(_behavior.SwordSpinSound);
                StartPunch(spawns);
            }
            else
            {
                ReturnToRiding(_behavior.CancelAnimation);
            }
            return;
        }

        if (_chargeCounter != _behavior.ChargeUpdates)
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
        SetAnimation(animationBase + _direction);
    }

    private void UpdateDismounting(Player player)
    {
        if (_runtime.ReadWramByte(0xd105)==0)
        {
            if(!OracleObjectMath.UpdateSpeedZ(ref _zFixed,ref _speedZ,0x40))return;
            _runtime.SetWramByte(0xd105,1);
            _screenTransitionsDisabled = false;
            CompanionRuntimeState.DismountAnimal(
                _runtime,
                CompanionRuntimeState.RickyId);
            player.BeginCompanionDismount(_precisePosition, _direction);
            CompanionRuntimeState.RememberAfterDismount(_runtime,player.Inventory,SpecialObjectId.Ricky,_group,_roomId,_precisePosition);
            SetAnimation(0x17);
            return;
        }
        if (player.CompanionJumpActive) return;

        _phase = RickyCompanionPhase.AwaitingDistance;
    }

    private void UpdateAwaitingDistance(Player player)
    {
        // State $06 substate $02 deliberately does not call
        // specialObjectAnimate. The $17 idle clock resumes only after Link
        // has moved far enough away and Ricky returns to state $01.
        if (LinkWithinMountDistance(player.PrecisePosition))
        {
            TryBeginHazard();
            return;
        }
        _phase = RickyCompanionPhase.Waiting;
        _runtime.SetWramByte(0xd105,0);
    }


    void ICompanionBarrierTarget.SetBarrierCoordinate(bool horizontal, int coordinate)
    {
        if (horizontal)
            _precisePosition.X = coordinate + _precisePosition.X - Mathf.Floor(_precisePosition.X);
        else
            _precisePosition.Y = coordinate + _precisePosition.Y - Mathf.Floor(_precisePosition.Y);
        _speed = 0; // companionScript_restrict*: writes yh/xh only, then SPEED_0.
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        CompanionRuntimeState.Update(_runtime, CompanionRuntimeState.RickyId, _precisePosition, _direction);
    }

    void ICompanionBarrierTarget.SynchronizeRiderAfterObjects(Player player) =>
        SynchronizePlayer(player, Vector2.Zero);

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
        int direction = CompanionMovement.DirectionForAngle(
            _angle, _direction);
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

    internal void ApplyRawMovement(int speed)
    {
        NativeObjectMovement.ApplySpeed(_runtime,
            ref _precisePosition, speed, _angle);
    }

    internal void ApplyCompanionMovement(int speed)
    {
        if (_angle == 0xff)
            return;
        int walls = CalculateAdjacentWallsBitset();
        ApplyCompanionMovement(speed, walls);
    }

    private void ApplyCompanionMovement(int speed, int walls) =>
        SpecialObjectMovement.ApplySpeed(_runtime, ref _precisePosition, speed, _angle, _adjacentWalls = walls);

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
        _runtime.SetWramByte(0xd133,(byte)walls);
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
        // TILEINDEX_VINE_TOP branches to checkGivenCollision_allowHoles with
        // A=$03. All other Ricky probes use the companion-specific
        // checkCollisionPosition_disallowSmallBridges path.
        byte collision = tile == _behavior.VineTopTile
            ? (byte)0x03
            : _room.GetTerrainInfo(point).Collision;
        int inTileX = Mathf.FloorToInt(point.X) & 0x0f;
        int inTileY = Mathf.FloorToInt(point.Y) & 0x0f;
        if (collision < 0x10)
        {
            int quadrantBit = (inTileY < 8 ? 2 : 0) +
                (inTileX < 8 ? 1 : 0);
            return (collision & (1 << quadrantBit)) != 0;
        }

        int collisionKind = collision & 0x0f;
        int axisPosition = collisionKind < 8 ? inTileX : inTileY;
        return (_behavior.CompanionCollisionMasks[collisionKind] &
            (1 << (axisPosition >> 1))) != 0;
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
        if (!CompanionHazard.TryCreate(
                _room, _precisePosition, out _hazard))
            return false;
        _screenTransitionsDisabled = true;
        _hazardMounted = LinkRiding;
        _phase = RickyCompanionPhase.HazardFalling;
        _runtime.SetWramByte(0xd106,0);
        _runtime.SetWramByte(0xd137,_hazard.Type==HazardType.Water?(byte)0x0e:(byte)0x0d);
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
                _runtime,1,_runtime.ReadWramByte(0xd137)==0x0e,
                _behavior.WaterAnimation,
                _behavior.HoleAnimation,
                SetAnimation,
                _playSound))
        {
            return;
        }

        HazardType completedHazard = _hazard.Type;
        int respawnAngle=_angle;
        Vector2 respawn = CompanionHazard.ResolveRespawn(
            player, _saveData, _runtime, ref respawnAngle, CanRespawnAt,_room,_precisePosition);
        _angle=respawnAngle;
        _precisePosition = respawn;
        _hazard = default;
        _screenTransitionsDisabled = false;
        _phase = _hazardMounted ? RickyCompanionPhase.Riding : RickyCompanionPhase.Waiting;
        _direction = (_angle >> 3) & 3;
        _hopCounter = _behavior.HopDelay;
        if (_hazardMounted) player.ApplyCompanionHazardDamage(completedHazard);
        SetAnimation((_hazardMounted ? _behavior.CancelAnimation : 1) + _direction);
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
            ZIndex = ObjectDrawPriority.BehindLinkZIndex;
            return;
        }
        ZIndex = ObjectDrawPriority.CompanionRelativeToLink(Position.Y, player.Position.Y, _zFixed >> 8);
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
        CompanionRuntimeState.SetLastAnimalMountPosition(_saveData, respawn);
        CompanionRuntimeState.Update(
            _runtime,
            CompanionRuntimeState.RickyId,
            position,
            _direction);
        SynchronizePlayer(player, Vector2.Zero);
    }

    private Texture2D CurrentRickyTexture => _chargePaletteActive
        ? _animation.CurrentTextureForPalette(2)
        : _animation.CurrentTexture;

    private Texture2D CurrentLinkTexture(int parameter) =>
        _chargePaletteActive
            ? _chargeLinkTextures[parameter]
            : _linkTextures[parameter];

}

internal enum RickyCompanionPhase
{
    Initializing,
    Inactive,
    FluteEntering,
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
    Scripted
}

internal sealed record RickyCompanionSpawn(
    Vector2 Position,
    int Direction,
    int Group,
    int Room,
    bool ForceMount = false,
    bool Riding = false,
    Vector2? FluteDestination = null) : RoomEntitySpawn;
