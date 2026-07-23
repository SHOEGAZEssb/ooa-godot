using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum LinkedGhiniState
{
    None,
    AwaitOfferChoice,
    AwaitRefusalClose,
    AwaitExplanationChoice,
    AwaitSecretChoice,
    AwaitFinalClose
}
