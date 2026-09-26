using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_FLOOR_COLOR_CHANGER $22:$00/$01.</summary>
internal sealed partial class FloorColorChangerRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomInteractionChildSource
{
    private readonly DungeonObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonInteractionDatabase _data;
    private readonly OracleRandom _random;
    private readonly OracleRuntimeState _runtime;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly List<Worker> _workers = new();
    private int _lastControlTile;
    private bool _initialized;

    public Node2D Node => this;
    internal int WorkerCount => _workers.Count;

    internal FloorColorChangerRoomEntity(
        DungeonObjectRecord record,
        OracleRoomData room,
        DungeonInteractionDatabase data,
        OracleRandom random,
        OracleRuntimeState runtime,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _record = record;
        _room = room;
        _data = data;
        _random = random;
        _runtime = runtime;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        Name = $"FloorColorChanger_{record.Group}_{record.Room:x2}";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int controlTile = _room.GetMetatile(_record.Position);
        if (!_initialized)
        {
            _initialized = true;
            _lastControlTile = controlTile;
        }
        if (controlTile != _lastControlTile)
        {
            int first = _data.Constant("red-toggle-floor");
            if (controlTile >= first && controlTile < first + 3)
            {
                _lastControlTile = controlTile;
                _workers.Add(new Worker((byte)(_data.Constant("red-floor") + controlTile - first)));
            }
        }
    }

    public void UpdateChildren(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        for (int index = 0; index < _workers.Count;)
        {
            Worker worker = _workers[index];
            int currentControlTile = _room.GetMetatile(_record.Position);
            if (!worker.Initialized)
            {
                worker.Initialized = true;
                worker.ControlTile = currentControlTile;
                byte[] permutation = _random.GeneratePermutation();
                for (int i = 0; i < permutation.Length; i++)
                    _runtime.SetWramByte(WramAddress.wBigBuffer + i, permutation[i]);
            }
            if (currentControlTile != worker.ControlTile &&
                currentControlTile != _data.Constant("somaria-block"))
            {
                _workers.RemoveAt(index);
                continue;
            }
            for (int count = 0; count < 4 && worker.Index > 0; count++)
                Convert(worker);
            // Source @done performs the final index0 conversion immediately
            // after counter1 becomes zero, in the same (64th) dispatch.
            if (worker.Index == 0)
            {
                Convert(worker);
                _workers.RemoveAt(index);
            }
            else index++;
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void Convert(Worker worker)
    {
        int packedPosition = _runtime.ReadWramByte(WramAddress.wBigBuffer + worker.Index--);
        int controllerPosition = _room.GetPackedPosition(_record.Position);
        int column = packedPosition & 0x0f;
        int row = packedPosition >> 4;
        // The source compares against $9f, rejects the left/top/bottom edges,
        // and deliberately does not reject the right edge.
        if (packedPosition >= 0x9f || packedPosition == controllerPosition ||
            column == 0 || row == 0 || row == 0x0a)
        {
            return;
        }

        Vector2 point = new(column * 16 + 8, row * 16 + 8);
        int tile = _room.GetMetatile(point);
        int first = _data.Constant("red-floor");
        if (tile >= first && tile < first + 3)
        {
            _room.SetPositionTileAndCollision(
                point, worker.TargetTile, null, _animationTick());
            _roomTileChanged();
            return;
        }
        // colorChangingFloor_processPosition deliberately accepts large-room
        // column $0f. It is padding rather than playable space, but remains a
        // real byte in the 16-byte-stride room-layout buffer.
        _room.SetUnderlyingStorageMetatile(packedPosition, worker.TargetTile);
    }

    private sealed class Worker(byte targetTile)
    {
        internal byte TargetTile { get; } = targetTile;
        internal int ControlTile { get; set; }
        internal bool Initialized { get; set; }
        internal int Index { get; set; } = 0xff;
    }
}
