using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class BlackTowerSeedDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<string, byte[]> _tables = [];
    private readonly Dictionary<(int, int), NpcRecord> _visuals = [];
    private readonly Dictionary<(int, int), int> _sourceOffsets = [];
    internal Color[] SeedPalette { get; } = OracleGraphicsData.LoadPaletteColors(
        Root + "black_tower_seed_palette.bin", transparentZero: true);

    internal BlackTowerSeedDatabase()
    {
        foreach (var row in GeneratedTable.Load(Root + "black_tower_seed_tables.tsv",
            new("Black Tower seed tables", GeneratedTableKeySemantics.Unique,
                ["key", "values", "source"], ["key"], headerRequired: true)).Rows)
            _tables.Add(row.RequiredString(0), Array.ConvertAll(row.SplitRequired(1, ','), value => Convert.ToByte(value, 16)));
        foreach (var (key, count) in new[] { ("essence-spawns", 16), ("tiles", 37), ("sparkle-offsets", 16), ("heights", 8) })
            if (!_tables.TryGetValue(key, out var values) || values.Length != count)
                throw new InvalidOperationException($"makuSeedAndEssences.s: incomplete {key}.");
        foreach (var row in GeneratedTable.Load(Root + "black_tower_seed_visuals.tsv",
            new("Black Tower seed graphics", GeneratedTableKeySemantics.Unique,
                ["id", "subid", "sprite", "tile-base", "palette", "animation", "source-offset", "source"], ["id", "subid"], headerRequired: true)).Rows)
        {
            int id = row.HexByte(0), subid = row.HexByte(1);
            string animation = row.RequiredString(5);
            _sourceOffsets.Add((id, subid), row.HexWord(6));
            _visuals.Add((id, subid), new(4, 0xea, id, subid, 0, 0, 0, 0, row.RequiredString(2),
                row.UnsignedDecimal(3), row.UnsignedDecimal(4), 0, false,
                animation, animation, animation, animation, "", NpcImplementationClassification.EventOwned));
        }
    }
    internal byte[] Table(string key) => _tables[key];
    internal NpcCharacter Actor(int id, int subid, Vector2 position)
    {
        var actor = NpcCharacter.CreateFromRecord(_visuals[(id, subid)] with { X = (int)position.X, Y = (int)position.Y });
        actor.SetGraphicsSourceOffset(_sourceOffsets[(id, subid)]);
        actor.SetScriptAnimation(actor.Record.DownAnimation);
        actor.SetAnimationRate(0);
        actor.SetBlocksLink(false);
        if (id == 0xd7 && subid == 0) actor.SetScriptPaletteOverrides(new Dictionary<int, Color[]> { [7] = SeedPalette });
        return actor;
    }
}
