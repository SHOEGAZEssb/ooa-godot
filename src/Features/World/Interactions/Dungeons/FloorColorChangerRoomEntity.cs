using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_FLOOR_COLOR_CHANGER $22:$00, the placed worker allocator.</summary>
internal sealed partial class FloorColorChangerRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly Func<OracleRoomData> _activeRoom;
    private readonly Func<Vector2,byte,bool> _createWorker;
    private readonly Func<int> _workerCount;
    private readonly int _firstToggle;
    private readonly int _firstFloor;
    private int _lastControlTile;
    private bool _initialized;

    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int WorkerCount => _workerCount();

    internal FloorColorChangerRoomEntity(DungeonObjectRecord record, Func<OracleRoomData> activeRoom,
        DungeonInteractionDatabase data, Func<Vector2,byte,bool> createWorker, Func<int> workerCount)
    {
        Position = record.Position;
        _activeRoom = activeRoom;
        _firstToggle = data.Constant("red-toggle-floor");
        _firstFloor = data.Constant("red-floor");
        _createWorker = createWorker;
        _workerCount = workerCount;
        Name = $"FloorColorChanger_{record.Group}_{record.Room:x2}";
        Visible = false;
    }

    private void Advance()
    {
        int tile = _activeRoom().GetPackedStorageMetatile(PackedPosition);
        if (!_initialized) { _initialized = true; _lastControlTile = tile; }
        if (tile == _lastControlTile || unchecked((byte)(tile-_firstToggle)) >= 3) return;
        // var03 is committed before allocation; a full pool does not retry
        // this color until the controller tile changes again.
        _lastControlTile = tile;
        _createWorker(Position,(byte)(_firstFloor+tile-_firstToggle));
    }

    private byte PackedPosition => unchecked((byte)(
        (Mathf.FloorToInt(Position.Y)&0xf0) | ((Mathf.FloorToInt(Position.X)>>4)&15)));

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) Advance();
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) { if (!_initialized) Advance(); }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
