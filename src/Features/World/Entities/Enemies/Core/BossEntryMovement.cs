using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BossEntryMovement(Vector2I direction)
{
    private const int ForceMovementCounter = 0x16;
    private bool _armed;
    private bool _initialized;
    private int _counter;

    internal int Counter => _counter;
    internal bool Active => _armed;

    internal void Arm()
    {
        if (direction == Vector2I.Zero)
            return;
        _counter = ForceMovementCounter;
        _armed = true;
    }

    internal void Update(Player player)
    {
        if (!_armed)
            return;
        if (!_initialized)
        {
            _initialized = true;
            player.BeginForcedRoomEntryMovement(direction);
            return;
        }

        _counter--;
        if (_counter != 0)
        {
            player.AdvanceForcedRoomEntryMovement(direction);
            return;
        }
        player.EndForcedRoomEntryMovement();
        _armed = false;
    }
}
