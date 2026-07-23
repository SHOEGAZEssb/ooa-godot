using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>One-update INTERAC_STATUE_EYEBALL $e2:$01 room-layout scanner.</summary>
internal sealed class StatueEyeballSpawnerRoomEntity : RoomEntityAdapter<Node2D>,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly OracleRoomData _room;
    private readonly DungeonEntranceInteractionDatabase _data;

    internal StatueEyeballSpawnerRoomEntity(
        OracleRoomData room,
        DungeonEntranceInteractionDatabase data)
        : base(new Node2D { Name = "StatueEyeballSpawner", Visible = false }, static _ => { })
    {
        _room = room;
        _data = data;
    }

    public bool Finished { get; private set; }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (_room.Layout.Length != 176)
        {
            throw new InvalidOperationException(
                $"INTERAC_STATUE_EYEBALL $e2:$01 requires a large room, got " +
                $"{_room.Group:x1}:{_room.Id:x2}.");
        }

        // The source starts at packed position $ae and decrements C through
        // $01, so child slot/first-update order is descending room position.
        for (int packed = 0xae; packed >= 1; packed--)
        {
            if (_room.Layout[packed] != _data.EyeStatueTile)
                continue;
            Vector2 position = new(
                (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
                (packed >> 4) * OracleRoomData.MetatileSize + 8 +
                    _data.EyeInitialYOffset);
            spawns.Add(new StatueEyeballSpawn(position));
        }
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
