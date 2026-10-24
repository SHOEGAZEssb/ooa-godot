using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MonkeyDatabase
{
    private const string Root = "res://assets/oracle/objects/monkey_";
    private readonly Dictionary<int, NpcRecord> _visuals = new();
    private readonly Dictionary<(int Id, int Index), string> _animations = new();
    private readonly Dictionary<string, int[]> _tables = new();
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _scripts =
        CutsceneCommandCatalog.LoadPrograms("res://assets/oracle/cutscenes/monkey_scripts.tsv");
    internal MonkeyDatabase()
    {
        foreach (var row in GeneratedTable.Load(Root + "visuals.tsv", new("monkey graphics", GeneratedTableKeySemantics.Unique,
            ["id", "subid", "sprite", "tile-base", "palette", "animation", "source"], ["id"], headerRequired: true)).Rows)
        {
            string animation = row.RequiredString(5); int id = row.HexByte(0);
            _visuals.Add(id, new(0, 0x5a, id, row.HexByte(1), 0, 0, 0, 0, row.RequiredString(2),
                row.UnsignedDecimal(3), row.Decimal(4, 0, 7), 0, false, animation, animation, animation, animation,
                "", NpcImplementationClassification.SpecializedNative));
        }
        foreach (var row in GeneratedTable.Load(Root + "animations.tsv", new("monkey animations", GeneratedTableKeySemantics.Unique,
            ["id", "index", "animation", "source"], ["id", "index"], headerRequired: true)).Rows)
            _animations.Add((row.HexByte(0), row.HexByte(1)), row.RequiredString(2));
        foreach (var row in GeneratedTable.Load(Root + "tables.tsv", new("monkey native tables", GeneratedTableKeySemantics.Unique,
            ["name", "values", "source"], ["name"], headerRequired: true)).Rows)
            _tables.Add(row.RequiredString(0), Array.ConvertAll(row.SplitRequired(1, ','), v => Convert.ToInt32(v, 16)));
        foreach (var row in GeneratedTable.Load(Root + "texts.tsv", new("monkey dialogue", GeneratedTableKeySemantics.Unique,
            ["id", "message-base64", "source"], ["id"], headerRequired: true)).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
    }
    internal NpcCharacter CreateActor(DungeonObjectRecord record, bool accessory = false)
    {
        int id = accessory ? 0x63 : 0x39;
        var actor = NpcCharacter.CreateFromRecord(_visuals[id] with { Group = record.Group, Room = record.Room,
            Var03 = accessory ? 1 : record.Var03, X = record.X, Y = record.Y });
        actor.SetAnimationRate(0); actor.SetDialogue(0, "", false); actor.TimeWarpSolidMarked = false;
        actor.SetCollisionRadii(0, 0); actor.Visible = false; return actor;
    }
    internal (Vector2 Position, int Animation) Placement(int index)
    {
        if (index is < 0 or > 9) throw new InvalidOperationException($"INTERAC $39:$05 invalid var03 ${index:x2}.");
        var table = _tables["positions"]; return (new(table[index * 4 + 1], table[index * 4]), table[index * 4 + 3]);
    }
    internal int Jump(int index) => unchecked((short)_tables["jumps"][index]);
    internal int[] BowtieOffsets => _tables["bowtie"];
    internal string Animation(int id, int index) => _animations[(id, index)];
    internal string Text(int id) => _texts[id];
    internal IReadOnlyList<CutsceneCommand> Script(bool bowtie) => _scripts[bowtie ? "monkeySubid5Script_bowtieMonkey" : "monkeySubid5Script"];
}
