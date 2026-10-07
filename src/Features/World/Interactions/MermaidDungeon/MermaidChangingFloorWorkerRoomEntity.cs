using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC$90:$05/$06 keeps state0 for its entire life; only substate changes.
internal sealed partial class MermaidChangingFloorWorkerRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly MermaidFloorWorkerProfile _profile;
    private readonly OracleRuntimeState _runtime;
    private readonly Action<byte,byte> _setTile;
    private bool _initialized;
    private byte _position, _yStep, _offset;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int SubId { get; }
    internal bool Initialized => _initialized;
    internal byte PackedPosition => _position;
    internal byte YStep => _yStep;
    internal byte BufferOffset => _offset;
    internal MermaidChangingFloorWorkerRoomEntity(int subid,MermaidFloorWorkerProfile profile,OracleRuntimeState runtime,Action<byte,byte> setTile)
    {
        SubId = subid; _profile = profile; _runtime = runtime; _setTile = setTile;
        Name = $"MermaidFloorWorker_{subid:x2}"; Visible = false;
    }
    private void Advance()
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true;
            _position = _profile.Position; _yStep = _profile.YStep; _offset = _profile.BufferOffset;
            return;
        }
        // $ff changes column/direction and continues within this same pass;
        // one ordinary tile ends the dispatch. Read live shared bytes.
        int cursor = _offset;
        for (int scanned = 0; scanned < 256; scanned++)
        {
            byte tile = _runtime.ReadWramByte(WramAddress.wBigBuffer+cursor);
            cursor = (cursor+1)&0xff;
            if (tile == 0)
            {
                _runtime.SetWramByte(WramAddress.wDisabledObjects,0);
                _runtime.SetWramByte(WramAddress.wMenuDisabled,0);
                Finished = true;
                return;
            }
            if (tile == 0xff)
            {
                _position = unchecked((byte)(_position+_profile.XStep));
                _yStep = unchecked((byte)-_yStep);
                _position = unchecked((byte)(_position+_yStep));
                continue;
            }
            _offset = (byte)cursor;
            byte position = _position;
            _position = unchecked((byte)(_position+_yStep));
            _setTile(position,tile);
            return;
        }
        throw new InvalidOperationException($"{_profile.Source}: shared floor buffer has no tile or terminator in a complete byte scan.");
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Advance(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
