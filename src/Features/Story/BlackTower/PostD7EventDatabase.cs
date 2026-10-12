using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>Temporary source object streams and script lanes for the post-D7 story.</summary>
internal sealed class PostD7EventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<string, int[]> _tables = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Id, int Index), string> _animations = [];
    private readonly Dictionary<(int Id, int SubId), NpcRecord> _visuals = [];
    private readonly Dictionary<(int Id, int SubId), int> _sourceOffsets = [];
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _scripts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<PostD7Placement>> _placements = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> _rabbitTables = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _texts = [];
    internal Color[] PossessedPalette { get; } = OracleGraphicsData.LoadPaletteColors(
        "res://assets/oracle/metadata/nayru_possessed_palette.bin", transparentZero: true);
    internal Color[] StonePalette { get; } = OracleGraphicsData.LoadPaletteColors(
        Root + "nayru_stone_sprite_palette.bin", transparentZero: true);

    internal PostD7EventDatabase()
    {
        foreach (var row in GeneratedTable.Load(Root + "post_d7_tables.tsv",
            new("post-D7 native tables", GeneratedTableKeySemantics.Unique,
                ["key", "values", "source"], ["key"], headerRequired: true)).Rows)
            _tables.Add(row.RequiredString(0), Array.ConvertAll(row.SplitRequired(1, ','), value => Convert.ToInt32(value, 16)));
        foreach (var row in GeneratedTable.Load(Root + "post_d7_rabbit_tables.tsv",
            new("post-D7 rabbit tables", GeneratedTableKeySemantics.Unique,
                ["key", "values", "source"], ["key"], headerRequired: true)).Rows)
            _rabbitTables.Add(row.RequiredString(0), Array.ConvertAll(row.SplitRequired(1, ','), value => Convert.ToInt32(value, 16)));
        foreach (var row in GeneratedTable.Load(Root + "post_d7_texts.tsv",
            new("post-D7 native dialogue", GeneratedTableKeySemantics.Unique,
                ["text-id", "message-base64", "source"], ["text-id"], headerRequired: true)).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        foreach (var row in GeneratedTable.Load(Root + "post_d7_animations.tsv",
            new("post-D7 animations", GeneratedTableKeySemantics.Unique,
                ["id", "index", "animation", "source"], ["id", "index"], headerRequired: true)).Rows)
            _animations.Add((row.HexByte(0), row.HexByte(1)), row.RequiredString(2));
        foreach (var row in GeneratedTable.Load(Root + "post_d7_visuals.tsv",
            new("post-D7 graphics", GeneratedTableKeySemantics.Unique,
                ["id", "subid", "sprite", "tile-base", "palette", "default-animation", "source-offset", "source"],
                ["id", "subid"], headerRequired: true)).Rows)
        {
            int id = row.HexByte(0), subId = row.HexByte(1), animation = row.UnsignedDecimal(5);
            _sourceOffsets.Add((id, subId), row.HexWord(6));
            string encoded = Animation(id, animation);
            _visuals.Add((id, subId), new(0, 0, id, subId, 0, 0, 0, 0, row.RequiredString(2),
                row.UnsignedDecimal(3), row.UnsignedDecimal(4), animation, false,
                encoded, encoded, encoded, encoded, string.Empty, NpcImplementationClassification.EventOwned));
        }
        foreach (var row in GeneratedTable.Load(Root + "post_d7_placements.tsv",
            new("post-D7 temporary object streams", GeneratedTableKeySemantics.Unique,
                ["scene", "order", "id", "subid", "y", "x", "var03", "source"],
                ["scene", "order"], headerRequired: true)).Rows)
        {
            string scene = row.RequiredString(0);
            if (!_placements.TryGetValue(scene, out var entries)) _placements.Add(scene, entries = []);
            if (row.UnsignedDecimal(1) != entries.Count) throw row.Invalid(1, "contiguous source object order");
            entries.Add(new(row.HexByte(2), row.HexByte(3), row.HexByte(4), row.HexByte(5), row.HexByte(6)));
        }
        foreach (string script in new[] { "linked", "ascent", "ambi", "boy", "father", "observer" })
            _scripts.Add(script, CutsceneCommandCatalog.Load(Root + $"post_d7_{script}_commands.tsv"));
        if (!Table("entry").SequenceEqual(new[] { 2, 0x90, 0x6b, 0x11, 0x40, 0x40, 60 }) ||
            !Table("tower").SequenceEqual(new[] { 0x70, 120, 4, 0xb1, 120, 4, 0xf6, 0x47, 0x0f, 3 }) ||
            Placements("family").Count != 4 || Placements("linked").Count != 3 ||
            Placements("return")[2] is not { Id: 0x8a, SubId: 0, Var03: 9 })
            throw new InvalidOperationException("Post-D7 source closure lacks $6b:$11, completed tower, stone family, or dynamic $8a/v$09.");
    }

    internal int[] Table(string key) => _tables.TryGetValue(key, out var values) ? values :
        throw new InvalidOperationException($"Post-D7 source table '{key}' is missing.");
    internal int[] RabbitTable(string key) => _rabbitTables[key];
    internal string Text(int id) => _texts[id];
    internal int SourceOffset(int id, int subId) => _sourceOffsets[(id, subId)];
    internal string Animation(int id, int index) => _animations.TryGetValue((id, index), out string? value) ? value :
        throw new InvalidOperationException($"Post-D7 source animation ${id:x2}/${index:x2} is missing.");
    internal IReadOnlyList<CutsceneCommand> Commands(string lane) => _scripts[lane];
    internal IReadOnlyList<PostD7Placement> Placements(string scene) => _placements[scene];
    internal NpcRecord Actor(int id, int subId, Vector2 position, int group, int room)
    {
        if (!_visuals.TryGetValue((id, subId), out var visual))
            throw new InvalidOperationException($"Post-D7 temporary object ${id:x2}:${subId:x2} is not imported.");
        return visual with { Group = group, Room = room, X = (int)position.X, Y = (int)position.Y };
    }
}

internal readonly record struct PostD7Placement(int Id, int SubId, int Y, int X, int Var03);
