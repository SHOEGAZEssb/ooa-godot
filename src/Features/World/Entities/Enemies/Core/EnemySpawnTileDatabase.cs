using Godot;
using System;

namespace oracleofages;

/// <summary>
/// checkTileValidForEnemySpawn's six collision-mode exception lists. A random
/// enemy normally requires a completely open collision byte and a metatile
/// absent from the selected list.
/// </summary>
internal sealed class EnemySpawnTileDatabase
{
    private const int CollisionModeCount = 6;
    private const int TilesPerMode = 256;
    private readonly byte[] _unspawnable = FileAccess.GetFileAsBytes(
        "res://assets/oracle/metadata/enemyUnspawnableTiles.bin");

    internal int RecordCount { get; }

    internal EnemySpawnTileDatabase()
    {
        int expected = CollisionModeCount * TilesPerMode;
        if (_unspawnable.Length != expected)
        {
            throw new InvalidOperationException(
                $"enemyUnspawnableTiles.bin should contain {expected} bytes, " +
                $"got {_unspawnable.Length}.");
        }
        int recordCount = 0;
        foreach (byte value in _unspawnable)
            recordCount += value != 0 ? 1 : 0;
        RecordCount = recordCount;
    }

    internal bool IsValid(int activeCollisions, TerrainInfo terrain)
    {
        if (activeCollisions is < 0 or >= CollisionModeCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(activeCollisions), activeCollisions,
                "Enemy placement requires one of the six original collision modes.");
        }

        return terrain.Collision == 0 &&
            _unspawnable[activeCollisions * TilesPerMode + terrain.Tile] == 0;
    }
}
