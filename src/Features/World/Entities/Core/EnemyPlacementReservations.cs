using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class EnemyPlacementReservations
{
    private readonly byte[] _positions = new byte[16];
    private int _count;

    internal int Count => _count;

    internal bool Contains(int packedPosition)
    {
        for (int index = 0; index < _count; index++)
        {
            if (_positions[index] == packedPosition)
                return true;
        }
        return false;
    }

    internal void Add(int packedPosition)
    {
        if (packedPosition is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(packedPosition));
        _positions[_count] = (byte)packedPosition;
        _count = (_count + 1) & 0x0f;
    }
}
