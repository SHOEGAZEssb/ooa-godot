using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// dungeonScripts.s:spawnChestAfterPuff. playsound and createpuff each
// return with carry clear; wait installs counter1 on a third script update.
internal sealed class ChestAfterPuffScript(int wait)
{
    private int _phase;
    internal int Counter { get; private set; } = -1;
    internal bool Advance(bool triggered,Vector2 position,ICollection<RoomEntitySpawn> spawns,
        Action<int> sound,Action writeChest)
    {
        if (_phase == 0)
        {
            if (!triggered) return false;
            sound(SoundId.SndSolvePuzzle); _phase = 1; return false;
        }
        if (_phase == 1)
        {
            spawns.Add(new PuzzlePuffSpawn(position,SoundId.SndPoof)); _phase = 2; return false;
        }
        if (_phase == 2) { Counter = wait; _phase = 3; return false; }
        if (--Counter != 0) return false;
        // settilehere forces carry set even if setTile rejects the write;
        // scriptend therefore executes in this same update.
        writeChest(); return true;
    }
}
