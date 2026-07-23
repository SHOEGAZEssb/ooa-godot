using Godot;
using System;

namespace oracleofages;

internal readonly record struct CutsceneCommandTraceEntry(
    int ScriptUpdate,
    CutsceneCommandSource Source,
    CutsceneCommandTracePhase Phase,
    int Counter,
    int NextCommandIndex);
