using Godot;
using System;

namespace oracleofages;

public sealed class PushableTileDatabase
{
    private const int CollisionModeCount = 6;
    private const int TileCount = 256;
    private const int RecordSize = 4;
    private readonly byte[] _records;

    public readonly record struct PushableTileRecord(
        byte InteractionParameter,
        byte SourceReplacement,
        byte DestinationTile,
        byte PropertyFlags)
    {
        public bool RequiresBracelet => (InteractionParameter & 0x40) != 0;
        public bool AllowsEveryDirection => (InteractionParameter & 0x80) != 0;
        public int RequiredDirection => (InteractionParameter >> 4) & 0x03;
        public bool Heavy => (PropertyFlags & 0x20) != 0;
        public bool PlaysSecretSound => (PropertyFlags & 0x80) != 0;
    }

    public PushableTileDatabase()
    {
        _records = FileAccess.GetFileAsBytes("res://assets/oracle/metadata/pushableTiles.bin");
        int expected = CollisionModeCount * TileCount * RecordSize;
        if (_records.Length != expected)
        {
            throw new InvalidOperationException(
                $"pushableTiles.bin should contain {expected} bytes, got {_records.Length}.");
        }
    }

    public bool TryGet(int activeCollisions, byte tile, out PushableTileRecord record)
    {
        if (activeCollisions < 0 || activeCollisions >= CollisionModeCount)
        {
            record = default;
            return false;
        }

        int offset = (activeCollisions * TileCount + tile) * RecordSize;
        if (_records[offset] == 0xff)
        {
            record = default;
            return false;
        }

        record = new PushableTileRecord(
            _records[offset],
            _records[offset + 1],
            _records[offset + 2],
            _records[offset + 3]);
        return true;
    }
}
