using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface IVariableRoomEntity
{
    void Update(double delta, Player player);
}
