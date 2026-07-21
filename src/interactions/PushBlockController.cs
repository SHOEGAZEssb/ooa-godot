using Godot;
using System;

namespace oracleofages;

public partial class PushBlockController : Node2D
{
    public const int PushDelayFrames = 20;
    public const int MoveFrames = 32;
    public const float MoveSpeedPerFrame = 0.5f;
    private const float CombinedLinkRadius = 12.0f;

    private readonly RoomSession _rooms;
    private readonly PushableTileDatabase _tiles;
    private readonly RoomView _roomView;
    private readonly Func<long> _animationTick;
    private readonly Action<int> _playSound;
    private int _pushCounter = PushDelayFrames;
    private int _candidatePosition = -1;
    private Vector2I _candidateDirection;
    private bool _active;
    private float _moveFrame;
    private Vector2 _sourceTopLeft;
    private Vector2 _destinationTopLeft;
    private Vector2 _collisionCenter;
    private Vector2I _moveDirection;
    private byte _destinationBackground;
    private PushableTileDatabase.PushableTileRecord _record;
    private Texture2D? _blockTexture;

    public event Action<Vector2, OracleRoomData.HazardType>? EnteredHazard;

    public bool Active => _active;
    internal int RemainingPushFrames => _pushCounter;
    internal float MoveFrame => _moveFrame;
    internal Vector2 BlockTopLeft => _sourceTopLeft + (Vector2)_moveDirection *
        (_moveFrame * MoveSpeedPerFrame);

    public PushBlockController(
        RoomSession rooms,
        PushableTileDatabase tiles,
        RoomView roomView,
        Func<long> animationTick,
        Action<int> playSound)
    {
        _rooms = rooms;
        _tiles = tiles;
        _roomView = roomView;
        _animationTick = animationTick;
        _playSound = playSound;
        ZIndex = 9;
        _rooms.RoomChanged += (_, _) => Cancel();
    }

    public void UpdatePushAttempt(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput,
        bool hasBracelet = false)
    {
        if (_active)
            return;

        if (!InteractableTilePushGeometry.TryGetCardinalInput(
                movementInput, out Vector2I direction) || direction != facing ||
            !TryGetCandidate(linkPosition, direction, hasBracelet, out int position,
                out Vector2 topLeft, out byte tile,
                out PushableTileDatabase.PushableTileRecord record))
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

        StartMovement(topLeft, tile, direction, record);
    }

    public override void _PhysicsProcess(double delta) => Advance(delta);

    internal void Advance(double delta)
    {
        if (_active)
            AdvanceMovement(delta);
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
        out PushableTileDatabase.PushableTileRecord record)
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
        if (tile == 0xff || !_tiles.TryGet(room.ActiveCollisions, tile, out record) ||
            (record.RequiresBracelet && !hasBracelet) ||
            (!record.AllowsEveryDirection && record.RequiredDirection !=
                InteractableTilePushGeometry.DirectionIndex(direction)))
        {
            return false;
        }

        Vector2 targetPoint = topLeft + (Vector2)direction * OracleRoomData.MetatileSize +
            Vector2.One * (OracleRoomData.MetatileSize / 2.0f);
        byte destination = room.GetMetatile(targetPoint);
        return destination != 0xff && (room.GetCollision(destination) & 0x0f) == 0;
    }

    private void StartMovement(
        Vector2 topLeft,
        byte tile,
        Vector2I direction,
        PushableTileDatabase.PushableTileRecord record)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Image image = room.Texture.GetImage().GetRegion(
            new Rect2I((Vector2I)topLeft, Vector2I.One * OracleRoomData.MetatileSize));
        _blockTexture = ImageTexture.CreateFromImage(image);
        _sourceTopLeft = topLeft;
        _destinationTopLeft = topLeft + (Vector2)direction * OracleRoomData.MetatileSize;
        _collisionCenter = topLeft + new Vector2(8, 6);
        _moveDirection = direction;
        _record = record;
        _destinationBackground = room.GetMetatile(_destinationTopLeft + Vector2.One * 8.0f);
        _moveFrame = 0.0f;
        _active = true;
        Visible = true;

        byte original = room.GetOriginalMetatile(topLeft + Vector2.One * 8.0f);
        byte originalCollision = room.GetCollision(original);
        byte replacement = originalCollision == 0 || originalCollision >= 0x10
            ? original
            : record.SourceReplacement;
        room.ReplaceMetatile(topLeft + Vector2.One * 8.0f, tile, replacement, _animationTick());
        _roomView.QueueRedraw();
        _playSound(OracleSoundEngine.SndMoveBlock);
        QueueRedraw();
    }

    private void AdvanceMovement(double delta)
    {
        _moveFrame = Mathf.Min(MoveFrames, _moveFrame + (float)(delta * 60.0));
        _collisionCenter = _sourceTopLeft + new Vector2(8, 6) +
            (Vector2)_moveDirection * (_moveFrame * MoveSpeedPerFrame);
        QueueRedraw();
        if (_moveFrame < MoveFrames)
            return;

        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 destinationCenter = _destinationTopLeft + Vector2.One * 8.0f;
        OracleRoomData.HazardType hazard = room.GetTerrainInfo(destinationCenter).Hazard;
        if (hazard != OracleRoomData.HazardType.None)
        {
            EnteredHazard?.Invoke(_collisionCenter, hazard);
        }
        else if (_record.DestinationTile != 0)
        {
            room.ReplaceMetatile(
                destinationCenter, _destinationBackground, _record.DestinationTile, _animationTick());
            _roomView.QueueRedraw();
        }
        Cancel();
    }

    private void ResetPushCounter()
    {
        _pushCounter = PushDelayFrames;
        _candidatePosition = -1;
        _candidateDirection = Vector2I.Zero;
    }

}
