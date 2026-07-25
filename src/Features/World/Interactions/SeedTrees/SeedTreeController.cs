using Godot;
using System;

namespace oracleofages;

/// <summary>
/// ENEMY_SEEDS_ON_TREE ($5a). The invisible controller finds the room-owned
/// tree graphic, creates three seed parts in original offset order, and clears
/// its refill bit after any child reports collection.
/// </summary>
internal partial class SeedTreeController : TransitionOffsetNode2D
{
    private static readonly Vector2[] SeedOffsets =
    [
        new(0, -8),
        new(-8, 0),
        new(8, 0)
    ];

    private SeedTreeDatabase _database = null!;
    private SeedTreePlacementRecord _record;
    private OracleRuntimeState _runtime = null!;
    private bool _childCollected;
    private bool _noSatchelMessageClaimed;

    internal SeedTreePlacementRecord Record => _record;
    internal bool Finished { get; private set; }
    internal bool HasActiveSeeds => !Finished;
    internal Vector2 TreeCenter { get; private set; }

    internal void Initialize(
        SeedTreeDatabase database,
        SeedTreePlacementRecord record,
        OracleRoomData room,
        OracleRuntimeState runtime)
    {
        _database = database;
        _record = record;
        _runtime = runtime;
        Visible = false;
        if (!database.IsRefilled(runtime, record.RefillIndex) ||
            !database.TryFindTreeCenter(room, out Vector2 center))
        {
            Finished = true;
            return;
        }

        TreeCenter = center;
        Position = center;
    }

    internal Vector2 SeedPosition(int index)
    {
        if (!HasActiveSeeds || index < 0 || index >= SeedOffsets.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return TreeCenter + SeedOffsets[index];
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;
        if (!_childCollected)
            return;
        _database.SetRefilled(_runtime, _record.RefillIndex, false);
        Finished = true;
    }

    internal void NotifyChildCollected() => _childCollected = true;

    internal bool TryClaimNoSatchelMessage()
    {
        if (_noSatchelMessageClaimed)
            return false;
        _noSatchelMessageClaimed = true;
        return true;
    }
}
