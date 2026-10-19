using System;

namespace oracleofages;

internal sealed class MovementCommandScratchTrace(Action<CutsceneCommandTraceEntry> observe) : ICutsceneCommandTraceSink
{
    public void Record(CutsceneCommandTraceEntry entry)
    {
        if (entry.Phase != CutsceneCommandTracePhase.Started) observe(entry);
    }
}
