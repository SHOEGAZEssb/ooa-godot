using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Invisible INTERAC_PIRATE $c4:$04. Link pushes upward for ten updates to
/// insert the Tokay Eyeball; this owner runs only the socket's native sequence.
/// </summary>
internal sealed partial class TokayEyeballSlotRoomEntity : Node2D,
    IRoomEntity,
    IFixedRoomEntity,
    IRoomBlocker,
    IRoomPushableEntity,
    IPlayerRestriction,
    IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity
{
    private const float CombinedLinkRadius = 12.0f;
    private readonly NpcRecord _placement;
    private readonly TokayEntranceEyeDatabase _database;
    private readonly TokayEyeballSlotRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly InventoryState _inventory;
    private readonly Action<int, string, Vector2> _showText;
    private readonly Action<int> _playSound;
    private readonly Action<int> _beginScreenShake;
    private readonly Action<int, int> _resetRoomMusic;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private TokayEyeballSlotState _state;
    private int _counter;
    private int _pushCounter;

    public Node2D Node => this;
    public bool Finished => _state == TokayEyeballSlotState.Finished;
    public bool DisablesSword => SequenceActive;
    public bool DisablesItems => SequenceActive;
    public bool DisablesMovement => SequenceActive;
    public bool DisablesMenus => SequenceActive;
    public bool DisablesRingTransformations => SequenceActive;
    public bool DisablesScreenTransitions => SequenceActive;
    internal TokayEyeballSlotState State => _state;
    internal int Counter => _counter;
    internal int PushCounter => _pushCounter;
    private bool SequenceActive => _state is not (
        TokayEyeballSlotState.Waiting or TokayEyeballSlotState.Finished);

    internal TokayEyeballSlotRoomEntity(
        NpcRecord placement,
        TokayEntranceEyeDatabase database,
        OracleRoomData room,
        OracleSaveData save,
        InventoryState inventory,
        Action<int, string, Vector2> showText,
        Action<int> playSound,
        Action<int> beginScreenShake,
        Action<int, int> resetRoomMusic,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _placement = placement;
        _database = database;
        _record = database.Slot;
        _room = room;
        _save = save;
        _inventory = inventory;
        _showText = showText;
        _playSound = playSound;
        _beginScreenShake = beginScreenShake;
        _resetRoomMusic = resetRoomMusic;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        if (placement is not { Group: 1, Room: 0xba, Id: 0xc4, SubId: 0x04 })
            throw new ArgumentOutOfRangeException(nameof(placement));
        Name = "TokayEyeballSocket";
        Position = new Vector2(placement.X, placement.Y);
        Visible = false;
        _pushCounter = _record.PushDelay;
        if (save.HasRoomFlag(_record.Group, _record.Room,
                checked((byte)_record.RoomFlag)))
        {
            _state = TokayEyeballSlotState.Finished;
        }
    }

    public void UpdatePushAttempt(
        Vector2 linkPosition,
        Vector2I facing,
        Vector2 movementInput)
    {
        if (_state != TokayEyeballSlotState.Waiting ||
            !InteractableTilePushGeometry.TryGetCardinalInput(
                movementInput, out Vector2I direction) ||
            direction != Vector2I.Up || facing != Vector2I.Up)
        {
            ResetPush();
            return;
        }

        Vector2 delta = Position - linkPosition;
        if (Mathf.Abs(delta.X) >= 5.0f ||
            delta.LengthSquared() >= 0x12 * 0x12 ||
            delta.Dot(Vector2I.Up) <= 0.0f)
        {
            ResetPush();
            return;
        }

        _pushCounter--;
        if (_pushCounter != 0)
            return;
        ResetPush();
        if (!_inventory.HasTreasure(_record.Treasure))
        {
            _showText(_placement.TextId, _placement.Message, Position);
            return;
        }
        _state = TokayEyeballSlotState.BeginInsert;
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        switch (_state)
        {
            case TokayEyeballSlotState.Waiting:
            case TokayEyeballSlotState.Finished:
                return;

            case TokayEyeballSlotState.BeginInsert:
                BeginInsert(spawns);
                return;

            case TokayEyeballSlotState.EyeWait:
                if (--_counter != 0)
                    return;
                _playSound(OracleSoundEngine.SndOpening);
                _beginScreenShake(_record.ShakeFrames);
                _counter = _record.ShakeWait;
                _state = TokayEyeballSlotState.ShakeWait;
                return;

            case TokayEyeballSlotState.ShakeWait:
                if (--_counter != 0)
                    return;
                OpenEntrance(spawns);
                _counter = _record.OpenWait;
                _state = TokayEyeballSlotState.OpenWait;
                return;

            case TokayEyeballSlotState.OpenWait:
                if (--_counter != 0)
                    return;
                _playSound(OracleSoundEngine.SndSolvePuzzle);
                _resetRoomMusic(_record.Group, _record.Room);
                _inventory.LoseTreasure(_record.Treasure);
                _state = TokayEyeballSlotState.Finished;
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Tokay eyeball socket state {_state} from " +
                    $"{_record.Source}.");
        }
    }

    public bool BlocksLink(Vector2 linkCenter)
    {
        if (_state != TokayEyeballSlotState.Waiting)
            return false;
        Vector2 delta = linkCenter - Position;
        return Mathf.Abs(delta.X) < CombinedLinkRadius &&
            Mathf.Abs(delta.Y) < CombinedLinkRadius;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        return ScreenTransitionPresentation.Hidden;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void BeginInsert(ICollection<RoomEntitySpawn> spawns)
    {
        _playSound(OracleSoundEngine.SndCtrlStopMusic);
        _save.SetRoomFlag(
            _record.Group, _record.Room, checked((byte)_record.RoomFlag));
        spawns.Add(new TokayEntranceEyeSpawn(_database.InsertedEye));
        _playSound(OracleSoundEngine.SndOpenChest);
        _counter = _record.EyeWait;
        _state = TokayEyeballSlotState.EyeWait;
    }

    private void OpenEntrance(ICollection<RoomEntitySpawn> spawns)
    {
        for (int index = 0; index < _record.OpenTiles.Length; index++)
        {
            int packed = _record.OpenPosition + index;
            Vector2 point = new(
                (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
                (packed >> 4) * OracleRoomData.MetatileSize + 8);
            _room.SetPositionTileAndCollision(
                point,
                checked((byte)_record.OpenTiles[index]),
                collision: null,
                _animationTick());
        }
        _roomTileChanged();
        _playSound(OracleSoundEngine.SndDoorClose);
        spawns.Add(new PuzzlePuffSpawn(
            new Vector2(_record.PuffX, _record.PuffY), Sound: 0));
    }

    private void ResetPush() => _pushCounter = _record.PushDelay;
}

internal enum TokayEyeballSlotState
{
    Waiting,
    BeginInsert,
    EyeWait,
    ShakeWait,
    OpenWait,
    Finished
}
