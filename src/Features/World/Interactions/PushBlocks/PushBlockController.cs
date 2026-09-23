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
    private readonly PushableTileDatabase _tiles;
    private readonly RoomView _roomView;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private readonly Func<byte, bool> _pushBlockPermitted;
    private readonly Action<int>? _pushSomaria;
    private int _pushCounter = PushDelayFrames;
    private int _candidatePosition = -1;
    private Vector2I _candidateDirection;
    private bool _active;
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
    internal bool NativeInitialized => _moveFrame != 0;
    internal bool LinkMovementDisabled => _active && _linkMovementDisabled;
    internal int RemainingPushFrames => _pushCounter;
    internal int ActiveMoveFrames => _activeMoveFrames;
    internal float ActiveMoveSpeedPerFrame => _activeMoveSpeedPerFrame;
    internal Vector2 BlockTopLeft => _sourceTopLeft + (Vector2)_moveDirection *
        (_moveFrame * _activeMoveSpeedPerFrame);
    internal Texture2D? BlockTexture => _blockTexture;

    public PushBlockController(
        RoomSession rooms,
        PushableTileDatabase tiles,
        RoomView roomView,
        Func<long> animationTick,
        Action<int> playSound,
        Func<byte, bool>? pushBlockPermitted = null,
        Action<int>? pushSomaria = null,
        bool observeRoomChanges = true)
    {
        _rooms = rooms;
        _tiles = tiles;
        _roomView = roomView;
        _animationTick = animationTick;
        _playSound = playSound;
        _pushBlockPermitted = pushBlockPermitted ?? (_ => true);
        _pushSomaria = pushSomaria;
        ZIndex = 9;
        if (observeRoomChanges) _rooms.RoomChanged += (_, _) => Cancel();
    }

    internal PushBlockController CreateSynchronizedController()
    {
        var child = new PushBlockController(_rooms,_tiles,_roomView,_animationTick,_playSound,
            _pushBlockPermitted,observeRoomChanges:false);
        child.EnteredHazard += (position,hazard) => EnteredHazard?.Invoke(position,hazard);
        return child;
    }

    internal void StartNativeMovement(byte position,int angle,int braceletLevel)
    {
        Vector2 topLeft = new((position & 15) * 16,(position >> 4) * 16);
        byte tile = _rooms.CurrentRoom.GetMetatile(topLeft + Vector2.One * 8);
        if (!_pushBlockPermitted(tile)) return;
        if (!_tiles.TryGet(_rooms.CurrentRoom.ActiveCollisions,tile,out var record))
            throw new NotSupportedException($"INTERAC $14 at ${position:x2}: missing push properties for tile ${tile:x2}.");
        Vector2I direction = (angle & 0x1f) switch {
            0 => Vector2I.Up, 8 => Vector2I.Right, 16 => Vector2I.Down, 24 => Vector2I.Left,
            _ => throw new NotSupportedException($"INTERAC $14: unsupported push angle ${angle:x2}.") };
        StartMovement(topLeft,tile,direction,record,braceletLevel);
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

        StartMovement(topLeft, tile, direction, record, braceletLevel);
    }

    public override void _PhysicsProcess(double delta) => Advance(delta);

    internal void Advance(double delta, Player? player = null)
    {
        if (_active)
            AdvanceMovement(delta,player);
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        if (!_active)
            return false;
        Vector2 delta = linkCenter - _collisionCenter;
        return Mathf.Abs(delta.X) < CombinedLinkRadius &&
            Mathf.Abs(delta.Y) < CombinedLinkRadius;
    }

    public void Cancel()
    {
        _active = false;
        _linkMovementDisabled = false;
        _moveFrame = 0.0f;
        _blockTexture = null;
        Visible = false;
        ResetPushCounter();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_active && _blockTexture is not null)
            DrawTexture(_blockTexture, BlockTopLeft);
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

        if (somaria) return true; // Destination is tested after the push countdown.
        Vector2 targetPoint = topLeft + (Vector2)direction * OracleRoomData.MetatileSize +
            Vector2.One * (OracleRoomData.MetatileSize / 2.0f);
        byte destination = room.GetMetatile(targetPoint);
        return destination != 0xff && (room.GetTerrainInfo(targetPoint).Collision & 0x0f) == 0;
    }

    private void StartMovement(
        Vector2 topLeft,
        byte tile,
        Vector2I direction,
        PushableTileRecord record,
        int braceletLevel)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        if (_tiles.TryGetSomaria(room.ActiveCollisions,tile,out _))
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
        // interactableTiles.s disables Link when the outdoor
        // TILEINDEX_GRAVE_HIDING_DOOR $d9 begins moving. The matching
        // pushableTiles.s property $85 clears that lock at completion.
        _linkMovementDisabled = tile == GraveHidingDoorTile &&
            (room.TilesetFlags & TilesetFlagOutdoors) != 0;
        bool usePowerGloveSpeed = braceletLevel >= 2 &&
            (record.PropertyFlags & _bracelet.HeavyPropertyMask) == 0;
        _activeMoveFrames = usePowerGloveSpeed
            ? _bracelet.PowerGlovePushFrames
            : _bracelet.PushFrames;
        int speedRaw = usePowerGloveSpeed
            ? _bracelet.PowerGlovePushSpeedRaw
            : _bracelet.PushSpeedRaw;
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
