using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class DungeonBossKeyMirrorDatabase
{
    private readonly Dictionary<(int Id,int SubId),int> _dungeons = new();
    internal DungeonBossKeyMirrorDatabase()
    {
        var table=GeneratedTable.Load("res://assets/oracle/objects/dungeon_boss_key_mirrors.tsv",
            new GeneratedTableSchema("Dungeon boss-key mirrors",GeneratedTableKeySemantics.Unique,
                ["id","subid","dungeon","source"],["id","subid"],headerRequired:true));
        foreach (var row in table.Rows)
        {
            int dungeon=row.HexByte(2); _=row.RequiredString(3);
            if (dungeon >= 16) throw row.Invalid(2,"a dungeon bit$00..$0f");
            _dungeons.Add((row.HexByte(0),row.HexByte(1)),dungeon);
        }
    }
    internal int Dungeon(DungeonObjectRecord record) => _dungeons.TryGetValue((record.Id,record.SubId),out int dungeon)
        ? dungeon : throw new InvalidOperationException($"{record.Source}: missing boss-key mirror INTERAC${record.Id:x2}:${record.SubId:x2}.");
}
