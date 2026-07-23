using Godot;
using System;

namespace oracleofages;

internal sealed record CutsceneShowTextVariantsCommand(
    CutsceneCommandSource Source,
    int StandardTextId,
    string StandardMessage,
    int LinkedTextId,
    string LinkedMessage)
    : CutsceneCommand(Source);
