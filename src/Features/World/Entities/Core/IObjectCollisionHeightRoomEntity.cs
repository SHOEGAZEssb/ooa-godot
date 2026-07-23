using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Object.zh used by the original item/enemy collision pass. Entities without
/// this capability are ordinary ground-height targets (Z = 0).
/// </summary>
internal interface IObjectCollisionHeightRoomEntity
{
    int CollisionZ { get; }
}
