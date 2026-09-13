namespace oracleofages;

internal readonly record struct LeverBehavior(
    int LeverLength, int PullSpeed, int LeverRadiusY, int LeverRadiusX,
    int LinkYOffset, int ConnectionStep, int MoveSound, int FullSound);
