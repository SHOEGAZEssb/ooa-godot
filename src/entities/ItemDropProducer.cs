using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ENEMY_ITEM_DROP_PRODUCER ($59), created by Ages object opcode $fa. It is
/// invisible and uncounted, captures its metatile on the first update, and
/// creates PART_ITEM_DROP only after that tile changes.
/// </summary>
public partial class ItemDropProducer : Node2D
{
    private OracleRoomData _room = null!;
    private OracleSaveData? _save;
    private int _subId;
    private byte _initialTile;

    internal bool Initialized { get; private set; }
    internal bool Finished { get; private set; }
    internal bool SpawnedDrop { get; private set; }

    internal void Initialize(
        int subId,
        Vector2 position,
        OracleRoomData room,
        OracleSaveData? save)
    {
        _subId = subId;
        Position = position;
        _room = room;
        _save = save;
        Visible = false;
    }

    internal void UpdateFrame(ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!Initialized)
        {
            _initialTile = _room.GetMetatile(Position);
            Initialized = true;
            return;
        }
        if (_room.GetMetatile(Position) == _initialTile)
            return;

        if (ItemDropDatabase.IsAvailable(_subId, _save))
        {
            spawns.Add(new ItemDropSpawn(
                _subId, Position, UpdateThisFrame: true));
            SpawnedDrop = true;
        }
        Finished = true;
    }
}
