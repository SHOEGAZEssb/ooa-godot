using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface IBraceletInteractableRoomEntity
{
    bool TryUseBracelet(Player player, Vector2I releaseDirection);
}
