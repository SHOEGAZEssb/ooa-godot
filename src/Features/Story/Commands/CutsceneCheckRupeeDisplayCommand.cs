namespace oracleofages;

internal sealed record CutsceneCheckRupeeDisplayCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);
