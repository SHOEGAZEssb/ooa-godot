namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static readonly bool[] SplitRomHostSchedule = [false];
    private static readonly bool[] BothRomHostSchedules = [false, true];

    // Every source fixture runs through the real application loop, with an
    // observer after each update. Repeat the first fixture of each independent
    // matrix in one host frame to verify its input/owner handoff as well.
    // Scheduler and input-buffer regressions separately exercise batching.
    private static bool[] RomHostSchedules(int caseIndex) =>
        caseIndex == 0 ? BothRomHostSchedules : SplitRomHostSchedule;
}
