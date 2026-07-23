using Godot;
using System;

namespace oracleofages;

public sealed class DebugWarpController
{
    private readonly Player _player;
    private readonly Action<int, int> _loadRoom;
    private readonly Func<Vector2> _findSpawn;

    public DebugWarpController(
        Player player,
        Action<int, int> loadRoom,
        Func<Vector2> findSpawn,
        int targetGroup,
        int targetRoom)
    {
        _player = player;
        _loadRoom = loadRoom;
        _findSpawn = findSpawn;
        ConfigureTarget(targetGroup, targetRoom);
    }

    public int TargetGroup { get; private set; }
    public int TargetRoom { get; private set; }

    public void Update()
    {
        if (Input.IsActionJustPressed("debug_room_warp"))
            WarpToTarget();
    }

    public void ConfigureTarget(int group, int room)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(group);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(group, 7);
        ArgumentOutOfRangeException.ThrowIfNegative(room);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(room, 0xff);
        TargetGroup = group;
        TargetRoom = room;
    }

    public void WarpToTarget()
    {
        _loadRoom(TargetGroup, TargetRoom);
        _player.WarpTo(_findSpawn());
        _player.Face(Vector2I.Down);
    }
}
