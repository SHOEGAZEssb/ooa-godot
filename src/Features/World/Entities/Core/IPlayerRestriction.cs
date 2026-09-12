using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IPlayerRestriction
{
    bool DisablesSword { get; }
    bool FreezesPlayerUpdates => false;
    bool DisablesItems => false;
    bool DisablesMovement => false;
    bool DisablesMenus => false;
    bool DisablesRingTransformations => false;
    bool DisablesScreenTransitions => false;
    bool DisablesWarpTiles => false;
    bool DisablesPlayerContact => false;
    bool DisablesCompanion => false;
}
