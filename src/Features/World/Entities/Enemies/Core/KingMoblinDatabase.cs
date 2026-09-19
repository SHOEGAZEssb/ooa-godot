using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KingMoblinDatabase
{
    private readonly Dictionary<string, byte[]> _tables = new();
    private readonly Dictionary<(int, int), ImportedEnemyDefinition> _actors = new();
    private readonly Dictionary<int, string> _texts = new();
    internal IReadOnlyDictionary<int, Color[]> Palettes { get; }
    internal KingMoblinDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/king_moblin_tables.tsv",
            new GeneratedTableSchema("King Moblin native tables", GeneratedTableKeySemantics.Unique,
                ["profile", "values", "source"], ["profile"], headerRequired: true));
        foreach (var row in table.Rows)
            _tables.Add(row.RequiredString(0), Array.ConvertAll(row.SplitRequired(1, ','), v => Convert.ToByte(v, 16)));
        foreach (var (name, count) in new[] { ("speeds",6), ("pickup",6), ("raise",6), ("explosions",4),
            ("minions",8), ("escape-angles",2), ("fuses",4), ("flashes",6),
            ("GLOBALFLAG_MOBLINS_KEEP_DESTROYED",1), ("GLOBALFLAG_16",1), ("ledge",5), ("collision",32) })
            if (!_tables.TryGetValue(name, out var values) || values.Length != count)
                throw new InvalidOperationException($"ENEMY_KING_MOBLIN $7f: missing source table {name}.");
        table = GeneratedTable.Load("res://assets/oracle/objects/king_moblin_actors.tsv",
            new GeneratedTableSchema("King Moblin actors", GeneratedTableKeySemantics.Unique,
                ["id","subid","sprites","tile-base","palette","source-grayscale-inverted","radius-y","radius-x","damage-quarters","health","animations-base64"],
                ["id","subid"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            var record = new ImportedEnemyDefinition(row.HexByte(0),row.HexByte(1),row.SplitRequired(2,','),
                row.UnsignedDecimal(3),row.UnsignedDecimal(4),row.Boolean01(5),row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),row.UnsignedDecimal(8),row.UnsignedDecimal(9),row.EncodedAnimations(10));
            _actors.Add((record.Id,record.SubId), record);
        }
        if (_actors.Count != 5 || Actor(0x7f).Health != 6)
            throw new InvalidOperationException("ENEMY_KING_MOBLIN $7f: incomplete native actor definitions.");
        table = GeneratedTable.Load("res://assets/oracle/objects/king_moblin_text.tsv",
            new GeneratedTableSchema("King Moblin dialogue", GeneratedTableKeySemantics.Unique,
                ["text-id","message-base64","source"],["text-id"],headerRequired:true));
        foreach(var row in table.Rows) _texts.Add(row.HexWord(0),row.Base64Utf8(1));
        if(_tables.Count!=12 || _texts.Count!=2 || !_texts.ContainsKey(0x2f19) || !_texts.ContainsKey(0x2f1a))
            throw new InvalidOperationException("ENEMY_KING_MOBLIN $7f: unexpected native table or dialogue keys.");
        var palette = OracleGraphicsData.LoadPalette("res://assets/oracle/objects/king_moblin_palette.bin",1,6);
        Palettes = new Dictionary<int, Color[]> { [6] = [palette[6,0],palette[6,1],palette[6,2],palette[6,3]] };
    }
    internal byte[] Bytes(string key) => _tables[key];
    internal ImportedEnemyDefinition Actor(int id, int subid=0) => _actors[(id,subid)];
    internal string Text(int id) => _texts[id];
    internal void ApplyRoomLayout(int group,OracleRoomData room,long tick)
    {
        var row=Bytes("ledge");
        if(group!=row[0] || room.Id!=row[1]) return;
        // roomTileChangesAfterLoad05 writes wRoomLayout only, retaining the
        // already generated wall pixels and wRoomCollisions at $33-$36.
        for(int i=0;i<row[3];i++)
        {
            int packed=row[2]+i;
            var position=new Vector2((packed&15)*16,(packed>>4)*16);
            int previous=room.GetMetatile(position);
            if(previous==row[4]) continue;
            room.SetPositionTileAndCollision(position,row[4],room.Collisions[previous],tick,preserveRenderedTile:true);
        }
    }
}
