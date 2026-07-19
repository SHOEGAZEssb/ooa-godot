using System;

namespace oracleofages;

/// <summary>
/// Runtime equivalent of wEnemiesKilledList: eight room IDs paired with
/// seven usable killed-enemy bits. This is deliberately transient and is not
/// part of save data.
/// </summary>
internal sealed class RecentEnemyDefeats
{
    private const int RoomCount = 8;
    private readonly byte[] _rooms = new byte[RoomCount];
    private readonly byte[] _killedEnemies = new byte[RoomCount];
    private int _tail;
    private int _activeRoom = -1;

    internal void BeginRoom(int room)
    {
        if (room is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(room));

        _activeRoom = room;
        if (FindRoom(room) >= 0)
            return;

        _rooms[_tail] = (byte)room;
        _killedEnemies[_tail] = 0;
        _tail = (_tail + 1) & (RoomCount - 1);
    }

    internal bool WasKilled(int enemyIndex)
    {
        if (enemyIndex == 0)
            return false;
        ValidateEnemyIndex(enemyIndex);
        int slot = FindRoom(_activeRoom);
        return slot >= 0 && (_killedEnemies[slot] & (1 << enemyIndex)) != 0;
    }

    internal void MarkKilled(int enemyIndex)
    {
        if (enemyIndex == 0)
            return;
        ValidateEnemyIndex(enemyIndex);
        int slot = FindRoom(_activeRoom);
        if (slot >= 0)
            _killedEnemies[slot] |= (byte)(1 << enemyIndex);
    }

    internal void Clear()
    {
        Array.Clear(_rooms);
        Array.Clear(_killedEnemies);
        _tail = 0;
        _activeRoom = -1;
    }

    private int FindRoom(int room)
    {
        if (room < 0)
            return -1;
        for (int index = 0; index < RoomCount; index++)
        {
            if (_rooms[index] == room)
                return index;
        }
        return -1;
    }

    private static void ValidateEnemyIndex(int enemyIndex)
    {
        if (enemyIndex is < 1 or > 7)
            throw new ArgumentOutOfRangeException(nameof(enemyIndex));
    }
}
