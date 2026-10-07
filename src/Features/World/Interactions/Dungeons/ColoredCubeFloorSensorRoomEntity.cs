using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC$90:$02 remains state0. Floor substitution retains the occupying
// cube's solidity; the cube's next roll clears the old collision cell.
internal sealed partial class ColoredCubeFloorSensorRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly byte _position;
    private readonly Func<OracleRoomData> _activeRoom;
    private readonly ColoredCubePuzzleState _puzzle;
    private readonly int _firstFloor;
    private readonly byte _standardFloor;
    private readonly Func<IRoomEntity,bool> _isOutgoing;
    private readonly Action<byte,byte> _writeTile;
    private readonly Action<int> _sound;
    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal ColoredCubeFloorSensorRoomEntity(DungeonObjectRecord record,
        Func<OracleRoomData> activeRoom,ColoredCubePuzzleState puzzle,
        DungeonInteractionDatabase data,Func<IRoomEntity,bool> isOutgoing,
        Action<byte,byte> writeTile,Action<int> sound)
    {
        Position = record.Position; Visible = false;
        Name = $"CubeFloorSensor_{record.Group:x1}_{record.Room:x2}_{record.Order}";
        _position = (byte)((record.Y&0xf0)|((record.X>>4)&15));
        _activeRoom = activeRoom; _puzzle = puzzle;
        _firstFloor = data.Constant("red-toggle-floor");
        _standardFloor = (byte)data.Constant("standard-floor");
        _isOutgoing = isOutgoing; _writeTile = writeTile; _sound = sound;
    }
    private void Advance()
    {
        if (Finished) return;
        if (_isOutgoing(this)) { Finished = true; return; }
        OracleRoomData room = _activeRoom();
        int color = unchecked((byte)(room.GetPackedStorageMetatile(_position)-_firstFloor));
        if (_puzzle.CubePosition != _position || (_puzzle.CubeColor&3) != color) return;
        _writeTile(_position,_standardFloor);
        room.SetPackedTileCollision(_position,0x0f);
        _sound(SoundId.SndSolvePuzzle);
        Finished = true;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Advance(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
