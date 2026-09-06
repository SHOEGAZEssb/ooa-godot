using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class CompanionForestDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<(int, int), int> _rooms = new();
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<int, IReadOnlyList<CutsceneCommand>> _commands = new();
    private readonly string _sprite;
    private readonly int _tileBase;
    private readonly string _animation;
    private readonly NpcRecord _exclamation;
    internal int Role(int group, int room) => _rooms.GetValueOrDefault((group, room), -1);
    internal string Text(int id) => _texts[id];
    internal IReadOnlyList<CutsceneCommand> Commands(int subId) => _commands[subId];

    internal CompanionForestDatabase()
    {
        foreach (int sub in new[] { 8, 9, 10, 11 })
            _commands.Add(sub, CutsceneCommandCatalog.Load(Root + $"companion_forest_{sub:x2}.tsv"));
        foreach (var row in GeneratedTable.Load(Root + "companion_forest_rooms.tsv",
            new GeneratedTableSchema("Companion forest placements", GeneratedTableKeySemantics.Ordered,
                ["group", "room", "subid", "source"], headerRequired: true)).Rows)
            _rooms.Add((row.HexByte(0), row.HexByte(1)), row.HexByte(2));
        foreach (var row in GeneratedTable.Load(Root + "companion_forest_text.tsv",
            new GeneratedTableSchema("Companion forest dialogue", GeneratedTableKeySemantics.Ordered,
                ["text-id", "text-base64", "source"], headerRequired: true)).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        var visual = GeneratedTable.Load(Root + "companion_forest_flute.tsv",
            new GeneratedTableSchema("Companion flute graphic", GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "animation-base64", "source"], headerRequired: true)).SingleRow();
        _sprite = visual.RequiredString(0); _tileBase = visual.UnsignedDecimal(1); _animation = visual.Base64Utf8(2);
        var mark = GeneratedTable.Load(Root + "companion_forest_exclamation.tsv",
            new GeneratedTableSchema("Companion exclamation", GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "animation-base64", "source"], headerRequired: true)).SingleRow();
        string animation = mark.Base64Utf8(3);
        _exclamation = new NpcRecord(0, 0, 0x9f, 0, 0, 0, 0, 0, mark.RequiredString(0),
            mark.UnsignedDecimal(1), mark.UnsignedDecimal(2), 0, false,
            animation, animation, animation, animation, string.Empty, NpcImplementationClassification.EventOwned);
    }

    internal NpcRecord FluteRecord(int group, int room, Vector2 point) => new(
        group, room, 0x71, 0x0a, (int)point.Y, (int)point.X, 0, 0,
        _sprite, _tileBase, 2, 0, false, _animation, _animation, _animation, _animation,
        string.Empty, NpcImplementationClassification.EventOwned);
    internal NpcRecord ExclamationRecord(int room, Vector2 point) => _exclamation with { Room = room, X = (int)point.X, Y = (int)point.Y };
}
