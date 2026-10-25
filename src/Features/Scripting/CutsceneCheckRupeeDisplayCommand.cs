namespace Oracles;

internal sealed record CutsceneCheckRupeeDisplayCommand(CutsceneCommandSource Source)
    : CutsceneCommand(Source);
