using Godot;
using System;

namespace oracleofages;

public partial class PushBlockController : Node2D
{
    public const int PushDelayFrames = 20;
    public const int MoveFrames = 32;
    public const float MoveSpeedPerFrame = 0.5f;
    private const float CombinedLinkRadius = 12.0f;
    private const byte GraveHidingDoorTile = 0xd9;
    private const byte TilesetFlagOutdoors = 0x01;

    private readonly RoomSession _rooms;
    private readonly OracleRuntimeState? _movementMemory;
    private int _activeSpeedRaw;
    private readonly PushableTileDatabase _tiles;
    private readonly RoomView _roomView;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private readonly Func<byte, bool> _pushBlockPermitted;
    private readonly Action<int>? _pushSomaria;
    private readonly Func<int>? _braceletLevelSource;
    private int _pendingPosition = -1, _pendingAngle, _pendingBraceletLevel;
    private int _pushCounter = PushDelayFrames;
    private int _candidatePosition = -1;
    private Vector2I _candidateDirection;
    private bool _active;
    private bool _outgoing;
    private float _moveFrame;
    private Vector2 _sourceTopLeft;
    private Vector2 _destinationTopLeft;
    private Vector2 _collisionCenter;
    private Vector2I _moveDirection;
    private PushableTileRecord _record;
    private Texture2D? _blockTexture;
    private bool _linkMovementDisabled;
    private readonly BraceletDatabaseRecord _bracelet = new BraceletDatabase().Data;
    private int _activeMoveFrames = MoveFrames;
    private float _activeMoveSpeedPerFrame = MoveSpeedPerFrame;

    public event Action<Vector2, HazardType>? EnteredHazard;

    public bool Active => _active;
    internal byte ActiveTile { get; private set; }
    internal int PushAngle => _rooms.BlockPushAngle & 0x1f;
    internal bool NativeInitialized => _active && _pendingPosition < 0;
    internal bool LinkMovementDisabled => _active && _linkMovementDisabled;
    internal int RemainingPushFrames => _pushCounter;
    internal int ActiveMoveFrames => _activeMoveFrames;
    internal float ActiveMoveSpeedPerFrame => _activeMoveSpeedPerFrame;
    internal Vector2 BlockTopLeft => _sourceTopLeft + (Vector2)_moveDirection *
        (_moveFrame * _activeMoveSpeedPerFrame);
    internal Texture2D? BlockTexture => _blockTexture;
    internal int BlockZHigh { get; private set; }

    public PushBlockController(
        RoomSession rooms,
        PushableTileDatabase tiles,
        RoomView roomView,
        Func<long> animationTick,
        Action<int> playSound,
        Func<byte, bool>? pushBlockPermitted = null,
        Action<int>? pushSomaria = null,
        bool observeRoomChanges = true,
        Func<int>? braceletLevelSource = null,
        OracleRuntimeState? movementMemory = null)
    {
        _rooms = rooms;
        _movementMemory = movementMemory;
        _tiles = tiles;
        _roomView = roomView;
        _animationTick = animationTick;
        _playSound = playSound;
        _pushBlockPermitted = pushBlockPermitted ?? (_ => true);
        _pushSomaria = pushSomaria;
        _braceletLevelSource = braceletLevelSource;
        ZIndex = 9;
        if (observeRoomChanges) _rooms.RoomChanged += (_, _) => { if (!_outgoing) Cancel(); };
    }

    internal PushBlockController CreateSynchronizedController()
    {
        var child = new PushBlockController(_rooms,_tiles,_roomView,_animationTick,_playSound,
            _pushBlockPermitted,observeRoomChanges:false,braceletLevelSource:_braceletLevelSource,
            movementMemory:_movementMemory);
        child.EnteredHazard += (position,hazard) => EnteredHazard?.Invoke(position,hazard);
        return child;
    }

    internal void StartNativeMovement(byte position,int angle,int braceletLevel)
    {
        Vector2 topLeft = new((position & 15) * 16,(position >> 4) * 16);
        byte tile = _rooms.CurrentRoom.GetMetatile(topLeft + Vector2.One * 8);
        if (!_pushBlockPermitted(tile)) { Cancel(); return; }
        // @loadPushableTileProperties returns at its zero terminator on a
        // miss, leaving the freshly allocated var32..var34 bytes zero. A
        // pending state0 can observe a tile changed since its allocation.
        _tiles.TryGet(_rooms.CurrentRoom.ActiveCollisions,tile,out var record);
        Vector2I direction = (angle & 0x1f) switch {
            0 => Vector2I.Up, 8 => Vector2I.Right, 16 => Vector2I.Down, 24 => Vector2I.Left,
            _ => throw new NotSupportedException($"INTERAC $14: unsupported push angle ${angle:x2}.") };
        StartMovement(topLeft,tile,direction,record,braceletLevel,dispatchSomaria: false);
    }

    public void UpdatePushAttempt(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput,
        int braceletLevel = 0)
    {
        // nextToPushableBlock returns before touching the shared counter
        // underwater. ITEM$18 dispatch precedes reserved INTERAC_PUSHBLOCK.
        if ((_rooms.CurrentRoom.TilesetFlags & 0x40) != 0)
            return;
        if (_active && !_tiles.TryGetSomaria(_rooms.CurrentRoom.ActiveCollisions,
            _rooms.CurrentRoom.GetMetatile(linkPosition+InteractableTilePushGeometry.FrontTileOffset(facing)),out _))
            return;

        if (!InteractableTilePushGeometry.TryGetCardinalInput(
                movementInput, out Vector2I direction) || direction != facing ||
            !TryGetCandidate(linkPosition, direction, braceletLevel > 0, out int position,
                out Vector2 topLeft, out byte tile,
                out PushableTileRecord record))
        {
            ResetPushCounter();
            return;
        }

        if (_candidatePosition != position || _candidateDirection != direction)
        {
            _candidatePosition = position;
            _candidateDirection = direction;
            _pushCounter = PushDelayFrames;
        }

        _pushCounter--;
        if (_pushCounter > 0)
            return;

        if (_tiles.TryGetSomaria(_rooms.CurrentRoom.ActiveCollisions, tile, out _))
        {
            StartMovement(topLeft, tile, direction, record, braceletLevel);
            return;
        }
        // nextToPushableBlock checks the destination only when the contact
        // counter expires. A changing obstruction must not restart it early.
        Vector2 target = topLeft + (Vector2)direction * OracleRoomData.MetatileSize +
            Vector2.One * (OracleRoomData.MetatileSize / 2.0f);
        OracleRoomData room = _rooms.CurrentRoom;
        ResetPushCounter();
        if (room.GetMetatile(target) == 0xff || (room.GetTerrainInfo(target).Collision & 0x0f) != 0)
            return;
        // nextToPushableBlock allocates reserved $d1; INTERAC$14 state0
        // owns the later tile read, graphics, sound and shared direction.
        _pendingPosition = position;
        _pendingAngle = InteractableTilePushGeometry.DirectionIndex(direction) * 8;
        _pendingBraceletLevel = braceletLevel;
        _sourceTopLeft = topLeft;
        _moveDirection = direction;
        _moveFrame = 0;
        ActiveTile = 0;
        _blockTexture = null;
        Visible = false;
        _active = true;
        _linkMovementDisabled = tile == GraveHidingDoorTile &&
            (_rooms.CurrentRoom.TilesetFlags & TilesetFlagOutdoors) != 0;
    }

    public override void _PhysicsProcess(double delta) => Advance(delta);

    internal void Advance(double delta, Player? player = null)
    {
        if (!_active) return;
        bool initializing = _pendingPosition >= 0;
        if (initializing)
        {
            byte position = (byte)_pendingPosition;
            _pendingPosition = -1;
            StartNativeMovement(position, _pendingAngle, _braceletLevelSource?.Invoke() ?? _pendingBraceletLevel);
        }
        if (_active && (initializing || !_outgoing)) AdvanceMovement(delta,player);
    }

    // setInteractionsEnabledTo2 includes reserved interaction $d1. An
    // initialized $14 keeps its sprite/counters until scroll bulk cleanup.
    internal void BeginScreenTransition() => _outgoing = _active;
    internal void UpdateDuringScreenTransition(Player player)
    {
        if (_outgoing && !NativeInitialized) Advance(1.0 / 60.0, player);
    }
    internal void SetScreenTransitionOffset(Vector2 offset)
    {
        if (_outgoing) Position = offset;
    }
    internal void FinishScreenTransition()
    {
        if (_outgoing) Cancel();
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        if (!_active || !NativeInitialized)
            return false;
        Vector2 delta = linkCenter - _collisionCenter;
        return Mathf.Abs(delta.X) < CombinedLinkRadius &&
            Mathf.Abs(delta.Y) < CombinedLinkRadius;
    }

    public void Cancel()
    {
        _outgoing = false;
        Position = Vector2.Zero;
        _active = false;
        _pendingPosition = -1;
        _linkMovementDisabled = false;
        _moveFrame = 0.0f;
        _blockTexture = null;
        BlockZHigh = 0;
        Visible = false;
        ResetPushCounter();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_active && _blockTexture is not null)
            DrawTexture(_blockTexture, BlockTopLeft + new Vector2(0, BlockZHigh));
    }

    private bool TryGetCandidate(
        Vector2 linkPosition,
        Vector2I direction,
        bool hasBracelet,
        out int position,
        out Vector2 topLeft,
        out byte tile,
        out PushableTileRecord record)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        record = default;
        if (!InteractableTilePushGeometry.IsAlignedForPush(linkPosition))
        {
            position = -1;
            topLeft = Vector2.Zero;
            tile = 0xff;
            return false;
        }
        Vector2 frontPoint = linkPosition +
            InteractableTilePushGeometry.FrontTileOffset(direction);
        position = room.GetPackedPosition(frontPoint);
        int tileX = position & 0x0f;
        int tileY = position >> 4;
        topLeft = new Vector2(
            tileX * OracleRoomData.MetatileSize,
            tileY * OracleRoomData.MetatileSize);
        tile = room.GetMetatile(frontPoint);
        bool somaria=_tiles.TryGetSomaria(room.ActiveCollisions,tile,out byte parameter);
        // ITEM$18 has an interaction parameter but no ordinary replacement,
        // destination, or property bytes. StartMovement dispatches it first.
        if(somaria) record=new(parameter,0,0,0);
        if (tile == 0xff || !somaria && !_tiles.TryGet(room.ActiveCollisions, tile, out record) ||
            !_pushBlockPermitted(tile) ||
            (record.RequiresBracelet && !hasBracelet) ||
            (!record.AllowsEveryDirection && record.RequiredDirection !=
                InteractableTilePushGeometry.DirectionIndex(direction)))
        {
            return false;
        }

        return true; // Destination is tested after the push countdown.
    }

    private void StartMovement(
        Vector2 topLeft,
        byte tile,
        Vector2I direction,
        PushableTileRecord record,
        int braceletLevel,
        bool dispatchSomaria = true)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        if (dispatchSomaria && _tiles.TryGetSomaria(room.ActiveCollisions,tile,out _))
        {
            Vector2 target=topLeft+(Vector2)direction*16+Vector2.One*8;
            if ((room.GetTerrainInfo(target).Collision&15)==0)
                (_pushSomaria ?? throw new InvalidOperationException("nextToPushableBlock ITEM$18 dispatch has no item owner."))(
                    InteractableTilePushGeometry.DirectionIndex(direction));
            ResetPushCounter();
            return;
        }
        // INTERAC_PUSHBLOCK state 0 passes the explicit source metatile to
        // objectMimicBgTile. Color 0 must therefore be transparent; copying
        // the opaque room region makes the ground around a pot move with it.
        _blockTexture = room.BuildMimickedMetatileTexture(tile);
        _sourceTopLeft = topLeft;
        _destinationTopLeft = topLeft + (Vector2)direction * OracleRoomData.MetatileSize;
        _collisionCenter = topLeft + new Vector2(8, 6);
        _moveDirection = direction;
        _record = record;
        ActiveTile = tile; // INTERAC $14 var31, observed by INTERAC $bd.
        bool usePowerGloveSpeed = braceletLevel == 2 &&
            (record.PropertyFlags & _bracelet.HeavyPropertyMask) == 0;
        _activeMoveFrames = usePowerGloveSpeed
            ? _bracelet.PowerGlovePushFrames
            : _bracelet.PushFrames;
        int speedRaw = usePowerGloveSpeed
            ? _bracelet.PowerGlovePushSpeedRaw
            : _bracelet.PushSpeedRaw;
        _activeSpeedRaw = speedRaw;
        Vector2 objectDelta = OracleObjectMovement.Shared.Delta(
            speedRaw, InteractableTilePushGeometry.DirectionIndex(direction) * 8);
        _activeMoveSpeedPerFrame = Math.Abs(
            direction.X != 0 ? objectDelta.X : objectDelta.Y);
        _rooms.WriteBlockPushAngle(InteractableTilePushGeometry.DirectionIndex(direction) * 8);
        _moveFrame = 0.0f;
        _active = true;
        Visible = true;

        byte underlying = room.GetUnderlyingMetatile(topLeft + Vector2.One * 8.0f);
        byte originalCollision = room.GetCollision(underlying);
        byte replacement = originalCollision == 0 || originalCollision >= 0x10
            ? underlying
            : record.SourceReplacement;
        _rooms.TrySetTile((byte)room.GetPackedPosition(topLeft + Vector2.One * 8),replacement);
        _roomView.QueueRedraw();
        _playSound(OracleSoundEngine.SndMoveBlock);
        QueueRedraw();
    }

    private void AdvanceMovement(double delta, Player? player)
    {
        // INTERAC$14 samples its object position before applying speed.
        // Button height is presentation; collision and destination stay XY.
        if ((_rooms.CurrentRoom.TilesetFlags & 0x18) != 0)
            BlockZHigh = _rooms.CurrentRoom.GetMetatile(_collisionCenter) == 0x0c ? -2 : 0;
        NativeObjectMovement.Velocity(_movementMemory, _activeSpeedRaw,
            InteractableTilePushGeometry.DirectionIndex(_moveDirection) * 8);
        _moveFrame = Mathf.Min(
            _activeMoveFrames, _moveFrame + (float)(delta * 60.0));
        _collisionCenter = _sourceTopLeft + new Vector2(8, 6) +
            (Vector2)_moveDirection * (_moveFrame * _activeMoveSpeedPerFrame);
        if (player is not null && !player.PassesNpcs)
        {
            // objectPreventLinkFromPassing: byte XY, 6+6 radii, horizontal
            // tie break, coordinate-high write retaining Link's fraction.
            Vector2 link = player.Position.Floor(), block = _collisionCenter.Floor();
            int rx = (byte)((int)link.X - (int)block.X + 12);
            int ry = (byte)((int)link.Y - (int)block.Y + 12);
            if (rx < 24 && ry < 24)
            {
                bool horizontal = 12 - Math.Abs(link.Y-block.Y) >= 12 - Math.Abs(link.X-block.X);
                int origin = (int)(horizontal ? block.X : block.Y);
                int coordinate = (int)(horizontal ? link.X : link.Y);
                player.SetScriptedCoordinateHigh(horizontal,(byte)(origin + (coordinate > origin ? 12 : -12)));
            }
        }
        QueueRedraw();
        if (_moveFrame < _activeMoveFrames)
            return;

        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 destinationCenter = _destinationTopLeft + Vector2.One * 8.0f;
        HazardType hazard = room.GetTerrainInfo(destinationCenter).Hazard;
        if (hazard != HazardType.None)
        {
            EnteredHazard?.Invoke(_collisionCenter, hazard);
        }
        else if (_record.DestinationTile != 0)
        {
            _rooms.TrySetTile((byte)room.GetPackedPosition(destinationCenter),_record.DestinationTile);
            _roomView.QueueRedraw();
        }
        if (hazard == HazardType.None && _record.PlaysSecretSound)
            _playSound(OracleSoundEngine.SndSolvePuzzle);
        Cancel();
    }

    private void ResetPushCounter()
    {
        _pushCounter = PushDelayFrames;
        _candidatePosition = -1;
        _candidateDirection = Vector2I.Zero;
    }

}
