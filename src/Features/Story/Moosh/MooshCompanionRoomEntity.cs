using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// SPECIALOBJECT_MOOSH $0d states $01/$03/$05/$06/$08 and the room $0:$6b
/// state-$0a farewell. This is the live w1Companion owner after the room
/// $0:$6c rescue handoff.
/// </summary>
internal sealed partial class MooshCompanionRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IPlayerRestriction,
    IPlayerForcedMovement, IPlayerRideableRoomEntity,
    IPlayerScreenTransitionRoomEntity, IRoomEntityLifetime,
    ICompanionBarrierTarget, IForestCompanion, IRoomBlocker
{

    private bool _forestWaiting;
    public bool ForestButtonPressed { get; private set; }

    public void UseForestInteraction(bool outside)
    {
        _forestWaiting = true;
        _phase = MooshCompanionPhase.Waiting;
        SetAnimation(outside ? 3 : 0);
    }

    public void NoticeForestLink(int animation) => SetAnimation(animation);

    public void ForceForestMount()
    {
        _forestWaiting = false;
        _phase = MooshCompanionPhase.Mounting;
        _mountStarted = false;
        SetAnimation(1 + _direction);
    }

    public bool TryInteract(Player player)
    {
        Vector2 delta = _precisePosition - player.Position;
        if (!_forestWaiting || _dialogueOpen() || Math.Abs(delta.X) + Math.Abs(delta.Y) > 24 ||
            delta.Dot(player.FacingVector) <= 0) return false;
        ForestButtonPressed = true;
        return true;
    }

    public bool BlocksLink(Vector2 center) => _forestWaiting &&
        Math.Abs(center.X - Position.X) < 12 && Math.Abs(center.Y - Position.Y) < 8;

    private bool _fluteEntrance;
    private bool _flutePending;
    private int _fluteCounter;

    private void UpdateFluteEntrance(ICollection<RoomEntitySpawn> spawns)
    {
        if (_flutePending)
        {
            _flutePending = false;
            _fluteCounter = 0x3c;
            _playSound(0xc5);
            SetAnimation(0x0f + _direction);
            return;
        }
        _animation.Advance();
        Vector2 before = _precisePosition;
        CompanionMovement.ApplySpeed(ref _precisePosition, 0x1e, _angle, AdjacentWalls());
        if (before.X < 0 && _precisePosition.X > 128) _precisePosition.X -= 256;
        if (before.Y < 0 && _precisePosition.Y > 128) _precisePosition.Y -= 256;
        if ((_zFixed >> 8) == 0) BreakGroundTile(spawns);
        Vector2 offset = _direction switch
        {
            0 => new(0, -8), 1 => new(8, 0), 2 => new(0, 8), _ => new(-8, 0)
        };
        // mooshStateC/companionRetIfNotFinishedWalkingIn stop at a nonzero
        // collision tile or the sixtieth clear-tile update.
        if (_room.GetTerrainInfo(_precisePosition + offset).Collision == 0 && --_fluteCounter != 0) return;
        _fluteEntrance = false;
        _phase = MooshCompanionPhase.Waiting;
        _direction = 2;
        _angle = 0xff;
        SetAnimation(3);
    }

    private static readonly Vector2[] CollisionSamples =
    [
        new(-3, -5), new(4, -5), new(-3, 8), new(4, 8),
        new(-5, -3), new(-5, 6), new(6, -3), new(6, 6)
    ];

    private readonly MooshRescueEventRecord _record;
    private readonly MooshCompanionVisualRecord _visual;
    private readonly MooshGoodbyeEventRecord? _goodbye;
    private readonly OracleSaveData? _saveData;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _playSound;
    private readonly Action<int, string, Vector2> _dialogueRequested;
    private readonly Func<bool> _dialogueOpen;
    private readonly Action<int> _screenShakeRequested;
    private readonly EnemyAnimationPlayer _animation;
    private readonly CompanionTerrainDatabase _terrain = new();
    private readonly LedgeJumpDatabase _ledges = new();
    private readonly Func<OracleRoomData, CompanionAttackTileBreaker> _createTileBreaker;
    private CompanionAttackTileBreaker _tileBreaker;
    private int _cliffCounter;
    private int _cliffWalls;
    private readonly Texture2D[] _linkTextures;
    private readonly Texture2D[] _chargeLinkTextures;
    private readonly Texture2D[] _damageLinkTextures;
    private readonly Vector2[] _linkTextureOffsets;
    private OracleRoomData _room;
    private OracleRoomData? _transitionDestination;
    private Vector2 _precisePosition;
    private int _group;
    private int _roomId;
    private int _direction;
    private int _angle = 0xff;
    private int _zFixed;
    private int _speedZ;
    private int _gravityDelay;
    private int _flapCount;
    private int _chargeCounter;
    private int _waterHoverCounter;
    private CompanionHazard _hazard;
    private bool _hazardMounted;
    private bool _stompContactDisabled;
    private bool _airborneInitialized;
    private bool _mountStarted;
    private bool _attackPressed;
    private bool _itemPressed;
    private bool _attackJustPressed;
    private bool _itemJustPressed;
    private bool _attackEdgeObserved;
    private bool _itemEdgeObserved;
    private bool _chargePaletteActive;
    private bool _dismountInitialized;
    private bool _dismountLandingObserved;
    private Vector2 _dismountPreviousLinkPosition;
    private bool _goodbyeDialogueStarted;
    private bool _finished;
    private MooshCompanionPhase _phase;

    public Node2D Node => this;
    public bool DisablesSword => GoodbyeRestrictsPlayer ||
        LinkRiding || _phase == MooshCompanionPhase.Mounting;
    public bool DisablesItems => DisablesSword;
    public bool DisablesPlayerContact => LinkRiding && _stompContactDisabled;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => GoodbyeRestrictsPlayer;
    public bool DisablesScreenTransitions => _phase is
        MooshCompanionPhase.Mounting or
        MooshCompanionPhase.Airborne or
        MooshCompanionPhase.HoveringOverWater or
        MooshCompanionPhase.Charging or
        MooshCompanionPhase.Falling or
        MooshCompanionPhase.StompRecovery or
        MooshCompanionPhase.HazardFalling;
    public bool Finished => _finished;
    public bool LinkRiding => _phase is
        MooshCompanionPhase.Riding or
        MooshCompanionPhase.Airborne or
        MooshCompanionPhase.HoveringOverWater or
        MooshCompanionPhase.Charging or
        MooshCompanionPhase.Falling or
        MooshCompanionPhase.StompRecovery ||
        _phase == MooshCompanionPhase.CliffJump ||
        (_phase == MooshCompanionPhase.HazardFalling && _hazardMounted) ||
        (_phase == MooshCompanionPhase.Dismounting &&
            !_dismountInitialized);
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
    internal bool GoodbyeActive => _goodbye is not null && !_finished;
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
        MooshRescueEventDatabase database,
        OracleSaveData? saveData,
        OracleRuntimeState runtime,
        Action<int> playSound,
        Action<int, string, Vector2> dialogueRequested,
        Func<bool> dialogueOpen,
        Action<int> screenShakeRequested,
        Func<OracleRoomData, CompanionAttackTileBreaker> createTileBreaker)
    {
        _record = database.Record;
        _visual = database.Visual;
        _goodbye = spawn.Goodbye;
        _saveData = saveData;
        _runtime = runtime;
        _playSound = playSound;
        _dialogueRequested = dialogueRequested;
        _dialogueOpen = dialogueOpen;
        _screenShakeRequested = screenShakeRequested;
        _room = room;
        _createTileBreaker = createTileBreaker;
        _tileBreaker = createTileBreaker(room);
        _group = spawn.Group;
        _roomId = spawn.Room;
        _precisePosition = spawn.Position;
        Position = spawn.Position;
        _direction = spawn.Direction;
        if (spawn.FluteDestination is Vector2 destination)
        {
            _fluteEntrance = true;
            _flutePending = true;
            _angle = _direction * 8;
            CompanionRuntimeState.SetLastAnimalMountPosition(_runtime, destination);
        }
        _phase = spawn.Goodbye is not null
            ? MooshCompanionPhase.GoodbyeInitializing
            : spawn.ForceMount
            ? MooshCompanionPhase.Mounting
            : spawn.Riding
                ? MooshCompanionPhase.Riding
                : MooshCompanionPhase.Waiting;
        if (_goodbye is not null && _saveData is null)
        {
            throw new InvalidOperationException(
                "Room 0:6b $67:$01 Moosh goodbye requires live save state.");
        }

        MooshCompanionVisualRecord visual = _visual;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{visual.Sprite}.png"),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            positionedOam: true,
            paletteVariants: [2]);
        (_linkTextures, _chargeLinkTextures, _damageLinkTextures,
            _linkTextureOffsets) =
            CompanionLinkFrames.Load(
                visual.LinkSprite, visual.LinkPalette,
                visual.LinkFrames, visual.LinkSourceOffsets);
        SetAnimation(_goodbye?.InitialAnimation ??
            (_phase == MooshCompanionPhase.Riding
                ? 0x13 + _direction
                : 0x01 + _direction));
        Name = $"Moosh_{_group:x1}_{_roomId:x2}";
        ZIndex = _phase == MooshCompanionPhase.Riding
            ? NpcCharacter.BehindLinkZIndex
            : Player.NormalZIndex;
        Visible = true;
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
        _attackPressed = Input.IsActionPressed("attack");
        _itemPressed = Input.IsActionPressed("item");
        // mooshState5/state8 read wGameKeysJustPressed. Do not reconstruct
        // that edge from Moosh's last entity update: ordinary entities pause
        // while a textbox owns input, so doing so turns the held button which
        // dismissed the text into a fresh companion action after the pause.
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
            // Direct component callers can perform several synchronous
            // updates during one host frame. Consume its Godot edge once.
            _attackJustPressed = attackEdge && !_attackEdgeObserved;
            _itemJustPressed = itemEdge && !_itemEdgeObserved;
            _attackEdgeObserved = attackEdge;
            _itemEdgeObserved = itemEdge;
        }
        _chargePaletteActive = false;

        if (_fluteEntrance) UpdateFluteEntrance(spawns);
        else switch (_phase)
        {
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
            case MooshCompanionPhase.GoodbyeInitializing:
                InitializeGoodbye();
                break;
            case MooshCompanionPhase.GoodbyeDialogue:
                UpdateGoodbyeDialogue();
                break;
            case MooshCompanionPhase.GoodbyeFlight:
                UpdateGoodbyeFlight();
                break;
            case MooshCompanionPhase.GoodbyeFinished:
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Moosh phase {_phase}.");
        }

        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (LinkRiding)
        {
            CompanionRuntimeState.Update(
                _runtime, CompanionRuntimeState.MooshId,
                _roomId, _precisePosition, _direction);
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
            CurrentMooshTexture,
            _animation.CurrentOffset +
            new Vector2(0, _zFixed >> 8) +
            SourceOamDrawOffset);
    }

    private void UpdateWaiting(Player player)
    {
        _animation.Advance();
        if (_forestWaiting) return;
        if (CompanionRuntimeState.MountingDisabled(_runtime) || !player.CanMountCompanion ||
            !LinkWithinMountDistance(player))
        {
            // mooshState1 falls through to hazards only outside the mount
            // radius; a rejected mount returns from companionTryToMount.
            if (!LinkWithinMountDistance(player)) TryBeginHazard();
            return;
        }
        _phase = MooshCompanionPhase.Mounting;
        _mountStarted = false;
    }

    private void UpdateMounting(Player player)
    {
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
        ZIndex = NpcCharacter.BehindLinkZIndex;
        SetAnimation(0x13 + _direction);
        CompanionRuntimeState.Begin(
            _runtime, CompanionRuntimeState.MooshId,
            _roomId, _precisePosition, _direction);
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
            _airborneInitialized = false;
            _playSound(_record.JumpSound);
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
        if (!_airborneInitialized)
        {
            // mooshPressedAButton only selects state $08/substate $00. The
            // following update initializes speedZ, counters, and animation
            // before yielding without vertical movement.
            _airborneInitialized = true;
            _zFixed = 0;
            _speedZ = -0x0140;
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

            if (_attackJustPressed && _flapCount < 0x10)
            {
                _flapCount++;
                _gravityDelay += 8;
                _playSound(_record.JumpSound);
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
        if ((int)hazard != _visual.WaterHazard)
            return false;

        _phase = MooshCompanionPhase.HoveringOverWater;
        _speedZ = 0;
        _waterHoverCounter = _visual.WaterHoverFrames;
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
        if (_attackPressed && _chargeCounter < 120)
        {
            if (_chargeCounter >= 40)
                _chargePaletteActive = (frameCounter & 0x04) == 0;
            _chargeCounter++;
            if (_chargeCounter == 40)
                _playSound(_record.ChargeSound);
            // State $08:$02 returns immediately at 40; collision bit 7 is
            // first cleared at 41 and stays clear until recovery or a hazard.
            if (_chargeCounter > 40) _stompContactDisabled = true;
            if (_chargeCounter < 120)
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
            _saveData.WriteWramByte(0xc649, (byte)(_saveData.ReadWramByte(0xc649) | 0x20));
        _screenShakeRequested(0x0f);
        _playSound(OracleSoundEngine.SndCtrlStopSfx);
        _playSound(_record.StompSound);
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
        _dismountInitialized = false;
        _dismountLandingObserved = false;
        return true;
    }

    private void UpdateDismounting(Player player)
    {
        if (!_dismountInitialized)
        {
            // The B-button update only changes Moosh to state $06. On the
            // following companion pass, substate 0 performs the dismount,
            // saves both respawn anchors, and selects animation $01+direction.
            _dismountInitialized = true;
            CompanionRuntimeState.Remember(
                _runtime, CompanionRuntimeState.MooshId,
                _group, _roomId, _precisePosition);
            CompanionRuntimeState.Clear(
                _runtime, CompanionRuntimeState.MooshId);
            player.BeginCompanionDismount(_precisePosition, _direction);
            SetAnimation(0x01 + _direction);
            return;
        }
        if (player.CompanionJumpActive)
            return;

        // updateSpecialObjects visits w1Companion before w1Link. The port's
        // application schedule visits Link first, so retain state-$06
        // substate 1 for the landing update and advance it one pass later.
        if (!_dismountLandingObserved)
        {
            _dismountLandingObserved = true;
            return;
        }

        _dismountPreviousLinkPosition = player.PrecisePosition;
        _phase = MooshCompanionPhase.AwaitingDistance;
    }

    private void UpdateAwaitingDistance(Player player)
    {
        // Source state-$06 substate 2 runs before Link. Use the position from
        // the preceding update so crossing the strict c=$09 Manhattan radius
        // is observed on the same original object pass.
        Vector2 sourceOrderLinkPosition = _dismountPreviousLinkPosition;
        _dismountPreviousLinkPosition = player.PrecisePosition;
        if (LinkWithinMountDistance(sourceOrderLinkPosition))
        {
            TryBeginHazard();
            return;
        }
        _phase = MooshCompanionPhase.Waiting;
    }

    private bool GoodbyeRestrictsPlayer => _phase is
        MooshCompanionPhase.GoodbyeDialogue or
        MooshCompanionPhase.GoodbyeFlight;

    private void InitializeGoodbye()
    {
        MooshGoodbyeEventRecord goodbye = _goodbye ??
            throw new InvalidOperationException(
                "Moosh entered the room 0:6b initializer without source data.");
        _direction = goodbye.FlightAngle >> 3;
        _angle = goodbye.FlightAngle;
        SetAnimation(goodbye.InitialAnimation);
        _phase = MooshCompanionPhase.GoodbyeDialogue;
    }

    private void UpdateGoodbyeDialogue()
    {
        MooshGoodbyeEventRecord goodbye = _goodbye ??
            throw new InvalidOperationException(
                "Moosh entered the room 0:6b dialogue without source data.");
        if (!_goodbyeDialogueStarted)
        {
            _goodbyeDialogueStarted = true;
            _dialogueRequested(goodbye.TextId, goodbye.Text, _precisePosition);
            return;
        }
        if (_dialogueOpen())
            return;

        _speedZ = goodbye.InitialSpeedZ;
        _angle = goodbye.FlightAngle;
        SetAnimation(goodbye.FlightAnimation);
        _phase = MooshCompanionPhase.GoodbyeFlight;
    }

    private void UpdateGoodbyeFlight()
    {
        MooshGoodbyeEventRecord goodbye = _goodbye ??
            throw new InvalidOperationException(
                "Moosh entered the room 0:6b flight without source data.");
        _animation.Advance();

        // mooshStateASubstate6 tests only the high byte of speedZ. Once it
        // reaches zero, Z remains at the top of the arc while SPEED_100 moves
        // Moosh downward until his wrapping high-byte Y reaches $f0.
        if ((unchecked((ushort)_speedZ) & 0xff00) != 0)
        {
            OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, goodbye.FlightGravity);
            return;
        }

        OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition,
            goodbye.FlightSpeed,
            goodbye.FlightAngle);
        int y = unchecked((byte)Mathf.FloorToInt(_precisePosition.Y));
        if (y < goodbye.ExitY)
            return;

        CompleteGoodbye(goodbye);
    }

    private void CompleteGoodbye(MooshGoodbyeEventRecord goodbye)
    {
        OracleSaveData save = _saveData ??
            throw new InvalidOperationException(
                "Room 0:6b Moosh goodbye lost its save-state owner.");
        save.WriteWramByte(
            goodbye.MooshStateAddress,
            (byte)(save.ReadWramByte(goodbye.MooshStateAddress) |
                goodbye.LeftMask));
        CompanionRuntimeState.Clear(_runtime, CompanionRuntimeState.MooshId);
        CompanionRuntimeState.ForgetRemembered(_runtime);
        _phase = MooshCompanionPhase.GoodbyeFinished;
        _finished = true;
        Visible = false;
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
        _zFixed = 0;
        _speedZ = 0;
        _chargeCounter = 0;
        _airborneInitialized = false;
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
        _zFixed = 0;
        _speedZ = 0;
        _playSound(OracleSoundEngine.SndSplash);
        return true;
    }

    private void UpdateHazardFalling(Player player)
    {
        if (!_hazard.Advance(
                ref _precisePosition,
                _animation,
                waterAnimation: 0x0d,
                holeAnimation: 0x0e,
                SetAnimation,
                _playSound))
        {
            return;
        }

        HazardType completedHazard = _hazard.Type;
        Vector2 respawn = CompanionHazard.ResolveRespawn(
            player, _runtime, CanRespawnAt);
        _precisePosition = respawn;
        _hazard = default;
        _phase = _hazardMounted ? MooshCompanionPhase.Riding : MooshCompanionPhase.Waiting;
        _angle = 0xff;
        _chargeCounter = 0;
        _airborneInitialized = false;
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
        CompanionMovement.ApplySpeed(ref _precisePosition, 0x28, _angle, AdjacentWalls());
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
        if ((_angle & 0xe7) != 0 || CompanionMovement.FacingWallMask(_angle, AdjacentWalls()) is not (3 or 0x0c or 0x30)) return false;
        byte tile = _room.GetMetatile(_precisePosition + _terrain.Probes("cliff")[_direction]);
        if (tile == 0xd4 ? _angle != 0x10 : !_ledges.IsCliffTile(_room.ActiveCollisions, tile, _angle)) return false;
        _phase = MooshCompanionPhase.CliffJump;
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
                _playSound(_record.JumpSound);
                SetAnimation(0x09 + _direction);
            }
            return;
        }
        _animation.Advance();
        OracleObjectMovement.Shared.ApplySpeed(ref _precisePosition, 0x50, _angle);
        OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x40);
        int away = CompanionMovement.FacingWallMask((_angle + 0x10) & 0x1f, AdjacentWalls());
        if (away != 0) _cliffWalls = away;
        else if (_cliffWalls != 0)
        {
            // mooshState7 resumes state $05 without resetting the falling arc.
            _phase = MooshCompanionPhase.Riding;
            SetAnimation(0x13 + _direction);
        }
    }

    void ICompanionBarrierTarget.ClampToLowerY(int y)
    {
        if (y is < 0 or > byte.MaxValue || _precisePosition.Y <= y)
            return;
        _precisePosition.Y = y;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

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
            _runtime, CompanionRuntimeState.MooshId,
            _roomId, _precisePosition, _direction);
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
        CompanionRuntimeState.SetLastAnimalMountPosition(_runtime, respawn);
        CompanionRuntimeState.Update(
            _runtime, CompanionRuntimeState.MooshId,
            _roomId, position, _direction);
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
    GoodbyeInitializing,
    GoodbyeDialogue,
    GoodbyeFlight,
    GoodbyeFinished,
    CliffJump
}

internal sealed record MooshCompanionSpawn(
    Vector2 Position,
    int Direction,
    int Group,
    int Room,
    bool ForceMount = false,
    bool Riding = false,
    MooshGoodbyeEventRecord? Goodbye = null,
    Vector2? FluteDestination = null) : RoomEntitySpawn;

internal sealed record MooshStompAttackSpawn(
    Vector2 Position,
    int Group,
    int Room) : RoomEntitySpawn(UpdateThisFrame: true);

internal sealed record MooshHoverExclamationSpawn(
    Vector2 Position,
    int ZFixed) : RoomEntitySpawn;
