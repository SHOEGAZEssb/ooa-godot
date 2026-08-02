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
    private InventoryState? _inventory;
    private int _subId;
    private byte _initialTile;

    internal bool Initialized { get; private set; }
    internal bool Finished { get; private set; }
    internal bool SpawnedDrop { get; private set; }

    internal void Initialize(
        int subId,
        Vector2 position,
        OracleRoomData room,
        InventoryState? inventory,
        OracleSaveData? save)
    {
        _subId = subId;
        Position = position;
        _room = room;
        _inventory = inventory;
        _save = save;
        Visible = false;
    }

    internal void UpdateFrame(ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!Initialized)
        {
            CaptureInitialTile();
            return;
        }
        if (_room.GetMetatile(Position) == _initialTile)
            return;

        if (ItemDropDatabase.IsAvailable(_subId, _inventory, _save))
        {
            spawns.Add(new ItemDropSpawn(
                _subId, Position, UpdateThisFrame: true));
            SpawnedDrop = true;
        }
        Finished = true;
    }

    internal ScreenTransitionPresentation PrepareForScreenTransition()
    {
        if (!Initialized)
            CaptureInitialTile();
        return ScreenTransitionPresentation.Hidden;
    }

    private void CaptureInitialTile()
    {
        _initialTile = _room.GetMetatile(Position);
        Initialized = true;
    }
}
