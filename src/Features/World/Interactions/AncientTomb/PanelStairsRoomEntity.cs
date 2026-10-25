using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// miscPuzzles_subid0d/0e. State zero keeps polling even while initialized
// interactions are frozen; the stone panels clear the shared completion bit.
internal sealed partial class PanelStairsRoomEntity(DungeonObjectRecord record,
    OracleSaveData save, Func<int> triggers, Action<int, bool> trigger, Func<int> torches,
    Action<int> shake, Action<int> sound, Func<Vector2, bool> puff, Action<byte, byte> tile)
    : Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    public bool Finished { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Visible = false;
        if (Finished) return;
        switch (State)
        {
            case 0:
                if (save.HasRoomFlag(record.Group, record.Room, 0x40)) { Finished = true; return; }
                if (record.SubId == 0x0d)
                {
                    if (torches() != 1 || triggers() != 7) return;
                    Counter = 30; shake(8); sound(SoundId.SndDoorClose); State = 1;
                }
                else if ((save.GetRoomFlags(record.Group, record.Room) & 15) == 15)
                { trigger(7, true); State = 1; }
                return;
            case 1 when record.SubId == 0x0d:
                if (--Counter != 0) return;
                if (triggers() != 7) { State = 0; return; }
                trigger(7, true); State = 2; return;
            case 1:
            case 2:
                if ((triggers() & 0x80) != 0) return;
                sound(SoundId.SndSolvePuzzle); puff(record.Position);
                tile((byte)(((int)record.Position.Y & 0xf0) | ((int)record.Position.X >> 4)), 0x52);
                Finished = true; return;
            default: throw new InvalidOperationException($"miscPuzzles.s: invalid panel-stair state ${State:x2}.");
        }
    }
}
