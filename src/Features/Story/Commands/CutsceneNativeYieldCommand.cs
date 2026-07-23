using Godot;
using System;

namespace oracleofages;

/// <summary>A native controller mutation that owns one fixed update.</summary>
internal sealed record CutsceneNativeYieldCommand(
    CutsceneCommandSource Source,
    string Handler)
    : CutsceneCommand(Source);
