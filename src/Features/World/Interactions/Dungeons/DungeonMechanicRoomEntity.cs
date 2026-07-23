using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract partial class DungeonMechanicRoomEntity : Node2D, IRoomEntity
{
    public Node2D Node => this;

    protected DungeonMechanicRoomEntity(
        DungeonMechanicDatabaseRecord record,
        string name)
        : this(record.PackedPosition, name)
    {
    }

    protected DungeonMechanicRoomEntity(int packedPosition, string name)
    {
        Name = name;
        Position = PositionFromPacked(packedPosition);
    }

    public void SetTransitionDrawOffset(Vector2 offset)
    {
        // These interactions are invisible and mutate the room tilemap only.
        // Destination room entities are frozen until scrolling completes.
    }

    private static Vector2 PositionFromPacked(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}
