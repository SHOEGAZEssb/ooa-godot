using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_FLOOR_COLOR_CHANGER $22:$00/$01.</summary>
internal sealed partial class WingDungeonFloorColorChanger : Node2D,
    IRoomEntity, IFixedRoomEntity
{
    private readonly ObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly WingDungeonDatabase _data;
    private readonly OracleRandom _random;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private readonly List<Worker> _workers = new();
    private int _lastControlTile;

    public Node2D Node => this;
    internal int WorkerCount => _workers.Count;

    internal WingDungeonFloorColorChanger(
        ObjectRecord record,
        OracleRoomData room,
        WingDungeonDatabase data,
        OracleRandom random,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _record = record;
        _room = room;
        _data = data;
        _random = random;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _lastControlTile = room.GetMetatile(record.Position);
        Name = $"WingDungeonFloorColorChanger_{record.Room:x2}";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        int controlTile = _room.GetMetatile(_record.Position);
        if (controlTile != _lastControlTile)
        {
            _lastControlTile = controlTile;
            int first = _data.Constant("red-toggle-floor");
            if (controlTile >= first && controlTile < first + 3)
            {
                _workers.Add(new Worker(
                    _random.GeneratePermutation(),
                    (byte)(_data.Constant("red-floor") + controlTile - first),
                    controlTile));
            }
        }

        for (int index = _workers.Count - 1; index >= 0; index--)
        {
            Worker worker = _workers[index];
            int currentControlTile = _room.GetMetatile(_record.Position);
            if (currentControlTile != worker.ControlTile &&
                currentControlTile != _data.Constant("somaria-block"))
            {
                _workers.RemoveAt(index);
                continue;
            }
            for (int count = 0; count < 4 && worker.Index >= 0; count++)
                Convert(worker);
            if (worker.Index < 0)
                _workers.RemoveAt(index);
        }
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private void Convert(Worker worker)
    {
        int packedPosition = worker.Permutation[worker.Index--];
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
        }
        _room.SetUnderlyingMetatile(point, worker.TargetTile);
    }

    private sealed class Worker(
        byte[] permutation,
        byte targetTile,
        int controlTile)
    {
        internal byte[] Permutation { get; } = permutation;
        internal byte TargetTile { get; } = targetTile;
        internal int ControlTile { get; } = controlTile;
        internal int Index { get; set; } = 0xff;
    }
}
