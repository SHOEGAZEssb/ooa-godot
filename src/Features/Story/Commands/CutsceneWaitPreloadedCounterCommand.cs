using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Decrements a counter installed by an earlier native operation. Unlike
/// <see cref="CutsceneWaitCommand"/>, the first runner update consumes a frame.
/// </summary>
internal sealed record CutsceneWaitPreloadedCounterCommand(
    CutsceneCommandSource Source)
    : CutsceneCommand(Source);
