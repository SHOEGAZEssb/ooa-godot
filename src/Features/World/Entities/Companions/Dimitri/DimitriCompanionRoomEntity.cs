
namespace Oracles;

/// <summary>SPECIALOBJECT_DIMITRI $0c and its native companion script handoff.</summary>
internal sealed partial class DimitriCompanionRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IPlayerRestriction, IPlayerForcedMovement,
    IPlayerRideableRoomEntity, IPlayerScreenTransitionRoomEntity,
    IRoomEntityLifetime, ICompanionBarrierTarget,
    IBraceletInteractableRoomEntity, IBraceletChildRoomEntity, IRoomInitializedCompanion, INativeSpecialObjectRoomEntity
{
    int INativeSpecialObjectRoomEntity.NativeSpecialObjectId => SpecialObjectId.Dimitri;
    bool INativeSpecialObjectRoomEntity.SupportsDebugReconstruction => _carried.ZFixed == 0 &&
        _runtime.ReadWramByte(0xd104) is 1 or 5;
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
        _carried.ZFixed=unchecked((short)(_runtime.ReadWramByte(0xd10e)|_runtime.ReadWramByte(0xd10f)<<8));
        _carried.SpeedZ=unchecked((short)(_runtime.ReadWramByte(0xd114)|_runtime.ReadWramByte(0xd115)<<8));
        _adjacentWalls=_runtime.ReadWramByte(0xd133);
        Visible=(_runtime.ReadWramByte(0xd11a)&0x80)!=0;
        ZIndex=LinkRiding?ObjectDrawPriority.BehindLinkZIndex:ObjectDrawPriority.FromVisible(_runtime.ReadWramByte(0xd11a));
    }
    internal void ContinueNativePresentation(Player player, int frame, int counter, bool mounting)
    {
        _direction = _runtime.ReadWramByte(0xd108); _angle = _runtime.ReadWramByte(0xd109);
        _water = _runtime.ReadWramByte(0xd138);
        _carried.ZFixed = unchecked((short)(_runtime.ReadWramByte(0xd10e) | _runtime.ReadWramByte(0xd10f) << 8));
        _carried.SpeedZ = unchecked((short)(_runtime.ReadWramByte(0xd114) | _runtime.ReadWramByte(0xd115) << 8));
        _animation.SetAnimation(_runtime.ReadWramByte(0xd130)); _animation.RestoreFrame(frame, counter);
        _phase = mounting ? DimitriPhase.Mounting : DimitriPhase.Waiting;
        if (!mounting) return;
        _mountStarted = true;
        player.BeginCompanionMount(player.PrecisePosition);
    }
    private readonly DimitriDatabase _data;
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _sound;
    private readonly Action<INativeSpecialObjectRoomEntity,RoomEntityFrame,ICollection<RoomEntitySpawn>> _initialize;
    private readonly Action<DimitriCompanionRoomEntity,RoomEntityFrame,ICollection<RoomEntitySpawn>>? _scriptedState;
    private readonly EnemyAnimationPlayer _animation;
    private CompanionAttackTileBreaker _tileBreaker;
    private readonly Func<OracleRoomData, CompanionAttackTileBreaker> _createTileBreaker;
    private OracleRoomData _room;
    private OracleRoomData? _destination;
    private Vector2 _precisePosition;
    private readonly int _group;
    private int _direction {get=>_runtime.ReadWramByte(0xd108);set=>_runtime.SetWramByte(0xd108,checked((byte)value));}
    private int _angle {get=>_runtime.ReadWramByte(0xd109);set=>_runtime.SetWramByte(0xd109,checked((byte)value));}
    private int _water
    {
        get=>_runtime.ReadWramByte(0xd138);
        set=>_runtime.SetWramByte(0xd138,checked((byte)value));
    }
    private int _speed {get=>_runtime.ReadWramByte(0xd110);set=>_runtime.SetWramByte(0xd110,checked((byte)value));}
    private int _counter
    {
        get=>_runtime.ReadWramByte(_runtime.ReadWramByte(0xd104)==0x0c?0xd107:0xd106);
        set=>_runtime.SetWramByte(_runtime.ReadWramByte(0xd104)==0x0c?0xd107:0xd106,checked((byte)value));
    }
    private int _bitePhase {get=>_runtime.ReadWramByte(0xd105);set=>_runtime.SetWramByte(0xd105,checked((byte)value));}
    private bool _swallowed {get=>_runtime.ReadWramByte(0xd135)!=0;set=>_runtime.SetWramByte(0xd135,value?(byte)1:(byte)0);}
    private bool _mountStarted;
    private ComponentInputEdge _attackEdge;
    private ComponentInputEdge _itemEdge;
    private CompanionHazard _hazard;
    private bool _hazardMounted;
    private bool _retired;
    // Typed phases read the source union, including state writes made by scripts/items.
    private DimitriPhase _phase
    {
        get=>_retired?DimitriPhase.Finished:_runtime.ReadWramByte(0xd104) switch
        {
            0 or 1=>DimitriPhase.Waiting,
            2=>_runtime.ReadWramByte(0xd105) switch
                {0 or 1=>DimitriPhase.Carried,2=>DimitriPhase.Thrown,3=>DimitriPhase.ThrownLanding,
                var sub=>throw new NotSupportedException($"dimitri.s: state$02 substate${sub:x2}.")},
            3=>DimitriPhase.Mounting,4=>DimitriPhase.Hazard,5=>DimitriPhase.Riding,
            6=>_runtime.ReadWramByte(0xd105) switch
                {0 or 1=>DimitriPhase.Dismounting,2=>DimitriPhase.AwaitingDistance,
                var sub=>throw new NotSupportedException($"dimitri.s: state$06 substate${sub:x2}.")},
            7=>DimitriPhase.CliffJump,8=>DimitriPhase.Eating,9=>DimitriPhase.Inactive,
            0x0a=>DimitriPhase.Scripted,
            0x0b=>DimitriPhase.ReturningToLand,
            0x0c=>_runtime.ReadWramByte(0xd103) switch
                {0=>DimitriPhase.FlutePending,1=>DimitriPhase.FluteEntering,
                var parameter=>throw new NotSupportedException($"dimitri.s: state$0c parameter${parameter:x2}.")},
            0x0d=>DimitriPhase.Carpenter,
            var state=>throw new NotSupportedException($"dimitri.s: {_runtime.Game} ordinary body state${state:x2} has no native dispatch.")
        };
        set
        {
            if(value==DimitriPhase.Finished){_retired=true;return;}
            byte state=value switch
            {
                DimitriPhase.Waiting=>1,DimitriPhase.Carried or DimitriPhase.Thrown or DimitriPhase.ThrownLanding=>2,
                DimitriPhase.Mounting=>3,DimitriPhase.Hazard=>4,DimitriPhase.Riding=>5,
                DimitriPhase.Dismounting or DimitriPhase.AwaitingDistance=>6,DimitriPhase.CliffJump=>7,
                DimitriPhase.Eating=>8,DimitriPhase.Inactive=>9,
                DimitriPhase.ReturningToLand=>0x0b,DimitriPhase.FlutePending or DimitriPhase.FluteEntering=>0x0c,
                DimitriPhase.Carpenter=>0x0d,_=>throw new ArgumentOutOfRangeException(nameof(value))
            };
            _runtime.SetWramByte(0xd104,state);
            if(value is DimitriPhase.AwaitingDistance or DimitriPhase.Thrown)_runtime.SetWramByte(0xd105,2);
            if(value==DimitriPhase.ThrownLanding)_runtime.SetWramByte(0xd105,3);
            if(value==DimitriPhase.FluteEntering)_runtime.SetWramByte(0xd103,1);
        }
    }
    private CompanionTerrainDatabase? _nativeSourceCache;
    private CompanionTerrainDatabase _native => _nativeSourceCache ??= new(AssetRoot);
    private BraceletDatabaseRecord? _braceletSourceCache;
    private BraceletDatabaseRecord _bracelet => _braceletSourceCache ??= DimitriBracelet();
    private BraceletDatabaseRecord DimitriBracelet()
    {
        var weight = BraceletWeightDatabase.For(AssetRoot).Weight(4);
        return new BraceletDatabase(AssetRoot).Data with { Gravity = weight.Gravity, InitialSpeedZ = weight.InitialSpeedZ,
            SpeedRaw = weight.SpeedRaw, TossSpeedRaw = weight.TossSpeedRaw };
    }
    private BombRecord? _throwingSourceCache;
    private BombRecord _throwing => _throwingSourceCache ??= new BombDatabase(AssetRoot).Data;
    private LedgeJumpDatabase? _ledgesSourceCache;
    private LedgeJumpDatabase _ledges => _ledgesSourceCache ??= new(AssetRoot);
    private CarriedObjectMotion _carried;
    private CarriedObjectMotion _braceletChildMotion;
    private bool _braceletChildStarting;
    private Vector2 _throwOrigin;
    private int _cliffWalls {get=>_runtime.ReadWramByte(0xd107);set=>_runtime.SetWramByte(0xd107,checked((byte)value));}
    private int _adjacentWalls;
    private Player? _holder;
    public Node2D Node => this;
    public bool Finished => _retired;
    // Construction may retain an opaque var03. Decode its dispatch only on an eligible visit.
    public bool LinkRiding => _runtime.ReadWramByte(0xd104) is 5 or 7 or 8 ||
        _runtime.ReadWramByte(0xd104)==0x0d && _runtime.ReadWramByte(0xd13c)!=0 ||
        _runtime.ReadWramByte(0xd104) is 4 or 6 or 9 or 0x0a && _runtime.ReadField("wLinkObjectIndex")==0xd1;
    public bool DisablesSword => LinkRiding || _phase == DimitriPhase.Mounting;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => false;
    public bool DisablesScreenTransitions => (_phase == DimitriPhase.Hazard && _hazardMounted) ||
        _phase is DimitriPhase.Mounting or DimitriPhase.Eating or DimitriPhase.CliffJump;
    public bool ControlsPlayerScreenTransition => LinkRiding || _phase == DimitriPhase.Carried;
    public bool BypassesScreenTransitionInputGate => false;
    public Vector2 ScreenTransitionPosition => _holder?.PrecisePosition ?? _precisePosition;
    int ICompanionBarrierTarget.CompanionId => CompanionRuntimeState.DimitriId;
    bool ICompanionBarrierTarget.BarrierMounted => LinkRiding;
    Vector2 ICompanionBarrierTarget.BarrierPosition => _precisePosition;
    internal DimitriPhase Phase => _phase;
    internal int Direction => _direction;
    internal bool InWater => _water != 0;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int Counter => _counter;
    internal Vector2 PrecisePosition => _precisePosition;
    internal int ZFixed => _carried.ZFixed;

    internal DimitriCompanionRoomEntity(DimitriCompanionSpawn spawn, OracleRoomData room,
        DimitriDatabase data, OracleSaveData save, OracleRuntimeState runtime,
        Action<int> sound,
        Func<OracleRoomData, CompanionAttackTileBreaker> createTileBreaker,
        Action<INativeSpecialObjectRoomEntity,RoomEntityFrame,ICollection<RoomEntitySpawn>> initialize,
        Action<DimitriCompanionRoomEntity,RoomEntityFrame,ICollection<RoomEntitySpawn>>? scriptedState = null)
    {
        AssetRoot = data.AssetRoot;
        _data = data; _save = save; _runtime = runtime; _sound = sound;_initialize=initialize;_scriptedState=scriptedState;
        _room = room;
        _createTileBreaker = createTileBreaker;
        _tileBreaker = createTileBreaker(room);
        _group = spawn.Group; _precisePosition = spawn.Position;
        if (spawn.FluteDestination is Vector2 destination)
        {
            CompanionRuntimeState.SetLastAnimalMountPosition(_save, destination);
        }
        _animation = new EnemyAnimationPlayer(this, data.Visual.Animations.Length);
        _animation.Load(OracleGraphicsCache.LoadImage(AssetRoot + "gfx/spr_dimitri.png"),
            data.Visual.Animations, 0, 2, positionedOam: true, animationSourceOffsets: data.Visual.SourceOffsets);
        int initialWater = IsWater(_precisePosition) ? 4 : 0;
        _animation.SetAnimation((LinkRiding ? 0 : 0x1c) + initialWater + _direction);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        Name = $"Dimitri_{_group:x}_{room.Id:x2}";
        Visible=(_runtime.ReadWramByte(0xd11a)&0x80)!=0;
    }

    internal void StopAtCarpenterSearchBoundary()
    {
        // carpenter.s:@dimitri redirects biting state $08 through state $0d.
        if (_phase != DimitriPhase.Eating) return;
        _phase = DimitriPhase.Carpenter;
    }

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (_phase == DimitriPhase.Mounting && _mountStarted)
            player.NudgeCompanionMountToward(_precisePosition);
        else if (LinkRiding) SynchronizePlayer(player);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (RoomInitialization is { Pending: true } initialization)
        {
            if (initialization.Advance(ref _precisePosition))
            {
                if (initialization.Rejected) _phase = DimitriPhase.Finished;
                _angle = 2;
                _direction = 2;
                if (!initialization.Rejected)
                {
                    CompanionRuntimeState.Update(_runtime, SpecialObjectId.Dimitri, _precisePosition, _direction);
                    _runtime.SetWramByte(0xd109, 2);
                }
                Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                SetAnimation(0x1c);
                Visible = !initialization.Rejected;
            }
            return;
        }
        Player player = frame.Player;
        bool attack = Input.IsActionPressed("attack") && Input.IsActionJustPressed("attack");
        bool item = Input.IsActionPressed("item") && Input.IsActionJustPressed("item");
        bool attackPressed = _attackEdge.Read(attack);
        bool itemPressed = _itemEdge.Read(item);
        switch (_phase)
        {
            case DimitriPhase.Scripted:
                if(_scriptedState is null)
                    throw new NotSupportedException($"dimitri.s: {_runtime.Game} state$0a var03${_runtime.ReadWramByte(0xd103):x2} requires its native content binding.");
                _scriptedState(this,frame,spawns);
                if(!CompanionRuntimeState.IsActive(_runtime,SpecialObjectId.Dimitri))_retired=true;
                break;
            case DimitriPhase.Waiting:
                _runtime.SetWramByte(0xd11a,(byte)(0xc0|ObjectDrawPriority.ToVisibleBucket(
                    ObjectDrawPriority.CompanionRelativeToLink(_precisePosition.Y,player.Position.Y,_carried.ZFixed>>8))));
                if (!OracleObjectMath.UpdateSpeedZ(ref _carried.ZFixed, ref _carried.SpeedZ, 0x40)) break;
                CheckHazard();
                if (_phase == DimitriPhase.Hazard) break;
                UpdateWater();
                _runtime.SetWramByte(0xd126,6);_runtime.SetWramByte(0xd127,6);
                if (!CompanionRuntimeState.MountingDisabled(_runtime) && player.CanMountCompanion && Distance(player.Position) < 9)
                {
                    _phase = DimitriPhase.Mounting;
                    player.BeginCompanionMount(player.PrecisePosition);
                    _mountStarted = true;
                }
                break;
            case DimitriPhase.Mounting:
                if (CompanionRuntimeState.MountingDisabled(_runtime) || player.CompanionMountInterrupted)
                {
                    _phase = DimitriPhase.Waiting;
                    _mountStarted = false;
                    break;
                }
                if (!_mountStarted)
                {
                    player.BeginCompanionMount(player.PrecisePosition);
                    _mountStarted = true;
                    break;
                }
                player.NudgeCompanionMountToward(_precisePosition);
                if (!player.CompanionJumpReadyToRide) break;
                _phase = DimitriPhase.Riding;
                _angle = 0xff;
                _mountStarted = false;
                SetAnimation(0);
                CompanionRuntimeState.BeginAnimalRide(_runtime, _save, SpecialObjectId.Dimitri, _precisePosition, _direction);
                SynchronizePlayer(player, finishMount: true);
                break;
            case DimitriPhase.Riding:
                if (!OracleObjectMath.UpdateSpeedZ(ref _carried.ZFixed, ref _carried.SpeedZ, 0x40)) break;
                if (attackPressed)
                {
                    _phase = DimitriPhase.Eating; _bitePhase = 0; _counter=0; _swallowed = false;
                    _angle = _direction * 8;
                    _speed = 0x1e;
                    SetAnimation(8);
                    _sound(SoundId.SndDimitri);
                    spawns.Add(new DimitriMouthSpawn(_group, _room.Id));
                    break;
                }
                // companionGotoDismountState rejects dismounts in water.
                if (itemPressed)
                {
                    if (_water == 0) BeginDismount();
                    break; // The native tail call returns even when water rejects dismounting.
                }
                UpdateMovement(spawns);
                break;
            case DimitriPhase.Eating:
                _animation.Advance();
                if (_bitePhase < 2) ApplySpeed(_speed, collide: false);
                if (_bitePhase == 0 && (_animation.CurrentParameter & 0x80) != 0)
                {
                    _bitePhase = 1; _counter = 12; _angle = (_direction ^ 2) * 8;
                    SetAnimation(0);
                }
                else if (_bitePhase == 1 && (_counter==0 || --_counter == 0))
                {
                    _counter=20;
                    _angle = _direction * 8;
                    if (_swallowed) { _bitePhase = 2; SetAnimation(0x10); }
                    else { _phase = DimitriPhase.Riding; _bitePhase=0; SetAnimation(0); }
                }
                else if (_bitePhase == 2 && (_counter==0 || --_counter == 0))
                { _phase = DimitriPhase.Riding; _bitePhase=0; SetAnimation(0); }
                break;
            case DimitriPhase.Dismounting:
                if (_runtime.ReadWramByte(0xd105)==0)
                {
                    _runtime.SetWramByte(0xd105,1);
                    Dismount(player);
                    SetAnimation(0x1c);
                }
                else if (!player.CompanionJumpActive)
                {
                    _phase = DimitriPhase.AwaitingDistance;
                }
                break;
            case DimitriPhase.AwaitingDistance:
                if (Distance(player.PrecisePosition) >= 9)
                {_phase=DimitriPhase.Waiting;_runtime.SetWramByte(0xd105,0);_runtime.SetWramByte(0xd13b,0);}
                break;
            case DimitriPhase.Carried:
                UpdateCarried(player);
                break;
            case DimitriPhase.Thrown:
                UpdateThrown(player);
                break;
            case DimitriPhase.ThrownLanding:
                if (!OracleObjectMath.UpdateSpeedZ(ref _carried.ZFixed, ref _carried.SpeedZ, 0x40)) break;
                _tileBreaker.TryBreak(_precisePosition + new Vector2(0, 5), 0x13, spawns);
                CheckHazard();
                if (_phase == DimitriPhase.Hazard) break;
                UpdateWater();
                _phase = DimitriPhase.Waiting;
                _runtime.SetWramByte(0xd105,0);
                SetAnimation(0x1c);
                break;
            case DimitriPhase.ReturningToLand:
                if (!OracleObjectMath.UpdateSpeedZ(ref _carried.ZFixed, ref _carried.SpeedZ, 0x40)) break;
                AdvanceNativeMovement(spawns);
                _phase=_water==0?DimitriPhase.Waiting:DimitriPhase.ReturningToLand;
                break;
            case DimitriPhase.CliffJump:
                if (_counter > 0)
                {
                    if (--_counter == 0) _sound(SoundId.SndJump);
                    break;
                }
                _animation.Advance();
                ApplySpeed(_speed, collide: false);
                OracleObjectMath.UpdateSpeedZ(ref _carried.ZFixed, ref _carried.SpeedZ, 0x40);
                int away = CompanionMovement.FacingWallMask((_angle + 0x10) & ObjectAngle.Mask, AdjacentWalls());
                if (away != 0) _cliffWalls = away;
                else if (_cliffWalls != 0) { _phase = DimitriPhase.Riding; SetAnimation(0); }
                break;
            case DimitriPhase.FlutePending:
                SpecialObjectOamDatabase.For(AssetRoot).InitializeCompanion(_runtime);
                _runtime.SetWramByte(0xd124,0x80);
                _runtime.SetWramByte(0xd11a,0xc1);
                _phase = DimitriPhase.FluteEntering;
                _counter = 0x3c;
                _sound(SoundId.SndDimitri);
                SetAnimation(0);
                break;
            case DimitriPhase.FluteEntering:
                AdvanceNativeMovement(spawns);
                _runtime.SetWramByte(0xd104,0x0c);
                Vector2 ahead = _precisePosition + _native.Probes("carry")[_direction];
                // companionRetIfNotFinishedWalkingIn returns normally at an
                // obstruction, or when the sixty clear-tile updates expire.
                if (_room.GetTerrainInfo(ahead).Collision != 0 || --_counter == 0)
                {
                    _runtime.SetWramByte(0xd103,0);
                    CompanionRuntimeState.UpdatePosition(_runtime,SpecialObjectId.Dimitri,_precisePosition);
                    _initialize(this,frame,spawns);
                    return;
                }
                break;
            case DimitriPhase.Hazard:
                if (_hazard.Advance(ref _precisePosition, _animation, _runtime, 1, false, 0x25, 0x25, _animation.SetAnimation, _sound))
                {
                    int respawnAngle=_angle;
                    _precisePosition = CompanionHazard.ResolveRespawn(player, _save, _runtime, ref respawnAngle, CanRespawnAt,_room,_precisePosition);
                    _angle=respawnAngle;
                    if (_hazardMounted) player.ApplyCompanionHazardDamage(_hazard.Type);
                    _phase = _hazardMounted ? DimitriPhase.Riding : DimitriPhase.Waiting;
                    _carried.ZFixed = 0; _carried.SpeedZ = 0;
                    _direction = (_angle >> 3) & 3; SetAnimation(0);
                }
                break;
            case DimitriPhase.Finished:
            case DimitriPhase.Inactive:
                break;
            case DimitriPhase.Carpenter:
                if(_runtime.ReadWramByte(0xd13c)==0)
                {
                    _phase=DimitriPhase.Waiting;_runtime.SetWramByte(0xd105,0);_runtime.SetWramByte(0xd13b,1);
                    CompanionRuntimeState.ReleaseCarried(_runtime,SpecialObjectId.Dimitri);SetAnimation(0x1c);
                }
                else{_phase=DimitriPhase.Riding;_bitePhase=0;SetAnimation(0);}
                break;
            default: throw new InvalidOperationException($"Unsupported Dimitri state {_phase} in {_group:x}:{_room.Id:x2}.");
        }
        PublishFrame(player);
    }

    internal void Dismount(Player player)
    {
        CompanionRuntimeState.DismountAnimal(_runtime,SpecialObjectId.Dimitri);
        player.BeginCompanionDismount(_precisePosition,_direction);
        CompanionRuntimeState.RememberAfterDismount(_runtime,player.Inventory,SpecialObjectId.Dimitri,_group,_room.Id,_precisePosition);
    }

    internal void AdvanceNativeMovement(ICollection<RoomEntitySpawn> spawns)
    {
        if((_animation.CurrentParameter&0x80)!=0)_sound(SoundId.SndLinkSwim);
        ApplySpeed(_water==0?0x1e:0x28);BreakGroundTile(spawns);_animation.Advance();
        CheckMovementHazards();
    }

    private void CheckMovementHazards()
    {
        if(_carried.ZFixed>=0 && CompanionHazard.TryCreate(_room,_precisePosition,out var hazard))
        {
            _hazardMounted=LinkRiding;_hazard=hazard;
            _phase=DimitriPhase.Hazard;_runtime.SetWramByte(0xd105,0);_runtime.SetWramByte(0xd106,0);
            if(hazard.Type==HazardType.Hole)return;
            _runtime.SetWramByte(0xd104,5);
            if(_runtime.Game==OracleGame.Ages && _runtime.ReadField("wLinkForceState")==2)
            {
                _runtime.SetField("wLinkForceState",0);_phase=DimitriPhase.Hazard;
                return;
            }
        }
        UpdateWater();
    }

    private void PublishFrame(Player player)
    {
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (Finished)
            CompanionRuntimeState.Clear(_runtime, SpecialObjectId.Dimitri);
        else if (LinkRiding || CompanionRuntimeState.IsActive(_runtime, SpecialObjectId.Dimitri))
            CompanionRuntimeState.Update(_runtime, SpecialObjectId.Dimitri, _precisePosition, _direction);
        if (!Finished) PublishAnimation();
        if (LinkRiding)
        {
            SynchronizePlayer(player);
        }
        ZIndex = ObjectDrawPriority.FromVisible(_runtime.ReadWramByte(0xd11a));
        Visible=!Finished && (_runtime.ReadWramByte(0xd11a)&0x80)!=0;
        QueueRedraw();
    }

    private void UpdateMovement(ICollection<RoomEntitySpawn> spawns)
    {
        int angle = CompanionMovement.AngleForInput(Input.GetVector("move_left", "move_right", "move_up", "move_down"));
        if (angle != 0xff)
        {
            if (_angle != angle)
            {
                _angle = angle;
                int direction = CompanionMovement.DirectionForAngle(angle, _direction);
                if (direction != _direction) { _direction = direction; SetAnimation(0); }
                else _animation.Advance();
                return; // dimitriState5 returns on an angle change.
            }
            if (TryStartCliffJump()) return;
            AdvanceNativeMovement(spawns);
            return;
        }
        CheckMovementHazards();
    }

    private void UpdateWater()
    {
        int water = IsWater(_precisePosition) ? 4 : 0;
        if (water != 0 && _room.GetMetatile(_precisePosition) is 0xfe or 0xff)
            _precisePosition.Y += 0.75f; // dimitriAddWaterfallResistance adds $00c0.
        if (water == _water) return;
        _water = water;
        bool walking = LinkRiding || _phase is DimitriPhase.Scripted or DimitriPhase.FluteEntering or DimitriPhase.ReturningToLand;
        // dimitriUpdateMovement@setWaterStatus calls the direction helper,
        // even when the retained angle is $ff. State 1 only sets animation.
        if (walking) _direction = (_angle >> 3) & 3;
        SetAnimation(walking ? 0 : 0x1c);
    }

    private bool IsWater(Vector2 position)
    {
        Vector2 probe = position + new Vector2(0, 5);
        if (probe.X < 0 || probe.Y < 0 || probe.X >= _room.Width || probe.Y >= _room.Height) return false;
        // Native Dimitri branches only on hazard $02 (hole); both remaining
        // hazard codes follow the swimming path.
        return _room.GetMetatile(probe) >= 0xfe || _room.GetTerrainInfo(probe).Hazard is HazardType.Water or HazardType.Lava;
    }
    private void CheckHazard()
    {
        if (_carried.ZFixed < 0) return;
        if (!CompanionHazard.TryCreate(_room, _precisePosition, out var hazard) || hazard.Type != HazardType.Hole) return;
        _hazardMounted = LinkRiding;
        _hazard = hazard; _phase = DimitriPhase.Hazard;_runtime.SetWramByte(0xd105,0);_counter=0;
    }
    private bool CanRespawnAt(Vector2 position) => CanOccupy(position) &&
        _room.GetTerrainInfo(position).Collision == 0 &&
        !CompanionHazard.TryCreate(_room, position, out _);
    private void BeginDismount()
    {
        _phase = DimitriPhase.Dismounting;_runtime.SetWramByte(0xd105,0);
    }
    private int Distance(Vector2 point) => Math.Abs(Mathf.FloorToInt(point.X) - Mathf.FloorToInt(_precisePosition.X)) +
        Math.Abs(Mathf.FloorToInt(point.Y) - Mathf.FloorToInt(_precisePosition.Y));
    private void ApplySpeed(int speed, bool collide = true)
    {
        _speed = speed;
        Vector2 before = _precisePosition;
        if (collide) SpecialObjectMovement.ApplySpeed(_runtime, ref _precisePosition, speed, _angle, _adjacentWalls = AdjacentWalls());
        else NativeObjectMovement.ApplySpeed(_runtime, ref _precisePosition, speed, _angle);
        // Source coordinates wrap as bytes. Keep the offscreen top/left flute
        // entrance on the negative side of world space until it crosses zero.
        if (_phase == DimitriPhase.FluteEntering)
        {
            if (before.X < 0 && _precisePosition.X > 128) _precisePosition.X -= 256;
            if (before.Y < 0 && _precisePosition.Y > 128) _precisePosition.Y -= 256;
        }
    }

    private void BreakGroundTile(ICollection<RoomEntitySpawn> spawns)
    {
        if ((_carried.ZFixed >> 8) == 0)
            _tileBreaker.TryBreak(_precisePosition + new Vector2(0, 5), 0x13, spawns);
    }
    private bool CanOccupy(Vector2 position)
    {
        foreach (Vector2 offset in _native.Probes("collision"))
        {
            if (CollisionAt(position + offset)) return false;
        }
        return true;
    }

    private bool CollisionAt(Vector2 sample) => _native.IsSolid(_room, sample, swimming: true);

    private int AdjacentWalls()
    {
        int walls = 0;
        var probes = _native.Probes("collision");
        for (int index = 0; index < probes.Count; index++)
            if (CollisionAt(_precisePosition + probes[index])) walls |= 1 << (7 - index);
        return walls;
    }

    private bool TryStartCliffJump()
    {
        if ((_angle & 0xe7) != 0 || CompanionMovement.FacingWallMask(_angle, _adjacentWalls) is not (3 or 0x0c or 0x30)) return false;
        byte tile = _room.GetMetatile(_precisePosition + _native.Probes("cliff")[_direction]);
        if (tile == _native.VineTop ? _angle != ObjectAngle.Down : !_ledges.IsCliffTile(_room.ActiveCollisions, tile, _angle)) return false;
        _phase = DimitriPhase.CliffJump;
        _speed = 0x50;
        _carried.SpeedZ = -0x2c0;
        _counter = 0x14;
        _cliffWalls = 0;
        return true;
    }

    public bool TryUseBracelet(Player player, Vector2I releaseDirection)
    {
        if (_phase == DimitriPhase.Carried)
        {
            // wcc67 prevents the Bracelet parent from throwing toward a wall.
            int angle = CompanionMovement.AngleForInput(releaseDirection);
            if (CompanionMovement.FacingWallMask(angle, AdjacentWalls()) != 0) return false;
            ReleaseCarried(player, releaseDirection);
            return true;
        }
        if (_phase is not (DimitriPhase.Waiting or DimitriPhase.AwaitingDistance) ||
            player.IsCarryingObject || player.TopDownAirborne || _carried.ZFixed != 0 || !CanBeCarried(player)) return false;
        Vector2 point = OracleObjectMath.ToPixelPosition(player.Position) + player.FacingVector * NpcCharacter.AButtonPointOffset;
        Vector2 difference = _precisePosition - point;
        if (difference.X is < -6 or >= 6 || difference.Y is < -6 or >= 6) return false;
        _phase = DimitriPhase.Carried;
        _runtime.SetWramByte(0xd105,1);
        _holder = player;
        _water = 0;
        _direction = CarriedObjectMotion.DirectionIndex(player.FacingVector);
        CompanionRuntimeState.Begin(_runtime, SpecialObjectId.Dimitri, _precisePosition, _direction);
        SetAnimation(0x18);
        _carried = new CarriedObjectMotion(_precisePosition);
        player.BraceletObjectWeight = 4; // dimitriState2Substate0: wLinkGrabState2=$40.
        player.BeginCarriedObjectPose();
        _save.WriteField("wCompanionTutorialTextShown", (byte)(_save.ReadField("wCompanionTutorialTextShown") | 4));
        _carried.Hold(player);
        _precisePosition = OracleObjectMath.ToPixelPosition(_carried.GroundPosition) +
            _precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition);
        return true;
    }

    private bool CanBeCarried(Player player, bool initialGrab = true)
    {
        int collision = _room.GetTerrainInfo(player.Position).Collision;
        int tile = _room.GetMetatile(player.Position);
        if ((initialGrab && collision is 0x0c or 0x0f or 0x11 or 0x19) ||
            tile == _native.VineMiddle || tile == _native.VineBottom) return false;
        int angle = CompanionMovement.AngleForInput(Input.GetVector("move_left", "move_right", "move_up", "move_down"));
        if (!initialGrab && angle == 0xff) return true;
        int direction = CarriedObjectMotion.DirectionIndex(player.FacingVector);
        if (!CarryDirectionAllowed(direction)) return false;
        if (angle != 0xff && (angle & 4) != 0)
        {
            int first = angle >> 3, second = (first + 1) & 3;
            return CarryDirectionAllowed(direction == first ? second : first);
        }
        return true;
    }

    private bool CarryDirectionAllowed(int direction)
    {
        Vector2 point = _precisePosition + _native.Probes("carry")[direction];
        int tile = _room.GetMetatile(point);
        return tile != _native.VineTop && tile != _native.VineMiddle && tile != _native.VineBottom &&
            _room.GetTerrainInfo(point).Collision is not (0x0c or 0x11 or 0x19);
    }

    private void UpdateCarried(Player player)
    {
        if (!CanBeCarried(player, initialGrab: false))
        {
            CompanionRuntimeState.ReleaseCarried(_runtime, SpecialObjectId.Dimitri);
            ReleaseCarried(player, Vector2I.Zero);
            return;
        }
        if (CompanionMovement.AngleForInput(Input.GetVector("move_left", "move_right", "move_up", "move_down")) != 0xff)
            _direction = CarriedObjectMotion.DirectionIndex(player.FacingVector);
        _carried.Hold(player);
        _precisePosition = OracleObjectMath.ToPixelPosition(_carried.GroundPosition) +
            _precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition);
        CompanionRuntimeState.Update(_runtime, SpecialObjectId.Dimitri, _precisePosition, _direction);
    }

    private void ReleaseCarried(Player player, Vector2I direction)
    {
        _braceletChildMotion = _carried;
        _braceletChildMotion.Release(player, direction, _bracelet);
        _braceletChildStarting = true;
        _throwOrigin = OracleObjectMath.ToPixelPosition(player.Position);
        _phase = DimitriPhase.Thrown;
        _holder = null;
    }

    private void UpdateThrown(Player player)
    {
        CompanionRuntimeState.ReleaseCarried(_runtime, SpecialObjectId.Dimitri);
        CheckHazard();
        if (_phase == DimitriPhase.Hazard) return;
        bool stopped = CompanionMovement.FacingWallMask(_angle, AdjacentWalls()) != 0;
        // dimitriState2Substate2 deliberately tests group zero, not room size.
        float maxX = _group == 0 ? 155 : 239, maxY = _group == 0 ? 122 : 168;
        if (_precisePosition.Y < 8) { _precisePosition.Y = 16; stopped = true; }
        if (_precisePosition.Y >= maxY) { _precisePosition.Y = maxY; stopped = true; }
        if (_precisePosition.X < 4) { _precisePosition.X = 4; stopped = true; }
        if (_precisePosition.X >= maxX) { _precisePosition.X = maxX; stopped = true; }
        if (stopped && !_braceletChildStarting) _braceletChildMotion.SpeedRaw = ObjectSpeed.Speed0;
        if (_carried.ZFixed >= 0 && IsWater(_precisePosition))
        {
            _angle = OracleObjectMovement.Shared.RelativeAngle(_precisePosition, _throwOrigin) & ObjectAngle.CardinalMask;
            _direction = _angle >> 3;
            _water = 4;
            SetAnimation(0);
            _phase = DimitriPhase.ReturningToLand;
        }
    }

    bool IBraceletChildRoomEntity.ReservedBraceletChildActive => _phase == DimitriPhase.Thrown;
    void IBraceletChildRoomEntity.WriteReservedBraceletAngle(int angle) => _braceletChildMotion.Angle = angle;

    void IBraceletChildRoomEntity.UpdateBraceletChild(Player player, bool frozen)
    {
        if (_phase is not (DimitriPhase.Carried or DimitriPhase.Thrown))
            return;
        if (frozen && (_phase != DimitriPhase.Thrown || !_braceletChildStarting))
            return;
        // ITEM_BRACELET copies only the high XYZ bytes after Link and the item
        // parents. The companion handler has already observed the previous copy.
        Vector2 fraction = _precisePosition - OracleObjectMath.ToPixelPosition(_precisePosition);
        if (_phase == DimitriPhase.Carried)
        {
            _carried.Hold(player);
            _precisePosition = OracleObjectMath.ToPixelPosition(_carried.GroundPosition) + fraction;
        }
        else if (_phase == DimitriPhase.Thrown)
        {
            _braceletChildStarting = false;
            _angle = _braceletChildMotion.Angle;
            _braceletChildMotion.AdvanceHorizontal(_throwing, _room,
                (position, speed, angle) => NativeObjectMovement.ApplySpeed(_runtime, position, speed, angle));
            if (_braceletChildMotion.AdvanceVertical(_bracelet))
            {
                _sound(SoundId.SndBombLand);
                if (!_braceletChildMotion.Bounce(_throwing))
                {
                    _phase = DimitriPhase.ThrownLanding;
                    return; // Native release deletes the child without a final position copy.
                }
            }
            _precisePosition = OracleObjectMath.ToPixelPosition(_braceletChildMotion.GroundPosition) + fraction;
            _carried.ZFixed = (_braceletChildMotion.ZFixed & ~0xff) | (_carried.ZFixed & 0xff);
        }
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        CompanionRuntimeState.Update(_runtime, SpecialObjectId.Dimitri, _precisePosition, _direction);
    }
    internal void SetAnimation(int animationBase)
    {
        _animation.SetAnimation(animationBase + _direction + _water);
        PublishAnimation();
    }
    private void PublishAnimation()
    {
        if (!CompanionRuntimeState.IsActive(_runtime, SpecialObjectId.Dimitri)) return;
        CompanionNativeAnimationDatabase.For(_data.AssetRoot).PublishAnimation(_runtime,SpecialObjectId.Dimitri,
            _animation.AnimationIndex,_animation.FrameIndex,_animation.RemainingUpdates,_animation.CurrentParameter);
    }

    private void SynchronizePlayer(Player player, Vector2 screenOffset = default, bool finishMount = false)
    {
        int parameter = _animation.CurrentParameter & 0x3f;
        if ((uint)parameter >= _data.Visual.LinkTextures.Length)
            throw new InvalidOperationException($"Dimitri animation emitted unsupported Link parameter ${parameter:x2}.");
        // specialObjects.s:func_410d@ridingDimitri, signed BC offsets.
        Vector2 offset = new(_direction == 1 ? -5 : _direction == 3 ? 5 : 0, -10);
        if (finishMount) player.FinishCompanionMount(_precisePosition, offset, _direction, _carried.ZFixed,
            _data.Visual.LinkTextures[parameter], _data.Visual.LinkDamageTextures[parameter], _data.Visual.LinkOffsets[parameter]);
        else player.SetCompanionRidePosition(_precisePosition, offset, _direction, _carried.ZFixed,
            _data.Visual.LinkTextures[parameter], _data.Visual.LinkDamageTextures[parameter], _data.Visual.LinkOffsets[parameter], screenOffset);
    }
    public override void _Draw() => DrawTexture(_animation.CurrentTexture,
        _animation.CurrentOffset + SourceOamDrawOffset + new Vector2(0, _carried.ZFixed >> 8));
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    void ICompanionBarrierTarget.SetBarrierCoordinate(bool horizontal, int coordinate)
    {
        if (horizontal)
            _precisePosition.X = coordinate + _precisePosition.X - Mathf.Floor(_precisePosition.X);
        else
            _precisePosition.Y = coordinate + _precisePosition.Y - Mathf.Floor(_precisePosition.Y);
        _speed = 0;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        CompanionRuntimeState.Update(_runtime, SpecialObjectId.Dimitri, _precisePosition, _direction);
    }
    void ICompanionBarrierTarget.SynchronizeRiderAfterObjects(Player player) => SynchronizePlayer(player);
    void ICompanionBarrierTarget.DropForSymmetryBarrier(Player player)
    {
        if (_phase is not (DimitriPhase.Carried or DimitriPhase.Thrown or DimitriPhase.ThrownLanding)) return;
        // companionScript_subid0d selects state2/substate3 before dropping Link's held item.
        _phase = DimitriPhase.ThrownLanding; _holder = null; _carried.GroundPosition = _precisePosition;
        player.DropHeldItemsForScript(); player.EndCarriedObjectPose();
    }
    public void SetScreenTransitionBoundaryCoordinate(bool horizontal, int coordinate, Player player)
    {
        if (_phase == DimitriPhase.Carried)
        {
            Vector2 position = player.PrecisePosition;
            if (horizontal) position.X = coordinate + position.X - Mathf.Floor(position.X);
            else position.Y = coordinate + position.Y - Mathf.Floor(position.Y);
            SetCarriedTransitionPosition(position, Vector2.Zero, player);
            return;
        }
        if (horizontal) _precisePosition.X = coordinate + _precisePosition.X - Mathf.Floor(_precisePosition.X);
        else _precisePosition.Y = coordinate + _precisePosition.Y - Mathf.Floor(_precisePosition.Y);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        CompanionRuntimeState.Update(_runtime, SpecialObjectId.Dimitri, _precisePosition, _direction);
        SynchronizePlayer(player);
    }
    public void BeginScreenTransition(OracleRoomData destination) => _destination = destination;
    public void SetScreenTransitionPosition(Vector2 position, Vector2 screenOffset, Player player)
    {
        if (_phase == DimitriPhase.Carried) { SetCarriedTransitionPosition(position, screenOffset, player); return; }
        _precisePosition = position; Position = OracleObjectMath.ToPixelPosition(position); SynchronizePlayer(player, screenOffset);
    }
    private void SetCarriedTransitionPosition(Vector2 position, Vector2 screenOffset, Player player)
    {
        player.SetScrollingTransitionPosition(position, -screenOffset);
        Vector2I offset = CarriedObjectMotion.HeldOffset(player);
        _precisePosition = position + new Vector2(offset.X, 0);
        _carried.GroundPosition = _precisePosition;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }
    public void FinishScreenTransition(Vector2 position, Player player)
    {
        _room = _destination ?? throw new InvalidOperationException("Dimitri finished scrolling without a destination.");
        _tileBreaker = _createTileBreaker(_room);
        if (_phase == DimitriPhase.Carried)
        {
            _destination = null;
            player.FinishScrollingTransition(position);
            player.BeginCarriedObjectPose();
            SetCarriedTransitionPosition(position, Vector2.Zero, player);
            CompanionRuntimeState.Update(_runtime, SpecialObjectId.Dimitri, _precisePosition, _direction);
            return;
        }
        _destination = null; _precisePosition = position; Position = OracleObjectMath.ToPixelPosition(position);
        player.SetLocalRespawnPosition(Position);
        CompanionRuntimeState.SetLastAnimalMountPosition(_save, Position);
        CompanionRuntimeState.Update(_runtime, SpecialObjectId.Dimitri, position, _direction);
        SynchronizePlayer(player);
    }
}

internal enum DimitriPhase
{
    Mounting, Waiting, Riding, Eating, Dismounting, Inactive, Carpenter,
    AwaitingDistance, Hazard, Scripted, Finished,
    Carried, Thrown, ThrownLanding, ReturningToLand, CliffJump, FlutePending, FluteEntering
}

internal sealed record DimitriCompanionSpawn(Vector2 Position, int Direction, int Group, int Room,
    bool Riding = false, Vector2? FluteDestination = null) : RoomEntitySpawn;
