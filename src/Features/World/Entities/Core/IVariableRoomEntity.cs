using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IVariableRoomEntity
{
    void Update(double delta, Player player);
}
