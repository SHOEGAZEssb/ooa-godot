using Godot;
using System;

namespace Oracles;

internal interface ICutsceneCommandTraceSink
{
    void Record(CutsceneCommandTraceEntry entry);

    void RecordObservation(CutsceneObservationTraceEntry entry)
    {
    }
}
