using Godot;
using System;

namespace oracleofages;

public sealed class DebugWarpController
{
    private readonly RoomSession _rooms;
    private readonly Player _player;
    private readonly Action<int, int> _loadRoom;
    private readonly Func<Vector2> _findSpawn;
    private readonly Func<long> _animationTick;

    public DebugWarpController(
        RoomSession rooms,
        Player player,
        Action<int, int> loadRoom,
        Func<Vector2> findSpawn,
        Func<long> animationTick)
    {
        _rooms = rooms;
        _player = player;
        _loadRoom = loadRoom;
        _findSpawn = findSpawn;
        _animationTick = animationTick;
    }

    public void Update()
    {
        if (Input.IsActionJustPressed("debug_sign")) WarpToSign();
        if (Input.IsActionJustPressed("debug_animation")) WarpToAnimation();
        if (Input.IsActionJustPressed("debug_bush")) WarpToBush();
        if (Input.IsActionJustPressed("debug_house")) WarpToHouse();
    }

    public void WarpToSign()
    {
        _loadRoom(0, 0x2a);
        _player.WarpTo(new Vector2(5 * OracleRoomData.MetatileSize + 8, 70));
        _player.Face(Vector2I.Up);
    }

    public void WarpToAnimation()
    {
        int targetRoom = _rooms.CurrentRoom.Id == 0xb8 ? 0x03 : 0xb8;
        _loadRoom(0, targetRoom);
        _player.WarpTo(_findSpawn());
        _player.Face(Vector2I.Down);
    }

    public void WarpToBush()
    {
        _loadRoom(0, 0x69);
        Vector2 bushPoint = new(24, 56);
        _rooms.CurrentRoom.ReplaceMetatile(bushPoint, 0x3a, 0xc5, _animationTick());
        _player.WarpTo(new Vector2(bushPoint.X, 70));
        _player.Face(Vector2I.Up);
    }

    public void WarpToHouse()
    {
        _loadRoom(0, 0x47);
        _player.WarpTo(new Vector2(5 * OracleRoomData.MetatileSize + 8, 54));
        _player.Face(Vector2I.Up);
    }

    public void WarpToNpc()
    {
        _loadRoom(0, 0x48);
        _player.WarpTo(new Vector2(0x38, 0x58));
        _player.Face(Vector2I.Up);
    }
}
