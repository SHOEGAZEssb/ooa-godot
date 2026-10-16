using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BlackTowerConfrontationDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/black_tower_confrontation_";
    private readonly Dictionary<(int, int), string> _animations = [];
    private readonly Dictionary<int, NpcRecord> _visuals = [];
    private readonly Dictionary<int, string> _extra = [];
    internal IReadOnlyList<CutsceneCommand> Ralph { get; } = CutsceneCommandCatalog.Load(Root + "ralph.tsv");
    internal IReadOnlyList<CutsceneCommand> Ambi { get; } = CutsceneCommandCatalog.Load(Root + "ambi.tsv");
    private readonly Color[] _evilPalette = OracleGraphicsData.LoadPaletteColors(
        "res://assets/oracle/metadata/nayru_possessed_palette.bin", transparentZero: true);
    internal BlackTowerConfrontationDatabase()
    {
        foreach (var row in GeneratedTable.Load(Root + "animations.tsv", new("Black Tower confrontation animations",
            GeneratedTableKeySemantics.Unique, ["id", "index", "animation", "source"], ["id", "index"], headerRequired: true)).Rows)
            _animations.Add((row.HexByte(0), row.HexByte(1)), row.RequiredString(2));
        foreach (var row in GeneratedTable.Load(Root + "visuals.tsv", new("Black Tower confrontation graphics",
            GeneratedTableKeySemantics.Unique, ["id", "subid", "sprite", "extra-sprite", "tile-base", "palette", "default-animation", "source"],
            ["id", "subid"], headerRequired: true)).Rows)
        {
            int id = row.HexByte(0), animation = row.UnsignedDecimal(6);
            string encoded = Animation(id, animation);
            _visuals.Add(id, new(4, 0xfe, id, row.HexByte(1), 0, 0, 0, 0, row.RequiredString(2),
                row.UnsignedDecimal(4), row.UnsignedDecimal(5), animation, false, encoded, encoded, encoded, encoded, "",
                NpcImplementationClassification.EventOwned));
            _extra.Add(id, row.RequiredString(3));
        }
    }
    internal string Animation(int id, int index) => _animations[(id, index)];
    internal NpcCharacter Actor(int id, Vector2 position)
    {
        var actor = NpcCharacter.CreateFromRecord(_visuals[id] with { X = (int)position.X, Y = (int)position.Y });
        if (_extra[id] != "-") actor.AppendScriptGraphics(_extra[id]);
        actor.SetScriptAnimation(actor.Record.DownAnimation); actor.SetAnimationRate(0);
        actor.SetBlocksLink(id != 0x5e);
        if (id == 0x4d) { actor.SetBasePalette(6); actor.SetScriptPaletteOverrides(new Dictionary<int, Color[]> { [6] = _evilPalette }); }
        return actor;
    }
}
