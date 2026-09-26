using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace oracleofages;

internal sealed class GoronCaveDatabase
{
    internal IReadOnlyList<CutsceneCommand> Commands { get; } =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/goron_cave_commands.tsv");
    private readonly Dictionary<string, GeneratedTableRow> _rows = new();
    private readonly Dictionary<(int, int), NpcRecord> _effects = new();
    private readonly Dictionary<string, int[]> _tables = new();

    internal GoronCaveDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/cutscenes/goron_cave_data.tsv",
            new GeneratedTableSchema("Goron cave", GeneratedTableKeySemantics.Unique,
                ["kind", "id", "value", "position"], ["kind", "id"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            _rows.Add(row.RequiredString(0) + ":" + row.RequiredString(1), row);
            if (row.RequiredString(0) == "bytes")
                _tables.Add(row.RequiredString(1), row.RequiredString(2).Split(',')
                    .Select(value => Convert.ToInt32(value, 16)).ToArray());
        }
        var effects = GeneratedTable.Load("res://assets/oracle/cutscenes/goron_cave_effects.tsv",
            new GeneratedTableSchema("Goron effects", GeneratedTableKeySemantics.Unique,
                ["id", "subid", "sprite", "tile-base", "palette", "animation"], ["id", "subid"], headerRequired: true));
        foreach (var row in effects.Rows)
        {
            int id = row.HexByte(0), sub = row.HexByte(1);
            string animation = row.RequiredString(5);
            _effects.Add((id, sub), new(5,0xc3,id,sub,0,0,0,0,row.RequiredString(2),
                row.HexByte(3),row.HexByte(4),0,false,animation,animation,animation,animation,"",
                NpcImplementationClassification.EventOwned));
        }
    }

    internal int Entry(string script) => _rows["entry:" + script].UnsignedDecimal(2);
    internal string Reward(int id,int subid) => _rows[$"reward:{id:x2}:{subid:x2}"].RequiredString(2);
    internal NpcRecord Effect(int id, int sub) => _effects[(id, sub)];
    internal int[] Bytes(string key) => _tables[key];
    internal string Animation(int id, int animation) => _rows[$"animation:{id:x2}:{animation}"].RequiredString(2);
    internal (string Message, int Position) Text(int id)
    {
        var row = _rows[$"text:{id:x4}"];
        return (Encoding.UTF8.GetString(Convert.FromBase64String(row.RequiredString(2))), row.UnsignedDecimal(3));
    }
    internal NpcRecord Actor(int id, int subid, int x, int y)
    {
        var row = _rows[$"sprite:{id:x2}"];
        return new(5, 0xc3, id, subid, y, x, 0, 0, row.RequiredString(2),
            row.UnsignedDecimal(3), Bytes($"palette-{id:x2}")[0], 2, true, Animation(id, 0), Animation(id, 1),
            Animation(id, 2), Animation(id, 3), "", NpcImplementationClassification.EventOwned);
    }
    internal NpcRecord BombRecord => new(0,0,InteractionId.ForestFairy,0,0,0,0,0,
        _rows["sprite:49"].RequiredString(2),0x10,4,0,false,
        Animation(0x49,0),Animation(0x49,0),Animation(0x49,0),Animation(0x49,0),"",
        NpcImplementationClassification.EventOwned);
}
