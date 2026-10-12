using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// miscPuzzles_subid0b publishes both arena flags before the first tile. The
// eight-update timer advances the ladder through packed position $9d.
internal sealed partial class JabuLadderRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly Func<bool> _completed;
    private readonly Action _complete;
    private readonly Func<int> _enemies;
    private readonly Action<byte,byte> _tile;
    private readonly Func<Vector2,bool> _puff;
    private readonly Action<int> _sound;
    private readonly Action _unlock;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    public bool Finished { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    internal JabuLadderRoomEntity(DungeonObjectRecord record,Func<bool> completed,Action complete,
        Func<int> enemies,Action<byte,byte> tile,Func<Vector2,bool> puff,Action<int> sound,Action unlock)
    {
        Position=record.Position; _completed=completed; _complete=complete; _enemies=enemies;
        _tile=tile; _puff=puff; _sound=sound; _unlock=unlock; Visible=false; Name="JabuMinibossLadder";
    }
    private void Initialize()
    { if (_completed()) Finished=true; else State=1; }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        switch (State)
        {
            case 0: Initialize(); return;
            case 1:
                if (_enemies() != 0) return;
                _complete(); Counter=8; State=2; return;
            case 2:
                if (--Counter != 0) return;
                Counter=8;
                byte packed=(byte)(((int)Position.Y&0xf0)|((int)Position.X>>4));
                _tile(packed,0x18); _puff(Position);
                Position=new(Position.X,((int)Position.Y+16)&0xff);
                if (packed < 0x90) return;
                _tile(0x80,0x52); _tile(0x90,0x01); // TILEINDEX_SS_EMPTY
                _sound(SoundId.SndSolvePuzzle); _unlock(); Finished=true; return;
            default: throw new InvalidOperationException($"INTERAC$90:$0b: invalid state${State:x2}.");
        }
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (State == 0 && !Finished) Initialize(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
