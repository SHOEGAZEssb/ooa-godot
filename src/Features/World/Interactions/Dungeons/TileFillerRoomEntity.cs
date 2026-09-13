using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_TILE_FILLER $25, walking the yellow endpoint over blue floor.</summary>
internal sealed partial class TileFillerRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRoomData _room;
    private readonly DungeonInteractionDatabase _data;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private bool _initialized;
    private int _endpoint;

    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int Endpoint => _endpoint;

    internal TileFillerRoomEntity(DungeonObjectRecord record, OracleRoomData room, DungeonInteractionDatabase data,
        Action<int> playSound, Action roomTileChanged, Func<long> animationTick)
    {
        _room = room;
        _data = data;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        Position = record.Position;
        _endpoint = room.GetPackedPosition(Position);
        Name = "TileFiller";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        // RoomEntityManager supplies the SCROLLMODE_01 gameplay boundary;
        // this handler must not initialize in a destination preload.
        if (!_initialized)
        {
            _initialized = true;
            SetTile(_endpoint, _data.Constant("yellow-floor"));
            spawns.Add(new PuzzlePuffSpawn(Position, OracleSoundEngine.SndPoof));
        }
        Vector2 link = OracleObjectMath.ToPixelPosition(frame.Player.Position);
        int packed = (((int)link.Y + 5) & 0xf0) | (((int)link.X >> 4) & 15);
        int delta = (packed - _endpoint) & 0xff;
        if (delta is not (1 or 0xff or 0x10 or 0xf0) || _room.GetMetatile(Point(packed)) != _data.Constant("blue-floor"))
            return;
        int previous = _endpoint;
        _endpoint = packed;
        SetTile(previous, _data.Constant("red-floor"));
        SetTile(packed, _data.Constant("yellow-floor"));
        _playSound(OracleSoundEngine.SndGetSeed);
    }

    private void SetTile(int packed, int tile)
    {
        _room.SetPositionTileAndCollision(Point(packed), (byte)tile, null, _animationTick());
        _roomTileChanged();
    }

    private static Vector2 Point(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
