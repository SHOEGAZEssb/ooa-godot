using Godot;
using System;

namespace oracleofages;

internal readonly record struct CutsceneObservationTraceEntry(
    int Frame,
    string Event,
    string Observation,
    CutsceneActorId? Actor,
    int Value,
    Vector2 Position);
