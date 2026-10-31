using System;

namespace Oracles;

internal static class MainThreadClock
{
    internal static void Advance(OracleSaveData save, OracleRuntimeState state)
    {
        if (save.Game != state.Game)
            throw new ArgumentException("The main-thread clock requires the active game's save and runtime state.");
        // bank0.s:mainThreadStart@mainThread publishes the low playtime byte
        // before runGameLogic, even when menus or cutscenes omit object updates.
        save.AdvancePlaytime();
        state.FrameCounter = save.ReadField("wPlaytimeCounter");
    }
}
