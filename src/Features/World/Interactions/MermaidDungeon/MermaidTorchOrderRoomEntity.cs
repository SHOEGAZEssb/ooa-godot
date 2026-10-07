using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// miscPuzzles_subid07 reads layout bits, not the cumulative wNumTorchesLit.
internal sealed partial class MermaidTorchOrderRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly MermaidTorchOrderDatabase _data;
    private readonly OracleRoomData _room;
    private readonly Func<bool> _completed;
    private readonly Action _recreateTorches;
    private readonly Action<byte,byte> _setTile;
    private readonly Action<int> _sound;
    private readonly Action _beginRetraction;
    internal bool Initialized { get; private set; }
    internal int Step { get; private set; }
    internal byte LastLitMask { get; private set; }
    public bool Finished { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Initialized;
    internal MermaidTorchOrderRoomEntity(MermaidTorchOrderDatabase data,OracleRoomData room,
        Func<bool> completed,Action recreateTorches,Action<byte,byte> setTile,Action<int> sound,Action beginRetraction)
    {
        _data=data; _room=room; _completed=completed; _recreateTorches=recreateTorches;
        _setTile=setTile; _sound=sound; _beginRetraction=beginRetraction;
        Name="MermaidTorchOrder"; Visible=false;
    }
    private byte LitMask()
    {
        byte mask=0;
        for (int i=0;i<_data.Steps.Length;i++)
            if (_room.GetPackedStorageMetatile(_data.Steps[i].Position) == 0x09) mask |= (byte)(1<<i);
        return mask;
    }
    private void Initialize()
    {
        if (_completed()) { Finished=true; return; }
        Initialized=true; LastLitMask=LitMask(); _recreateTorches();
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (!Initialized) { Initialize(); return; }
        if (!frame.Player.NativePuzzleResetVulnerable) return;
        byte mask=LitMask();
        if (mask == LastLitMask) return;
        LastLitMask=mask;
        if (mask != _data.Steps[Step].ExpectedMask)
        {
            Step=0; LastLitMask=0; _sound(SoundId.SndError);
            foreach (var step in _data.Steps) _setTile(step.ResetPosition,0x08);
            _recreateTorches(); return;
        }
        if (Step < _data.Steps.Length-1) { Step++; return; }
        _beginRetraction(); Finished=true;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (!Initialized && !Finished) Initialize(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
