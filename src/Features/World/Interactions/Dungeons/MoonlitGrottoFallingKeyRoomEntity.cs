using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_DUNGEON_EVENTS $21:$0e. The source watches wRoomLayout+$4a for
/// metatile $2a, then creates TREASURE_SMALL_KEY:$01 at its exact Y/X.
/// </summary>
internal sealed partial class MoonlitGrottoFallingKeyRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleRoomData _room;
    private readonly GroundTreasureGrantRequest _request;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int GoalPosition => _data.MoonlitKeyGoalPosition;
    internal int GoalTile => _data.MoonlitKeyGoalTile;

    internal MoonlitGrottoFallingKeyRoomEntity(
        DungeonMechanicDatabaseRecord record,
        DungeonMechanicDatabase data,
        OracleRoomData room,
        GroundTreasureGrantRequest request)
    {
        if (record.Id != 0x21 || record.SubId != 0x0e)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _data = data;
        _room = room;
        _request = request;
        Name = $"MoonlitFallingKey_{record.Room:x2}";
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished ||
            _room.GetMetatile(Point(_data.MoonlitKeyGoalPosition)) !=
                _data.MoonlitKeyGoalTile)
        {
            return;
        }

        spawns.Add(new GroundTreasureGrantSpawn(_request));
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private static Vector2 Point(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}
