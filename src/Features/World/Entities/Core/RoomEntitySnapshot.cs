using System;
using System.Buffers;
using System.Collections.Generic;

namespace Oracles;

// A scoped membership snapshot. Each nested scan owns its own rented array,
// so callbacks may mutate the live list or enter another collision scan.
internal readonly ref struct RoomEntitySnapshot
{
    private readonly IRoomEntity[] _entities;
    private readonly int _count;

    internal RoomEntitySnapshot(List<IRoomEntity> source)
    {
        _count = source.Count;
        _entities = _count == 0 ? Array.Empty<IRoomEntity>() : ArrayPool<IRoomEntity>.Shared.Rent(_count);
        source.CopyTo(_entities);
    }

    internal ReadOnlySpan<IRoomEntity> Entities => _entities.AsSpan(0, _count);

    public void Dispose()
    {
        if (_count != 0) ArrayPool<IRoomEntity>.Shared.Return(_entities, clearArray: true);
    }
}
