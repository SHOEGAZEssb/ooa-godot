using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal interface IPlayerInteractable
{
    bool TryInteract(Player player);
}
