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
    bool AlternatesMovementWithSwordRestriction => true;
    bool DisablesMenus => false;
    bool DisablesRingTransformations => false;
    bool DisablesScreenTransitions => false;
    bool DisablesWarpTiles => false;
    bool MenuDisablesWarpTiles => false;
    bool DisablesPlayerContact => false;
    bool DisablesCompanion => false;
    bool PassesNpcs => false;
}
