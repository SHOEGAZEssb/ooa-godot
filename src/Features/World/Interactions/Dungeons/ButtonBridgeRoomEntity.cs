using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class ButtonBridgeRoomEntity(ButtonBridgeData data, OracleRoomData room,
    Func<int> triggers, Action<byte,byte> writeTile, Action<int> sound, Func<Vector2,bool> debris)
    : Node2D, IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity,
      IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { State = 1; Visible = false; return ScreenTransitionPresentation.Hidden; }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        Visible = false;
        bool pressed = (triggers() & 1) != 0;
        switch (State)
        {
            case 0: State = 1; return;
            case 1: if (pressed) { State = 2; Counter = data.Interval; } return;
            case 2: if (!pressed) { State = 4; return; } break;
            case 3: if (!pressed) State = 4; return;
            case 4: if (pressed) { State = 1; return; } break;
        }
        Counter = (Counter-1) & 0xff;
        if (Counter != 0) return;
        Counter = data.Interval;
        int direction = State == 2 ? 1 : -1;
        for (int p = State == 2 ? data.First : data.Last; p >= data.First && p <= data.Last; p += direction)
        {
            byte tile = room.Layout[p];
            if ((tile == data.Hole) != (State == 2)) continue;
            if (State == 4 && tile == data.Diamond) _ = debris(Position);
            writeTile((byte)p,State == 2 ? data.Bridge : data.Hole);
            sound(OracleSoundEngine.SndDoorClose);
            return;
        }
        State = State == 2 ? 3 : 1;
    }
}

internal readonly record struct ButtonBridgeData(int First,int Last,int Interval,byte Hole,byte Bridge,byte Diamond,string Source);
