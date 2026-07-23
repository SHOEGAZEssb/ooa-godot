using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal interface IPlayerInteractable
{
    bool TryInteract(Player player);
}
