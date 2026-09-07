using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>SPECIALOBJECT_DIMITRI $0c and its native companion script handoff.</summary>
internal sealed partial class DimitriCompanionRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IPlayerRestriction, IPlayerForcedMovement,
    IPlayerRideableRoomEntity, IPlayerScreenTransitionRoomEntity,
    IRoomEntityLifetime, IPlayerInteractable, IRoomBlocker, ICompanionBarrierTarget,
    IBraceletInteractableRoomEntity, IForestCompanion
{
    private readonly DimitriDatabase _data;
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _sound;
    private readonly Action<int, string, Vector2> _showText;
    private readonly Func<bool> _dialogueOpen;
    private readonly EnemyAnimationPlayer _animation;
    private CompanionAttackTileBreaker _tileBreaker;
    private readonly Func<OracleRoomData, CompanionAttackTileBreaker> _createTileBreaker;
    private OracleRoomData _room;
    private OracleRoomData? _destination;
    private Vector2 _precisePosition;
    private readonly int _group;
    private int _direction;
    private int _angle = 0xff;
    private int _water;
    private int _counter;
    private int _bitePhase;
    private bool _swallowed;
    private bool _mountStarted;
    private bool _rescueMount;
    private bool _dismountStarted;
    private bool _landingObserved;
    private bool _goodbye;
    private Vector2 _previousLink;
    private bool _attackEdge;
    private bool _itemEdge;
    private CompanionHazard _hazard;
    private bool _hazardMounted;
    private DimitriPhase _phase;
    private readonly DimitriNativeDatabase _native = new();
    private readonly BraceletDatabaseRecord _bracelet = new BraceletDatabase().Data;
    private readonly BombRecord _throwing = new BombDatabase().Data;
    private readonly LedgeJumpDatabase _ledges = new();
    private CarriedObjectMotion _carried;
    private Vector2 _throwOrigin;
    private int _cliffWalls;
    private Player? _holder;
    private bool _forestInteraction;
    public bool ForestButtonPressed { get; private set; }

    public void UseForestInteraction(bool outside)
    {
        _forestInteraction = true;
        _phase = DimitriPhase.Harassed;
        if (outside) _animation.SetAnimation(0x1e);
    }
    public void NoticeForestLink(int animation) => _animation.SetAnimation(animation);
    public void ForceForestMount()
    {
        _phase = DimitriPhase.Mounting;
        _mountStarted = false;
        _rescueMount = false;
        SetAnimation(0x1c);
    }

    public Node2D Node => this;
    public bool Finished => _phase == DimitriPhase.Finished;
    public bool LinkRiding => _phase is DimitriPhase.Riding or DimitriPhase.Eating or
        DimitriPhase.MountedDialogue or DimitriPhase.CliffJump ||
        (_phase == DimitriPhase.Hazard && _hazardMounted) ||
        (_phase == DimitriPhase.Dismounting && !_dismountStarted);
    private bool StoryLocked => _phase is DimitriPhase.IntroPending or DimitriPhase.IntroDialogue or
        DimitriPhase.RescueDialogue or DimitriPhase.MountedDialogue or DimitriPhase.GoodbyeDialogue or DimitriPhase.Leaving;
    public bool DisablesSword => LinkRiding || StoryLocked || _phase == DimitriPhase.Mounting;
    public bool DisablesItems => DisablesSword;
    public bool DisablesMovement => DisablesSword;
    public bool DisablesMenus => StoryLocked;
    public bool DisablesScreenTransitions => StoryLocked || (_phase == DimitriPhase.Hazard && _hazardMounted) ||
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
        Action<int> sound, Action<int, string, Vector2> showText, Func<bool> dialogueOpen,
        Func<OracleRoomData, CompanionAttackTileBreaker> createTileBreaker)
    {
        _data = data; _save = save; _runtime = runtime; _sound = sound;
        _showText = showText; _dialogueOpen = dialogueOpen; _room = room;
        _createTileBreaker = createTileBreaker;
        _tileBreaker = createTileBreaker(room);
        _group = spawn.Group; _precisePosition = spawn.Position; _direction = spawn.Direction;
        _phase = spawn.Riding ? DimitriPhase.Riding :
            (save.ReadWramByte(0xc647) & 0x80) == 0 &&
            ((save.ReadWramByte(0xc647) & 0x40) != 0 || (save.ReadWramByte(0xc647) & 0x20) == 0)
                ? DimitriPhase.Harassed : DimitriPhase.Waiting;
        if (spawn.FluteDestination is Vector2 destination)
        {
            _phase = DimitriPhase.FlutePending;
            _angle = _direction * 8;
            CompanionRuntimeState.SetLastAnimalMountPosition(_runtime, destination);
        }
        _animation = new EnemyAnimationPlayer(this, data.Animations.Length);
        _animation.Load(OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_dimitri.png"),
            data.Animations, 0, 2, positionedOam: true, animationSourceOffsets: data.SourceOffsets);
        _water = IsWater(_precisePosition) ? 4 : 0;
        _animation.SetAnimation(_phase == DimitriPhase.Harassed ? 0x24 :
            (LinkRiding ? 0 : 0x1c) + _water + _direction);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        Name = $"Dimitri_{_group:x}_{room.Id:x2}";
    }

    internal void BeginIntroResponse()
    {
        if ((_save.ReadWramByte(0xc647) & 1) == 0)
            _phase = DimitriPhase.IntroPending;
    }

    internal void StopAtCarpenterSearchBoundary()
    {
        // carpenter.s:@dimitri redirects biting state $08 through state $0d.
        if (_phase != DimitriPhase.Eating) return;
        _phase = DimitriPhase.Riding;
        _carried.ZFixed = 0;
        SetAnimation(0);
    }

    public bool TryInteract(Player player)
    {
        if (_phase != DimitriPhase.Harassed || _dialogueOpen()) return false;
        Vector2 delta = _precisePosition - player.Position;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) > 24 ||
            delta.Dot(player.FacingVector) <= 0) return false;
        if (_forestInteraction) { ForestButtonPressed = true; return true; }
        if ((_save.ReadWramByte(0xc647) & 2) == 0)
        {
            Show(0x2100, player);
            _phase = DimitriPhase.HarassedDialogue;
        }
        else
        {
            Show(_save.ReadWramByte(0xc610) == 0x0c ? 0x2102 : 0x2101, player);
            _phase = DimitriPhase.RescueDialogue;
        }
        return true;
    }

    public bool BlocksLink(Vector2 linkCenter) => _phase is DimitriPhase.Harassed or DimitriPhase.HarassedDialogue &&
        Math.Abs(linkCenter.X - Position.X) < 12 && Math.Abs(linkCenter.Y - Position.Y) < 8;

    public void UpdatePlayerForcedMovement(Player player)
    {
        if (_phase == DimitriPhase.Mounting && _mountStarted)
            player.NudgeCompanionMountToward(_precisePosition);
        else if (LinkRiding) SynchronizePlayer(player);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Player player = frame.Player;
        bool attack = Input.IsActionPressed("attack") && Input.IsActionJustPressed("attack");
        bool item = Input.IsActionPressed("item") && Input.IsActionJustPressed("item");
        bool attackPressed = attack && (Input.OriginalUpdateActive || !_attackEdge);
        bool itemPressed = item && (Input.OriginalUpdateActive || !_itemEdge);
        _attackEdge = attack; _itemEdge = item;
        // INTERAC_COMPANION_SCRIPTS $71:$06 waits until Dimitri reaches land.
        if (!_goodbye && _data.IsGoodbyeRoom(_group, _room.Id) &&
            (_save.ReadWramByte(0xc647) & 0x40) == 0 && _water == 0 &&
            _phase is DimitriPhase.Riding or DimitriPhase.Waiting)
        {
            _goodbye = true;
            if (LinkRiding) BeginDismount();
            else _phase = DimitriPhase.GoodbyeWaiting;
        }
        switch (_phase)
        {
            case DimitriPhase.Harassed:
                _animation.Advance();
                break;
            case DimitriPhase.IntroPending:
                Show(0x2100, player);
                _phase = DimitriPhase.IntroDialogue;
                break;
            case DimitriPhase.IntroDialogue:
                if (_dialogueOpen()) break;
                SetStateBit(1);
                _phase = DimitriPhase.Harassed;
                break;
            case DimitriPhase.HarassedDialogue:
                if (!_dialogueOpen()) _phase = DimitriPhase.Harassed;
                break;
            case DimitriPhase.RescueDialogue:
                if (_dialogueOpen()) break;
                _rescueMount = true;
                _phase = DimitriPhase.Mounting;
                SetAnimation(0x1c);
                break;
            case DimitriPhase.MountedDialogue:
                if (_dialogueOpen()) break;
                SetStateBit(0x20);
                _phase = DimitriPhase.Riding;
                break;
            case DimitriPhase.Waiting:
                if (!OracleObjectMath.UpdateSpeedZ(ref _carried.ZFixed, ref _carried.SpeedZ, 0x40)) break;
                CheckHazard();
                if (_phase == DimitriPhase.Hazard) break;
                UpdateWater();
                if (!CompanionRuntimeState.MountingDisabled(_runtime) && !player.TopDownAirborne && !player.IsDying && !player.IsDrowning &&
                    !player.IsFallingInHole && Distance(player.Position) < 9)
                    _phase = DimitriPhase.Mounting;
                break;
            case DimitriPhase.Mounting:
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
                CompanionRuntimeState.Begin(_runtime, 0x0c, _room.Id, _precisePosition, _direction);
                SynchronizePlayer(player, finishMount: true);
                if (_rescueMount)
                {
                    _rescueMount = false;
                    Show(0x2106, player);
                    _phase = DimitriPhase.MountedDialogue;
                }
                break;
            case DimitriPhase.Riding:
                if (!OracleObjectMath.UpdateSpeedZ(ref _carried.ZFixed, ref _carried.SpeedZ, 0x40)) break;
                if (attackPressed)
                {
                    _phase = DimitriPhase.Eating; _bitePhase = 0; _swallowed = false;
                    _angle = _direction * 8;
                    SetAnimation(8);
                    _sound(0xc4);
                    spawns.Add(new DimitriMouthSpawn(this, _group, _room.Id));
                    break;
                }
                // companionGotoDismountState rejects dismounts in water.
                if (itemPressed && _water == 0) { BeginDismount(); break; }
                UpdateMovement(spawns);
                if (_phase == DimitriPhase.Riding) CheckHazard();
                break;
            case DimitriPhase.Eating:
                _animation.Advance();
                if (_bitePhase < 2) ApplySpeed(0x1e, collide: false);
                if (_bitePhase == 0 && (_animation.CurrentParameter & 0x80) != 0)
                {
                    _bitePhase = 1; _counter = 12; _angle = (_direction ^ 2) * 8;
                    SetAnimation(0);
                }
                else if (_bitePhase == 1 && --_counter == 0)
                {
                    _angle = _direction * 8;
                    if (_swallowed) { _bitePhase = 2; _counter = 20; SetAnimation(0x10); }
                    else { _phase = DimitriPhase.Riding; SetAnimation(0); }
                }
                else if (_bitePhase == 2 && --_counter == 0)
                { _phase = DimitriPhase.Riding; SetAnimation(0); }
                break;
            case DimitriPhase.Dismounting:
                if (!_dismountStarted)
                {
                    _dismountStarted = true;
                    CompanionRuntimeState.Remember(_runtime, 0x0c, _group, _room.Id, _precisePosition);
                    CompanionRuntimeState.Clear(_runtime, 0x0c);
                    if (_goodbye) CompanionRuntimeState.ForgetRemembered(_runtime);
                    player.BeginCompanionDismount(_precisePosition, _direction);
                    SetAnimation(0x1c);
                }
                else if (!player.CompanionJumpActive)
                {
                    if (!_landingObserved) { _landingObserved = true; break; }
                    _previousLink = player.PrecisePosition;
                    _phase = _goodbye ? DimitriPhase.GoodbyeWaiting : DimitriPhase.AwaitingDistance;
                }
                break;
            case DimitriPhase.AwaitingDistance:
                if (Distance(_previousLink) >= 9) _phase = DimitriPhase.Waiting;
                _previousLink = player.PrecisePosition;
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
                _phase = DimitriPhase.AwaitingDistance;
                _previousLink = player.PrecisePosition;
                SetAnimation(0x1c);
                break;
            case DimitriPhase.ReturningToLand:
                if (!OracleObjectMath.UpdateSpeedZ(ref _carried.ZFixed, ref _carried.SpeedZ, 0x40)) break;
                ApplySpeed(0x28);
                BreakGroundTile(spawns);
                _animation.Advance();
                UpdateWater();
                if (_water == 0) { _phase = DimitriPhase.Waiting; SetAnimation(0x1c); }
                break;
            case DimitriPhase.CliffJump:
                if (_counter > 0)
                {
                    if (--_counter == 0) _sound(OracleSoundEngine.SndJump);
                    break;
                }
                _animation.Advance();
                ApplySpeed(0x50, collide: false);
                OracleObjectMath.UpdateSpeedZ(ref _carried.ZFixed, ref _carried.SpeedZ, 0x40);
                int away = FacingWallMask((_angle + 0x10) & 0x1f, AdjacentWalls());
                if (away != 0) _cliffWalls = away;
                else if (_cliffWalls != 0) { _phase = DimitriPhase.Riding; SetAnimation(0); }
                break;
            case DimitriPhase.FlutePending:
                _phase = DimitriPhase.FluteEntering;
                _counter = 0x3c;
                _sound(0xc4);
                SetAnimation(0);
                break;
            case DimitriPhase.FluteEntering:
                ApplySpeed(_water == 0 ? 0x1e : 0x28);
                BreakGroundTile(spawns);
                _animation.Advance();
                UpdateWater();
                Vector2 ahead = _precisePosition + _native.Probes("carry")[_direction];
                // companionRetIfNotFinishedWalkingIn returns normally at an
                // obstruction, or when the sixty clear-tile updates expire.
                if (_room.GetTerrainInfo(ahead).Collision != 0 || --_counter == 0)
                {
                    _phase = DimitriPhase.Waiting;
                    _direction = 2;
                    SetAnimation(0x1c);
                }
                break;
            case DimitriPhase.Hazard:
                if (_hazard.Advance(ref _precisePosition, _animation, 0x25, 0x25, _animation.SetAnimation, _sound))
                {
                    _precisePosition = CompanionHazard.ResolveRespawn(player, _runtime, CanRespawnAt);
                    if (_hazardMounted) player.ApplyCompanionHazardDamage(_hazard.Type);
                    _phase = _hazardMounted ? DimitriPhase.Riding : DimitriPhase.Waiting;
                    _carried.ZFixed = 0; _carried.SpeedZ = 0;
                    _angle = 0xff; SetAnimation(0);
                }
                break;
            case DimitriPhase.GoodbyeWaiting:
                if (player.TopDownAirborne) break;
                Show(0x2104, player);
                _phase = DimitriPhase.GoodbyeDialogue;
                break;
            case DimitriPhase.GoodbyeDialogue:
                if (_dialogueOpen()) break;
                _direction = 1; _angle = 8; SetAnimation(0);
                _sound(0xc4);
                _phase = DimitriPhase.Leaving;
                break;
            case DimitriPhase.Leaving:
                ApplySpeed(_water == 0 ? 0x1e : 0x28);
                BreakGroundTile(spawns);
                _animation.Advance();
                UpdateWater();
                if (_precisePosition.X >= _room.Width) _phase = DimitriPhase.Finished;
                break;
            case DimitriPhase.Finished:
                break;
            default: throw new InvalidOperationException($"Unsupported Dimitri state {_phase} in {_group:x}:{_room.Id:x2}.");
        }
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        if (LinkRiding)
        {
            CompanionRuntimeState.Update(_runtime, 0x0c, _room.Id, _precisePosition, _direction);
            SynchronizePlayer(player);
        }
        ZIndex = LinkRiding || Position.Y <= player.Position.Y + NpcCharacter.LinkPriorityYOffset
            ? NpcCharacter.BehindLinkZIndex : NpcCharacter.InFrontOfLinkZIndex;
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
            if ((_animation.CurrentParameter & 0x80) != 0) _sound(0x88);
            ApplySpeed(_water == 0 ? 0x1e : 0x28);
            BreakGroundTile(spawns);
            _animation.Advance();
        }
        UpdateWater();
    }

    private void UpdateWater()
    {
        int water = IsWater(_precisePosition) ? 4 : 0;
        if (water != 0 && _room.GetMetatile(_precisePosition) is 0xfe or 0xff)
            _precisePosition.Y += 0.75f; // dimitriAddWaterfallResistance adds $00c0.
        if (water == _water) return;
        _water = water;
        SetAnimation(LinkRiding || _phase is DimitriPhase.Leaving or DimitriPhase.FluteEntering or DimitriPhase.ReturningToLand ? 0 : 0x1c);
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
        _hazard = hazard; _phase = DimitriPhase.Hazard;
    }
    private bool CanRespawnAt(Vector2 position) => CanOccupy(position) &&
        _room.GetTerrainInfo(position).Collision == 0 &&
        !CompanionHazard.TryCreate(_room, position, out _);
    private void BeginDismount()
    {
        _phase = DimitriPhase.Dismounting; _dismountStarted = false; _landingObserved = false;
    }
    private int Distance(Vector2 point) => Math.Abs(Mathf.FloorToInt(point.X) - Mathf.FloorToInt(_precisePosition.X)) +
        Math.Abs(Mathf.FloorToInt(point.Y) - Mathf.FloorToInt(_precisePosition.Y));
    private void ApplySpeed(int speed, bool collide = true)
    {
        Vector2 before = _precisePosition;
        if (collide) CompanionMovement.ApplySpeed(ref _precisePosition, speed, _angle, AdjacentWalls());
        else OracleObjectMovement.Shared.ApplySpeed(ref _precisePosition, speed, _angle);
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

    private bool CollisionAt(Vector2 sample)
    {
        if (sample.X < 0 || sample.Y < 0 || sample.X >= _room.Width || sample.Y >= _room.Height) return false;
        int tile = _room.GetMetatile(sample);
        if (tile >= 0xfe) return false;
        if (tile is 0xd5 or 0xd6) return true;
        int collision = tile == 0xd4 ? 3 : _room.GetTerrainInfo(sample).Collision;
        int x = Mathf.FloorToInt(sample.X) & 15, y = Mathf.FloorToInt(sample.Y) & 15;
        if (collision < 0x10) return (collision & (1 << ((y < 8 ? 2 : 0) + (x < 8 ? 1 : 0)))) != 0;
        int kind = collision & 15;
        return (_native.CollisionMasks[kind] & (1 << ((kind < 8 ? x : y) >> 1))) != 0;
    }

    private int AdjacentWalls()
    {
        int walls = 0;
        var probes = _native.Probes("collision");
        for (int index = 0; index < probes.Count; index++)
            if (CollisionAt(_precisePosition + probes[index])) walls |= 1 << (7 - index);
        return walls;
    }

    private static int FacingWallMask(int angle, int walls)
    {
        if (angle == 0xff) return 0;
        int mask = 0;
        if (angle is not (8 or 0x18)) mask |= ((angle >> 3) & 3) == 0 ? 0xc0 : 0x30;
        if ((angle & 15) != 0) mask |= (angle & 0x10) == 0 ? 3 : 0x0c;
        return walls & mask;
    }

    private bool TryStartCliffJump()
    {
        if ((_angle & 0xe7) != 0 || FacingWallMask(_angle, AdjacentWalls()) is not (3 or 0x0c or 0x30)) return false;
        byte tile = _room.GetMetatile(_precisePosition + _native.Probes("cliff")[_direction]);
        if (tile == 0xd4 ? _angle != 0x10 : !_ledges.IsCliffTile(_room.ActiveCollisions, tile, _angle)) return false;
        _phase = DimitriPhase.CliffJump;
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
            if (FacingWallMask(angle, AdjacentWalls()) != 0) return false;
            ReleaseCarried(player, releaseDirection);
            return true;
        }
        if (_phase is not (DimitriPhase.Waiting or DimitriPhase.AwaitingDistance) ||
            player.IsCarryingObject || player.TopDownAirborne || _carried.ZFixed != 0 || !CanBeCarried(player)) return false;
        Vector2 point = OracleObjectMath.ToPixelPosition(player.Position) + player.FacingVector * NpcCharacter.AButtonPointOffset;
        Vector2 difference = _precisePosition - point;
        if (difference.X is < -6 or >= 6 || difference.Y is < -6 or >= 6) return false;
        _phase = DimitriPhase.Carried;
        _holder = player;
        _water = 0;
        _direction = CarriedObjectMotion.DirectionIndex(player.FacingVector);
        CompanionRuntimeState.Begin(_runtime, 0x0c, _room.Id, _precisePosition, _direction, updateMountPoint: false);
        SetAnimation(0x18);
        _carried = new CarriedObjectMotion(_precisePosition);
        player.BeginCarriedObjectPose();
        _save.WriteWramByte(0xc649, (byte)(_save.ReadWramByte(0xc649) | 4));
        _carried.Hold(player);
        _precisePosition = _carried.GroundPosition;
        return true;
    }

    private bool CanBeCarried(Player player, bool initialGrab = true)
    {
        int collision = _room.GetTerrainInfo(player.Position).Collision;
        if ((initialGrab && collision is 0x0c or 0x0f or 0x11 or 0x19) ||
            _room.GetMetatile(player.Position) is 0xd5 or 0xd6) return false;
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
        return _room.GetMetatile(point) is not (0xd4 or 0xd5 or 0xd6) &&
            _room.GetTerrainInfo(point).Collision is not (0x0c or 0x11 or 0x19);
    }

    private void UpdateCarried(Player player)
    {
        if (!CanBeCarried(player, initialGrab: false)) { ReleaseCarried(player, Vector2I.Zero); return; }
        if (CompanionMovement.AngleForInput(Input.GetVector("move_left", "move_right", "move_up", "move_down")) != 0xff)
            _direction = CarriedObjectMotion.DirectionIndex(player.FacingVector);
        _carried.Hold(player);
        _precisePosition = _carried.GroundPosition;
        CompanionRuntimeState.Update(_runtime, 0x0c, _room.Id, _precisePosition, _direction);
    }

    private void ReleaseCarried(Player player, Vector2I direction)
    {
        _carried.Release(player, direction, _bracelet);
        _precisePosition = _carried.GroundPosition;
        _throwOrigin = OracleObjectMath.ToPixelPosition(player.Position);
        _phase = DimitriPhase.Thrown;
        _holder = null;
        CompanionRuntimeState.Clear(_runtime, 0x0c);
    }

    private void UpdateThrown(Player player)
    {
        CheckHazard();
        if (_phase == DimitriPhase.Hazard) return;
        bool stopped = FacingWallMask(CompanionMovement.AngleForInput(_carried.Direction), AdjacentWalls()) != 0;
        // dimitriState2Substate2 deliberately tests group zero, not room size.
        float maxX = _group == 0 ? 155 : 239, maxY = _group == 0 ? 122 : 168;
        if (_precisePosition.Y < 8) { _precisePosition.Y = 16; stopped = true; }
        if (_precisePosition.Y >= maxY) { _precisePosition.Y = maxY; stopped = true; }
        if (_precisePosition.X < 4) { _precisePosition.X = 4; stopped = true; }
        if (_precisePosition.X >= maxX) { _precisePosition.X = maxX; stopped = true; }
        _carried.GroundPosition = _precisePosition;
        if (stopped) _carried.SpeedRaw = 0;
        _carried.AdvanceHorizontal(_throwing, static _ => false);
        _precisePosition = _carried.GroundPosition;
        bool landed = _carried.AdvanceVertical(_bracelet);
        if (landed && IsWater(_precisePosition))
        {
            _angle = OracleObjectMovement.Shared.RelativeAngle(_precisePosition, _throwOrigin) & 0x18;
            _direction = _angle >> 3;
            _water = 4;
            SetAnimation(0);
            _phase = DimitriPhase.ReturningToLand;
        }
        else if (landed && !_carried.Bounce(_throwing))
            _phase = DimitriPhase.ThrownLanding;
    }
    private void SetAnimation(int animationBase) => _animation.SetAnimation(animationBase + _direction + _water);
    private void SetStateBit(byte mask)
    {
        if (_save.WriteWramByte(0xc647, (byte)(_save.ReadWramByte(0xc647) | mask))) _save.CommitInventoryChange();
    }
    private void Show(int textId, Player player) => _showText(textId, _data.Text(textId), player.Position);
    internal void Swallow() => _swallowed = true;
    internal bool CanSwallow(int mode) => _data.CanSwallow(mode);
    internal bool AcceptsMouthCollision(int type) => _data.AcceptsMouthCollision(type);

    private void SynchronizePlayer(Player player, Vector2 screenOffset = default, bool finishMount = false)
    {
        int parameter = _animation.CurrentParameter & 0x3f;
        if ((uint)parameter >= _data.LinkTextures.Length)
            throw new InvalidOperationException($"Dimitri animation emitted unsupported Link parameter ${parameter:x2}.");
        // specialObjects.s:func_410d@ridingDimitri, signed BC offsets.
        Vector2 offset = new(_direction == 1 ? -5 : _direction == 3 ? 5 : 0, -10);
        if (finishMount) player.FinishCompanionMount(_precisePosition, offset, _direction, _carried.ZFixed,
            _data.LinkTextures[parameter], _data.LinkDamageTextures[parameter], _data.LinkOffsets[parameter]);
        else player.SetCompanionRidePosition(_precisePosition, offset, _direction, _carried.ZFixed,
            _data.LinkTextures[parameter], _data.LinkDamageTextures[parameter], _data.LinkOffsets[parameter], screenOffset);
    }
    public override void _Draw() => DrawTexture(_animation.CurrentTexture,
        _animation.CurrentOffset + SourceOamDrawOffset + new Vector2(0, _carried.ZFixed >> 8));
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    void ICompanionBarrierTarget.ClampToLowerY(int y) { if (_precisePosition.Y > y) _precisePosition.Y = y; }
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
        CompanionRuntimeState.Update(_runtime, 0x0c, _room.Id, _precisePosition, _direction);
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
            CompanionRuntimeState.Update(_runtime, 0x0c, _room.Id, _precisePosition, _direction);
            return;
        }
        _destination = null; _precisePosition = position; Position = OracleObjectMath.ToPixelPosition(position);
        player.SetLocalRespawnPosition(Position);
        CompanionRuntimeState.SetLastAnimalMountPosition(_runtime, Position);
        CompanionRuntimeState.Update(_runtime, 0x0c, _room.Id, position, _direction);
        SynchronizePlayer(player);
    }
}

internal enum DimitriPhase
{
    Harassed, IntroPending, IntroDialogue, HarassedDialogue, RescueDialogue,
    Mounting, MountedDialogue, Waiting, Riding, Eating, Dismounting,
    AwaitingDistance, Hazard, GoodbyeWaiting, GoodbyeDialogue, Leaving, Finished,
    Carried, Thrown, ThrownLanding, ReturningToLand, CliffJump, FlutePending, FluteEntering
}

internal sealed record DimitriCompanionSpawn(Vector2 Position, int Direction, int Group, int Room,
    bool Riding = false, Vector2? FluteDestination = null) : RoomEntitySpawn;
