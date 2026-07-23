using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Contributes to the single wLinkRidingObject-style support state consumed
/// by Link's terrain handlers.
/// </summary>
internal interface IPlayerRideableRoomEntity
{
    bool LinkRiding { get; }
}
