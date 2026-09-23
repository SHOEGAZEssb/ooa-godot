using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogTiles()
    {
        var data = new SmogControllerDatabase();
        var sequence = new SmogTileSequence();
        var writes = new List<int>();
        var puffs = new List<Vector2I>();
        bool full = false;
        bool SetTile(int position, int tile) { writes.Add(position * 256 + tile); return !full; }
        void Step(int count = 1)
        {
            for (int i = 0; i < count; i++) sequence.Update(_ => 0,SetTile,puffs.Add);
        }
        sequence.BeginGeneration(data.Phases[0],new(120,88));
        Step(4);
        FailIf(writes.Count != 0 || sequence.Counter != 1, "INTERAC$33 state6 must wait four updates before its first write.");
        full = true; Step();
        FailIf(!writes.SequenceEqual(new[] { 0x110c }) || puffs.Count != 0 || sequence.Position != new Vector2I(24,24),
            "Failed state6 write must move the interaction but retain its pointer and suppress the puff.");
        Step(5);
        FailIf(writes.Count != 2 || writes[1] != 0x110c || puffs.Count != 0,
            "A full changed-tile queue must retry the same source pair after five updates.");
        full = false; Step(5);
        FailIf(writes[^1] != 0x110c || puffs.Count != 1 || puffs[0] != new Vector2I(24,24),
            "Successful state6 write must advance once and create its puff at the metatile center.");
        Step(40);
        int[] expected = [0x110c,0x371d,0x461d,0x471d,0x481d,0x761d,0x771d,0x781d,0x871d];
        FailIf(!writes.Skip(2).SequenceEqual(expected) || puffs.Count != 9 || sequence.Complete,
            "Phase0 must write nine ordered tiles, leaving its terminator for another interval.");
        Step(4);
        FailIf(sequence.Complete, "The phase terminator must not complete before the fifth update.");
        Step();
        FailIf(!sequence.Complete || sequence.Counter != 5 || writes.Count != 11,
            "State6 terminator must hand state7 counter1=5 without another tile or puff.");
        Step(10);
        FailIf(writes.Count != 11 || sequence.Counter != 5, "Completed tile sequence must not replay writes or consume the successor's counter.");

        // Isolated stateA: partial collision is a block, only the inner 13x9
        // rectangle is scanned, and a successful removal is revisited next time.
        byte[] collisions = new byte[256];
        collisions[0x11] = 1; collisions[0x2d] = 0x80; collisions[0x9d] = 0xff;
        collisions[0x10] = collisions[0x1e] = collisions[0xa1] = 0xff;
        var reads = new List<int>(); writes.Clear(); puffs.Clear(); full = true;
        void ClearStep(int count)
        {
            for (int i = 0; i < count; i++) sequence.Update(position => { reads.Add(position); return collisions[position]; },
                (position,tile) => { writes.Add(position * 256 + tile); if (!full) collisions[position] = 0; return !full; },puffs.Add);
        }
        sequence.BeginClearing(new(24,20));
        ClearStep(5);
        FailIf(!writes.SequenceEqual(new[] { 0x11a3 }) || puffs.Count != 1 || sequence.Complete,
            "StateA must puff even when its clear-tile write fails.");
        full = false; ClearStep(5);
        FailIf(writes.Count != 2 || writes[1] != 0x11a3 || puffs.Count != 2,
            "StateA must retain its scan position across a failed write.");
        ClearStep(10);
        FailIf(!writes.SequenceEqual(new[] { 0x11a3,0x11a3,0x2da3,0x9da3 }) || sequence.Complete,
            "StateA must remove one nonzero collision tile per five updates in row-major order.");
        ClearStep(5);
        FailIf(!sequence.Complete || sequence.Counter != 5 || reads.Any(position =>
                (position & 15) is < 1 or > 13 || (position >> 4) is < 1 or > 9),
            "StateA must finish only after scanning the inner 13x9 area, excluding padding and outer walls.");
        FailIf(puffs[^1] != new Vector2I(216,152), "StateA puff must use room coordinates, including the large room's lower rows.");
        // Restart discards the previous sequence cursor and remaining wait.
        sequence.BeginGeneration(data.Phases[0],new(0,0)); Step(2);
        sequence.BeginClearing(new(24,20));
        FailIf(sequence.Counter != 5 || sequence.Position != new Vector2I(24,20) || !sequence.Clearing,
            "Starting cleanup must replace the old tile cursor and delay.");
        GD.Print("Validated isolated Smog tile-generation retries, terminator delay, cleanup rectangle/order and puff behavior.");
    }
}
