using System;

namespace oracleofages;

// Enemy.var36. The actor calls Reset after choosing its idle animation, and
// Advance only in the source movement/shooting path (not while counter2 pauses).
internal sealed class SmogFireTimer(SmogFireTimerDatabase data)
{
    internal int Remaining { get; private set; }

    internal void Reset(int subid, int phase, Func<int> nextRandom) =>
        Remaining = data.Select(subid, phase, nextRandom());

    // smog_decCounterToFireProjectile returns NZ only on the 1 -> 0 update.
    // Zero is quiescent, not an immediate/repeating request to shoot.
    internal bool Advance()
    {
        if (Remaining == 0) return false;
        Remaining--;
        return Remaining == 0;
    }
}
