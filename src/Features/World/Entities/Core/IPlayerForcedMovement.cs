using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Runs Link-owned forced state before ordinary room objects on an original
/// update. Boss initialization arms this on its preceding enemy update.
/// </summary>
internal interface IPlayerForcedMovement
{
    void UpdatePlayerForcedMovement(Player player);
}
