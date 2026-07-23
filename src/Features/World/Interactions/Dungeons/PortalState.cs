using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal enum PortalState
{
    Initialize,
    Ready,
    WaitForLinkToLeave,
    Spinning,
    WarpRequested
}
