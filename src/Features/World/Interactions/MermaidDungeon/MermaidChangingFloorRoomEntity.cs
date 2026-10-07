using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC$90:$04 publishes masks and children before copying shared wBigBuffer.
internal sealed partial class MermaidChangingFloorRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity,
    IRoomEntityUpdateFreeze, IPlayerRestriction
{
    private readonly MermaidChangingFloorDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<int,bool> _createWorker;
    private bool _initialized;
    private byte _lastToggle;
    private int _pattern;
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    public bool FreezesRoomEntities => _runtime.ReadWramByte(WramAddress.wDisabledObjects) != 0;
    public bool FreezesInteractions => (_runtime.ReadWramByte(WramAddress.wDisabledObjects)&2) != 0;
    public bool FreezesPlayerUpdates => (_runtime.ReadWramByte(WramAddress.wDisabledObjects)&0x81) != 0;
    public bool DisablesItems => (_runtime.ReadWramByte(WramAddress.wDisabledObjects)&0x90) != 0;
    public bool DisablesCompanion => (_runtime.ReadWramByte(WramAddress.wDisabledObjects)&0xa0) != 0;
    public bool DisablesSword => false;
    public bool DisablesMenus => _runtime.ReadWramByte(WramAddress.wMenuDisabled) != 0;
    public bool MenuDisablesWarpTiles => DisablesMenus;

    internal MermaidChangingFloorRoomEntity(MermaidChangingFloorDatabase data,OracleRuntimeState runtime,Func<int,bool> createWorker)
    {
        _data = data; _runtime = runtime; _createWorker = createWorker;
        Name = "MermaidChangingFloor"; Visible = false;
    }
    private void Advance()
    {
        byte toggle = _runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress);
        if (!_initialized) { _initialized = true; _lastToggle = toggle; return; }
        // Freeze ownership doesn't grant this state1 handler native admission.
        if (FreezesInteractions || toggle == _lastToggle) return;
        _lastToggle = toggle;
        _runtime.SetWramByte(WramAddress.wDisabledObjects,0xff);
        _runtime.SetWramByte(WramAddress.wMenuDisabled,0xff);
        _pattern = (_pattern+1)&1;
        _createWorker(5); _createWorker(6);
        _data.LoadPattern(_runtime,_pattern);
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (!_initialized) Advance(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
