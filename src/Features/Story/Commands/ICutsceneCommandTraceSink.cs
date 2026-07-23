using Godot;
using System;

namespace oracleofages;

internal interface ICutsceneCommandTraceSink
{
    void Record(CutsceneCommandTraceEntry entry);

    void RecordObservation(CutsceneObservationTraceEntry entry)
    {
    }
}
