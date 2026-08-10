using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ENEMY_VINE_SPROUT $62. The resting sprout temporarily owns its metatile's
/// layout/collision entry; a valid 20-update push restores that tile, moves at
/// SPEED_c0 for $16 updates, centers, and stores the new global position.
/// </summary>
internal sealed partial class VineSproutRoomEntity : TransitionOffsetNode2D,
    IRoomEntity,
    IFixedRoomEntity,
    IRoomBlocker,
    IRoomPushableEntity,
    IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity
{
    private const float CombinedLinkRadius = 12.0f;
    private readonly VineSproutDatabase _database;
    private readonly VineSproutRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly EnemyAnimationPlayer _animation;
    private int _pushCounter;
    private int _moveCounter;
    private Vector2 _moveDelta;
    private byte _underlyingTile;
    private int _occupiedPackedPosition = -1;
    private bool _initialized;
    private bool _moving;

    public Node2D Node => this;
    internal bool Moving => _moving;
    internal int PushCounter => _pushCounter;
    internal int MoveCounter => _moveCounter;
    internal int PersistedPosition =>
        _save.ReadWramByte(VineSproutDatabase.PositionAddress + _record.SubId);
    public bool Finished { get; private set; }

    internal VineSproutRoomEntity(
        VineSproutDatabase database,
        VineSproutRecord record,
        OracleRoomData room,
        OracleSaveData save,
        Vector2 position,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _database = database;
        _record = record;
        _room = room;
        _save = save;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _pushCounter = record.PushDelay;
        Position = position;
        Name = $"VineSprout_{record.SubId:x2}";
        ZIndex = NpcCharacter.BehindLinkZIndex;
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(
            EnemyVisualSource.LoadComposite([record.Sprite]),
            [record.Animation],
            record.TileBase,
            record.Palette,
            sourceGrayscaleInverted: record.SourceGrayscaleInverted);
        _animation.SetAnimation(0);
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        Visible = true;
        return ScreenTransitionPresentation.Visible;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        _ = spawns;
        if (!_initialized)
        {
            _initialized = true;
            OccupyTile();
            return;
        }
        if (!_moving)
        {
            UpdateResting(frame.Player, spawns);
            return;
        }

        Position += _moveDelta;
        _moveCounter--;
        QueueRedraw();
        if (_moveCounter != 0)
            return;

        Position = CenterOnTile(Position);
        _moving = false;
        _pushCounter = _record.PushDelay;
        OccupyTile();
    }

    private void UpdateResting(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        bool jumpingDownCliff = player.LedgeJumpPhase is
            LedgeJumpState.Airborne or
            LedgeJumpState.AirborneBeforeScroll or
            LedgeJumpState.AirborneAfterScroll;
        if (jumpingDownCliff)
        {
            // vineSprout_linkJumpingDownCliff restores the tile throughout
            // LINK_STATE_JUMPING_DOWN_LEDGE, then destroys the sprout only
            // inside the source's signed Z and high-byte overlap windows.
            RestoreTile();
            if (!OverlapsLink(player.Position) ||
                player.LedgeZ < -_record.CliffGroundProximity ||
                player.LedgeZ >= 0)
            {
                return;
            }

            spawns.Add(new RockDebrisSpawn(
                Position,
                _record.CliffDebrisInteraction));
            _database.ResetPosition(_record.SubId, _save);
            Finished = true;
            return;
        }

        // State 1 also leaves the underlying tile restored while Link is
        // already inside the sprout, then reclaims it after he leaves.
        if (OverlapsLink(player.Position))
        {
            RestoreTile();
            return;
        }

        if (_occupiedPackedPosition < 0)
            OccupyTile();
    }

    public void UpdatePushAttempt(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput)
    {
        if (!_initialized || _moving ||
            !InteractableTilePushGeometry.TryGetCardinalInput(
                movementInput, out Vector2I direction) ||
            direction != facing)
        {
            ResetPush();
            return;
        }

        Vector2 delta = Position - linkPosition;
        bool centered = direction.X == 0
            ? Mathf.Abs(delta.X) < 4.0f
            : Mathf.Abs(delta.Y) < 4.0f;
        if (!centered || delta.LengthSquared() >= 0x12 * 0x12 ||
            delta.Dot(direction) <= 0.0f)
        {
            ResetPush();
            return;
        }

        _pushCounter--;
        if (_pushCounter != 0)
            return;

        Vector2 destination = Position + (Vector2)direction *
            OracleRoomData.MetatileSize;
        if (_room.GetTerrainInfo(destination).Collision != 0)
        {
            ResetPush();
            return;
        }

        RestoreTile();
        _moveDelta = OracleObjectMovement.Shared.Delta(
            _record.SpeedRaw,
            InteractableTilePushGeometry.DirectionIndex(direction) * 8);
        _moveCounter = _record.MoveFrames;
        _moving = true;
        _playSound(OracleSoundEngine.SndMoveBlock);
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        // In state 1 the source writes collision $0f into the occupied tile.
        // Let that tile stop Link so his adjacent-wall probe can select the
        // pushing animation. state 4 restores the tile before moving, so only
        // the moving sprout needs entity-owned collision.
        if (!_moving)
            return false;
        Vector2 delta = linkCenter - Position;
        return Mathf.Abs(delta.X) < CombinedLinkRadius &&
            Mathf.Abs(delta.Y) < CombinedLinkRadius;
    }

    public new void SetTransitionDrawOffset(Vector2 offset) =>
        base.SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (Visible && _animation.HasFrames)
        {
            DrawTexture(
                _animation.CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }

    private void OccupyTile()
    {
        _underlyingTile = _room.GetMetatile(Position);
        _occupiedPackedPosition = _room.GetPackedPosition(Position);
        _room.SetPositionTileAndCollision(
            Position,
            0x00,
            0x0f,
            _animationTick(),
            // vineSprout_updateTileAtPosition writes wRoomLayout and
            // wRoomCollisions directly after the BG has been drawn. `$00 is
            // logical state only; the normal metatile under the sprout stays
            // visible.
            preserveRenderedTile: true);
        _database.PersistPosition(_record.SubId, Position, _save);
        _roomTileChanged();
    }

    private void RestoreTile()
    {
        if (_occupiedPackedPosition < 0)
            return;
        Vector2 position = new(
            (_occupiedPackedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (_occupiedPackedPosition >> 4) * OracleRoomData.MetatileSize + 8);
        _room.SetPositionTileAndCollision(
            position, _underlyingTile, 0x00, _animationTick());
        _occupiedPackedPosition = -1;
        _roomTileChanged();
    }

    private void ResetPush() => _pushCounter = _record.PushDelay;

    private bool OverlapsLink(Vector2 linkPosition)
    {
        Vector2 delta = Position - linkPosition;
        return Mathf.Abs(delta.X) <= _record.CliffOverlapRadius &&
            Mathf.Abs(delta.Y) <= _record.CliffOverlapRadius;
    }

    private static Vector2 CenterOnTile(Vector2 position) => new(
        Mathf.Floor(position.X / OracleRoomData.MetatileSize) *
            OracleRoomData.MetatileSize + 8,
        Mathf.Floor(position.Y / OracleRoomData.MetatileSize) *
            OracleRoomData.MetatileSize + 8);
}
