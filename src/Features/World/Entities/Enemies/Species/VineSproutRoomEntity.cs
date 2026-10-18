using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ENEMY_VINE_SPROUT $62. The resting sprout temporarily owns its metatile's
/// layout/collision entry; a valid 20-update push restores that tile, moves at
/// SPEED_c0 on the 21 nonzero countdown visits, centers on visit $16, and
/// stores the new global position.
/// </summary>
internal sealed partial class VineSproutRoomEntity : TransitionOffsetNode2D,
    IRoomEntity,
    IFixedRoomEntity,
    IRoomBlocker,
    IRoomPushableEntity,
    IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    private const float CombinedLinkRadius = 12.0f;
    private readonly VineSproutDatabase _database;
    private readonly VineSproutRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly EnemyAnimationPlayer _animation;
    private readonly OracleRandom _random;
    private readonly OracleRuntimeState? _memory;
    private int _pushCounter;
    private int _moveCounter;
    private int _moveAngle;
    private Vector2? _pendingPushInput;
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
        Func<long> animationTick,
        OracleRandom random,
        OracleRuntimeState? memory = null)
    {
        _database = database;
        _record = record;
        _room = room;
        _save = save;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _random = random;
        _memory = memory;
        _pushCounter = record.PushDelay;
        Position = position;
        Name = $"VineSprout_{record.SubId:x2}";
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
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
        _ = spawns;
        Vector2 pushInput = _pendingPushInput ?? Vector2.Zero;
        _pendingPushInput = null;
        if (!_initialized)
        {
            _random.Next(); // bank0.s: enemyStandardUpdate initializes var3d before enemyCode62.
            _initialized = true;
            OccupyTile();
            return;
        }
        if (!_moving)
        {
            if (UpdateResting(frame.Player, spawns))
                UpdatePush(frame.Player, pushInput);
            return;
        }

        // vineSprout_state4 decrements first; its zero visit centres without
        // applying velocity. Acceptance in state1 never moves this update.
        if (--_moveCounter != 0)
        {
            var walls = EnemyAdjacentWallResolver.Shared.ProbeTopDown(Position, _moveAngle,
                point => point.X < 0 || point.X >= _room.Width || point.Y < 0 || point.Y >= _room.Height ||
                    _room.IsSolidForEnemyMovement(point, holesAreWalls: true));
            Vector2 precise = Position;
            EnemyTerrainMovement.ApplyGivenAdjacentWalls(ref precise, _moveAngle,
                NativeObjectMovement.Velocity(_memory, _record.SpeedRaw, _moveAngle), walls, _record.SpeedRaw);
            Position = precise;
            QueueRedraw();
            return;
        }

        Position = CenterOnTile(Position);
        _moving = false;
        _pushCounter = _record.PushDelay;
        OccupyTile();
    }

    private bool UpdateResting(
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
                return false;
            }

            spawns.Add(new RockDebrisSpawn(
                Position,
                _record.CliffDebrisInteraction));
            _database.ResetPosition(_record.SubId, _save);
            Finished = true;
            return false;
        }

        // State 1 also leaves the underlying tile restored while Link is
        // already inside the sprout, then reclaims it after he leaves.
        if (OverlapsLink(player.Position))
        {
            RestoreTile();
            return false;
        }

        if (_occupiedPackedPosition < 0)
            OccupyTile();
        return true;
    }

    public void UpdatePushAttempt(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput)
    {
        // Link's front-tile pass supplies input only. The ENEMY pass owns
        // eligibility, the countdown, allocation-free acceptance and sound.
        _pendingPushInput = movementInput;
    }

    private void UpdatePush(Player player, Vector2 movementInput)
    {
        Vector2I direction = player.FacingVector;
        if (movementInput.IsZeroApprox() || player.ActiveTransformation != 0 ||
            !player.NativeNormalStateForInteraction || player.ItemCreationZFixed < 0 ||
            player.TopDownSwimming || player.GrabStateActive ||
            Input.IsActionJustPressed("attack") || Input.IsActionJustPressed("item"))
        {
            ResetPush();
            return;
        }

        Vector2 delta = Position.Floor() - player.EnemyContactPosition;
        bool centered = direction.X == 0
            ? Mathf.Abs(delta.X) <= 4.0f
            : Mathf.Abs(delta.Y) <= 4.0f;
        if (!centered || Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) >= 0x12 ||
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
        _moveAngle = InteractableTilePushGeometry.DirectionIndex(direction) * 8;
        _moveCounter = _record.MoveFrames;
        _moving = true;
        _playSound(SoundId.SndMoveBlock);
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
