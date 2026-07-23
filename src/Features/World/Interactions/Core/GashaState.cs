using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum GashaState
{
    None,
    AwaitNoSeedsClose,
    AwaitPlantChoice,
    AwaitNutIntroClose,
    AwaitRewardClose,
    AwaitDisplayedCounters,
    AwaitDisappearance
}
