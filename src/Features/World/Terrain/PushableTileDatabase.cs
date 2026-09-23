using Godot;
using System;

namespace oracleofages;

public sealed class PushableTileDatabase
{
    private const int CollisionModeCount = 6;
    private const int TileCount = 256;
    private const int RecordSize = 4;
    private readonly byte[] _records;
    private readonly (byte Tile,byte Parameter)[] _somaria=new (byte,byte)[6];

    public PushableTileDatabase()
    {
        var table=GeneratedTable.Load("res://assets/oracle/metadata/somaria_push_tiles.tsv",
            new GeneratedTableSchema("Somaria push dispatch",GeneratedTableKeySemantics.Unique,
                ["active-collisions","tile","parameter","source"],["active-collisions"],headerRequired:true));
        if(table.Rows.Count!=6) throw new InvalidOperationException("Somaria push dispatch requires all six collision modes.");
        for(int mode=0;mode<6;mode++)
        {
            var row=table.Rows[mode];
            if(row.Decimal(0,0,5)!=mode) throw row.Invalid(0,"ordered collision mode");
            _somaria[mode]=((byte)row.HexByte(1),(byte)row.HexByte(2));
            if((_somaria[mode].Parameter&15)!=0) throw row.Invalid(2,"nextToPushableBlock dispatch");
            _=row.RequiredString(3);
        }
        _records = FileAccess.GetFileAsBytes("res://assets/oracle/metadata/pushableTiles.bin");
        int expected = CollisionModeCount * TileCount * RecordSize;
        if (_records.Length != expected)
        {
            throw new InvalidOperationException(
                $"pushableTiles.bin should contain {expected} bytes, got {_records.Length}.");
        }
    }

    internal bool TryGetSomaria(int mode,byte tile,out byte parameter)
    {
        parameter=0;
        if(mode is <0 or >=6 || _somaria[mode].Tile!=tile) return false;
        parameter=_somaria[mode].Parameter;
        return true;
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

public readonly record struct PushableTileRecord(byte InteractionParameter, byte SourceReplacement, byte DestinationTile, byte PropertyFlags)
{
    public bool RequiresBracelet => (InteractionParameter & 0x40) != 0;
    public bool AllowsEveryDirection => (InteractionParameter & 0x80) != 0;
    public int RequiredDirection => (InteractionParameter >> 4) & 0x03;
    public bool PlaysSecretSound => (PropertyFlags & 0x80) != 0;
}
