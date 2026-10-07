using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_FLOOR_COLOR_CHANGER $22:$01, a physical shared-buffer worker.</summary>
internal sealed partial class FloorColorWorkerRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly byte _targetTile;
    private readonly int _firstFloor;
    private readonly int _somariaTile;
    private readonly Func<OracleRoomData> _activeRoom;
    private readonly Func<byte,byte,bool> _setTile;
    private readonly OracleRandom _random;
    private readonly OracleRuntimeState _runtime;
    private int _controlTile;
    private int _index;
    private bool _initialized;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int Index => _index;

    internal FloorColorWorkerRoomEntity(Vector2 position,byte targetTile,DungeonInteractionDatabase data,
        Func<OracleRoomData> activeRoom,Func<byte,byte,bool> setTile,OracleRandom random,OracleRuntimeState runtime)
    {
        Position = position;
        _targetTile = targetTile;
        _firstFloor = data.Constant("red-floor");
        _somariaTile = data.Constant("somaria-block");
        _activeRoom = activeRoom;
        _setTile = setTile;
        _random = random;
        _runtime = runtime;
        Name = "FloorColorWorker";
        Visible = false;
    }

    private void Advance()
    {
        if (Finished) return;
        OracleRoomData room = _activeRoom();
        byte controller = unchecked((byte)((Mathf.FloorToInt(Position.Y)&0xf0) |
            ((Mathf.FloorToInt(Position.X)>>4)&15)));
        int current = room.GetPackedStorageMetatile(controller);
        if (!_initialized)
        {
            _initialized = true;
            _controlTile = current;
            _index = 0xff;
            byte[] permutation = _random.GeneratePermutation();
            for (int index = 0; index < 256; index++)
                _runtime.SetWramByte(WramAddress.wBigBuffer+index,permutation[index]);
        }
        if (current != _controlTile && current != _somariaTile) { Finished = true; return; }
        for (int count = 0; count < 4 && _index > 0; count++) Convert(room,controller);
        // The fourth decrement reaches zero on update64; @done converts
        // index0 immediately before deleting in the same dispatch.
        if (_index == 0) { Convert(room,controller); Finished = true; }
    }

    private void Convert(OracleRoomData room,byte controller)
    {
        byte position = _runtime.ReadWramByte(WramAddress.wBigBuffer+_index--);
        if (position >= 0x9f || position == controller || (position&15) == 0 ||
            (position&0xf0) is 0 or 0xa0) return;
        int tile = room.GetPackedStorageMetatile(position);
        if (unchecked((byte)(tile-_firstFloor)) < 3) _setTile(position,_targetTile);
        // The original accepts right-column padding. Only noncolored tiles
        // write their underlying byte; setTile alone leaves it unchanged.
        else room.SetUnderlyingStorageMetatile(position,_targetTile);
    }

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) Advance();
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) { if (!_initialized) Advance(); }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
