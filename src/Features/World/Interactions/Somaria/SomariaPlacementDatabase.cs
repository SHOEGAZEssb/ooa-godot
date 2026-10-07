using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SomariaPlacementDatabase
{
    private readonly Vector2I[] _offsets = new Vector2I[4];
    internal int CreateParameter { get; }
    internal byte Tile { get; }
    internal byte Collision { get; }
    private readonly int _zSubtract, _zBoundary, _forbiddenGroup, _forbiddenRoom, _alignY;
    internal SomariaPlacementDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/somaria_placement.tsv",
            new GeneratedTableSchema("Somaria placement",GeneratedTableKeySemantics.Unique,
                ["create-parameter","tile","collision","z-subtract","z-boundary","forbidden-group","forbidden-room","align-y","source"],
                ["create-parameter"],headerRequired:true));
        GeneratedTableRow row = table.SingleRow();
        CreateParameter=row.Decimal(0,0,255); Tile=(byte)row.HexByte(1); Collision=(byte)row.HexByte(2);
        _zSubtract=row.Decimal(3,0,255); _zBoundary=row.HexByte(4); _forbiddenGroup=row.Decimal(5,0,7);
        _forbiddenRoom=row.HexByte(6); _alignY=row.Decimal(7,-128,127); _=row.RequiredString(8);
        table=GeneratedTable.Load("res://assets/oracle/metadata/somaria_creation_offsets.tsv",
            new GeneratedTableSchema("Somaria creation offsets",GeneratedTableKeySemantics.Unique,
                ["direction","y","x","source"],["direction"],headerRequired:true));
        if (table.Rows.Count != 4) throw new InvalidOperationException("Somaria creation requires four directions.");
        for (int i=0;i<4;i++)
        {
            row=table.Rows[i];
            if (row.Decimal(0,0,3)!=i) throw row.Invalid(0,"ordered directions");
            _offsets[i]=new(row.Decimal(2,-128,127),row.Decimal(1,-128,127)); _=row.RequiredString(3);
        }
    }
    internal Vector2 CreationPosition(Vector2 link,int direction)
    {
        if (direction is <0 or >3) throw new ArgumentOutOfRangeException(nameof(direction));
        return new((byte)((int)Mathf.Floor(link.X)+_offsets[direction].X),
            (byte)((int)Mathf.Floor(link.Y)+_offsets[direction].Y));
    }
    internal Vector2 Align(Vector2 position) => new(((byte)(int)Mathf.Floor(position.X)&0xf0)+8,
        (byte)(((byte)(int)Mathf.Floor(position.Y)&0xf0)+8+_alignY));
    internal bool HeightAllowed(int z) => (byte)(z-_zSubtract)>=_zBoundary;
    internal bool RoomAllowed(int group,int room) => group!=_forbiddenGroup || room!=_forbiddenRoom;
    internal int Hazard(int mode,int tile) => ItemHazardDatabase.Shared.Hazard(mode,tile);
}
