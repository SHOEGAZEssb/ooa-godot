using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IBraceletInteractableRoomEntity
{
    bool TryUseBracelet(Player player);
}
