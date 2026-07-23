using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IPlayerRestriction
{
    bool DisablesSword { get; }
    bool DisablesItems => false;
    bool DisablesMovement => false;
    bool DisablesMenus => false;
    bool DisablesRingTransformations => false;
}
