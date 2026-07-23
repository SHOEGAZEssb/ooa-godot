using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneWaitCommand(CutsceneCommandSource Source, int Frames)
    : CutsceneCommand(Source);
