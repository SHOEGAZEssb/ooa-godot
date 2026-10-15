using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MakuSeedDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<(int, int), NpcRecord> _actors = [];
    private readonly Dictionary<(int, int), string[]> _sprites = [];
    private readonly Dictionary<(int, int), int> _sourceOffsets = [];
    private readonly Dictionary<(int, int), string> _animations = [];
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _commands = [];
    private readonly Dictionary<int, string> _texts = [];
    private readonly Dictionary<string, int[]> _tables = [];
    internal MakuTreeAdviceDatabase Maku { get; } = new();
    internal Color[] SeedPalette { get; }

    internal MakuSeedDatabase()
    {
        foreach (var row in GeneratedTable.Load(Root + "maku_seed_visuals.tsv",
            new("Maku Seed actor graphics", GeneratedTableKeySemantics.Unique,
                ["id", "subid", "sprites", "tile-base", "palette", "animation", "source-offset", "source"], ["id", "subid"], headerRequired: true)).Rows)
        {
            int id = row.HexByte(0), subid = row.HexByte(1);
            string[] sprites = row.SplitRequired(2, ',');
            string animation = row.RequiredString(5);
            _sprites.Add((id, subid), sprites);
            _sourceOffsets.Add((id, subid), row.HexWord(6));
            _actors.Add((id, subid), new(0, 0x38, id, subid, 0, 0, 0, 0, sprites[0],
                row.Decimal(3), row.Decimal(4), 0, false, animation, animation, animation, animation,
                "", NpcImplementationClassification.EventOwned));
        }
        foreach (var row in GeneratedTable.Load(Root + "maku_seed_animations.tsv",
            new("Maku Seed animations", GeneratedTableKeySemantics.Unique,
                ["id", "index", "animation", "source"], ["id", "index"], headerRequired: true)).Rows)
            _animations.Add((row.HexByte(0), row.HexByte(1)), row.RequiredString(2));
        foreach (string script in new[] { "tree-seed", "tree-return", "tree-repeat", "cloaked", "twinrova" })
            _commands.Add(script, CutsceneCommandCatalog.Load(Root + $"maku_seed_{script}_commands.tsv"));
        foreach (var row in GeneratedTable.Load(Root + "maku_seed_texts.tsv",
            new("Maku Seed dialogue", GeneratedTableKeySemantics.Unique,
                ["text-id", "message-base64", "source"], ["text-id"], headerRequired: true)).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        foreach (var row in GeneratedTable.Load(Root + "maku_seed_tables.tsv",
            new("Maku Seed native tables", GeneratedTableKeySemantics.Unique,
                ["profile", "values", "source"], ["profile"], headerRequired: true)).Rows)
            _tables.Add(row.RequiredString(0), Array.ConvertAll(row.SplitRequired(1, ','), value => Convert.ToInt32(value, 16)));
        var palette = OracleGraphicsData.LoadPalette(Root + "maku_seed_palette.bin", 1, 7);
        SeedPalette = [palette[7, 0], palette[7, 1], palette[7, 2], palette[7, 3]];
    }

    internal NpcCharacter CreateActor(int id, int subid, Vector2 position)
    {
        var actor = NpcCharacter.CreateFromRecord(_actors[(id, subid)] with { X = (int)position.X, Y = (int)position.Y });
        foreach (string sprite in _sprites[(id, subid)][1..]) actor.AppendScriptGraphics(sprite);
        actor.SetGraphicsSourceOffset(_sourceOffsets[(id, subid)]);
        actor.SetScriptAnimation(_animations[(id, 0)]);
        actor.SetAnimationRate(0);
        actor.SetBlocksLink(false);
        return actor;
    }
    internal string Animation(int id, int animation) => _animations[(id, animation)];
    internal IReadOnlyList<CutsceneCommand> Commands(string script) => _commands[script];
    internal string Text(int id) => _texts[id];
    internal int[] Table(string name) => _tables[name];
}
