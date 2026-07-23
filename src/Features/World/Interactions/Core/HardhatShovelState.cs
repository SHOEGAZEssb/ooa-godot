using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum HardhatShovelState
{
    None,
    AwaitOpeningClose,
    PreRewardWait,
    AwaitRewardClose,
    PostRewardWait,
    AwaitFinalClose,
    AwaitSimpleClose
}
