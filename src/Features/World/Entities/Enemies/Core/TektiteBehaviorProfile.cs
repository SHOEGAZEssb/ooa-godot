namespace oracleofages;

internal readonly record struct TektiteBehaviorProfile(
    int EvenSubIdWait, int OddSubIdWait, int WaitMask, int CrouchFrames,
    int SpeedRaw, int BigLeapMask, int JumpSound,
    int SmallLeapSpeedZ, int SmallLeapGravity, int BigLeapSpeedZ, int BigLeapGravity);
