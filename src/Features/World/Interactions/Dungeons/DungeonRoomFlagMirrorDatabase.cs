using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class DungeonRoomFlagMirrorDatabase
{
    private readonly Dictionary<(int Id,int SubId),DungeonRoomFlagMirrorProfile> _profiles = new();
    internal DungeonRoomFlagMirrorDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/dungeon_room_flag_mirrors.tsv",
            new GeneratedTableSchema("native dungeon room-flag mirrors",GeneratedTableKeySemantics.Unique,
                ["id","subid","flag","target-room","source"],["id","subid"],headerRequired:true));
        foreach (var row in table.Rows)
        {
            byte flag = (byte)row.HexByte(2);
            if (flag == 0 || (flag&(flag-1)) != 0) throw row.Invalid(2,"a single room-flag bit");
            _profiles.Add((row.HexByte(0),row.HexByte(1)),new(flag,(byte)row.HexByte(3),row.RequiredString(4)));
        }
        if (_profiles.Count != 2) throw new InvalidOperationException("Missing INTERAC$90:$08/$09 room-flag mirrors.");
    }
    internal DungeonRoomFlagMirrorProfile Profile(DungeonObjectRecord record) =>
        _profiles.TryGetValue((record.Id,record.SubId),out var profile) ? profile :
        throw new InvalidOperationException($"{record.Source}: missing room-flag mirror profile${record.Id:x2}:${record.SubId:x2}.");
}

internal readonly record struct DungeonRoomFlagMirrorProfile(byte Flag,byte TargetRoom,string Source);
