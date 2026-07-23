using Godot;
using System;

namespace oracleofages;

/// <summary>
/// A bespoke object-code handler retained outside the script interpreter. The
/// shared runner owns its command boundary while the event host owns its state.
/// </summary>
internal sealed record CutsceneNativeBlockingCommand(
    CutsceneCommandSource Source,
    string Handler,
    CutsceneActorId? Actor,
    int Frames,
    string Payload)
    : CutsceneCommand(Source);
