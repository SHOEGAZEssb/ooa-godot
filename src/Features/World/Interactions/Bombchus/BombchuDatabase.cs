using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// ITEM$0d uses resident common sprites. Its animation $06 is not ITEM$03's
// explosion, even though both items dispatch the same explosion handler.
internal sealed class BombchuDatabase
{
    private readonly BombchuGraphic[] _graphics = new BombchuGraphic[7];
    private readonly bool[] _targets = new bool[128];
    private readonly Dictionary<string, Vector2I[]> _offsets = new();
    private readonly Dictionary<string, int> _mechanics = new();

    internal BombchuDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/bombchu_animations.tsv",
            new GeneratedTableSchema("ITEM$0d animations", GeneratedTableKeySemantics.Unique,
                ["animation", "sprite", "tile-base", "oam-flags", "collision", "radius-y",
                 "radius-x", "damage", "health", "frames", "source"], ["animation"], headerRequired: true));
        if (table.Rows.Count != 7) throw new InvalidOperationException("ITEM$0d requires seven ordered animation streams.");
        for (int index = 0; index < 7; index++)
        {
            var row = table.Rows[index];
            if (row.Decimal(0) != index) throw row.Invalid(0, "ordered animations $00-$06");
            var graphic = new BombchuGraphic(row.RequiredString(1), row.HexByte(2), row.HexByte(3),
                row.HexByte(4), new(row.Decimal(6, 0, 15), row.Decimal(5, 0, 15)),
                row.HexByte(7), row.HexByte(8), row.RequiredString(9), row.RequiredString(10));
            var definition = OracleGraphicsCache.GetAnimationDefinition(graphic.Animation);
            if (definition.Frames.Length != (index == 6 ? 7 : 2) ||
                index == 6 && definition.Frames[^1].Parameter != 0xff)
                throw row.Invalid(9, "two-frame walking loop or seven-frame explosion ending with $ff");
            _graphics[index] = graphic;
        }
        table = GeneratedTable.Load("res://assets/oracle/metadata/bombchu_targets.tsv",
            new GeneratedTableSchema("bombchuTargets", GeneratedTableKeySemantics.Unique,
                ["enemy-id", "target", "source"], ["enemy-id"], headerRequired: true));
        if (table.Rows.Count != 128) throw new InvalidOperationException("bombchuTargets requires enemy IDs $00-$7f.");
        for (int index = 0; index < 128; index++)
        {
            var row = table.Rows[index];
            if (row.HexByte(0) != index) throw row.Invalid(0, "ordered enemy IDs $00-$7f");
            _targets[index] = row.Decimal(1, 0, 1) != 0;
            _ = row.RequiredString(2);
        }
        table = GeneratedTable.Load("res://assets/oracle/metadata/bombchu_offsets.tsv",
            new GeneratedTableSchema("ITEM$0d direction offsets", GeneratedTableKeySemantics.Unique,
                ["kind", "direction", "y", "x", "source"], ["kind", "direction"], headerRequired: true));
        if (table.Rows.Count != 12) throw new InvalidOperationException("ITEM$0d requires twelve ordered offset pairs.");
        string[] kinds = ["front", "normal", "sidescroll"];
        for (int index = 0; index < 12; index++)
        {
            var row = table.Rows[index];
            string kind = kinds[index / 4];
            int direction = index % 4;
            if (row.RequiredString(0) != kind || row.Decimal(1) != direction)
                throw row.Invalid(0, "ordered front/normal/sidescroll direction pairs");
            if (direction == 0) _offsets.Add(kind, new Vector2I[4]);
            _offsets[kind][direction] = new(row.Decimal(3, -128, 127), row.Decimal(2, -128, 127));
            _ = row.RequiredString(4);
        }
        table = GeneratedTable.Load("res://assets/oracle/metadata/bombchu_mechanics.tsv",
            new GeneratedTableSchema("ITEM$0d source constants", GeneratedTableKeySemantics.Unique,
                ["name", "value", "source"], ["name"], headerRequired: true));
        string[] names = ["initial-wait", "initial-fuse", "initial-vision", "initial-turn", "search-speed",
            "target-radius", "target-wait", "target-speed", "vision-step", "vision-limit", "vision-reset",
            "topdown-gravity", "sidescroll-gravity", "motion-gravity", "chase-counter", "homing-frame-mask",
            "wall-jump-speed-z", "child-limit"];
        if (table.Rows.Count != names.Length) throw new InvalidOperationException("ITEM$0d source constant set changed.");
        for (int index = 0; index < names.Length; index++)
        {
            var row = table.Rows[index];
            if (row.RequiredString(0) != names[index]) throw row.Invalid(0, "ordered ITEM$0d constants");
            _mechanics.Add(names[index], row.Decimal(1, -32768, 255));
            _ = row.RequiredString(2);
        }
    }

    internal BombchuGraphic Graphic(int animation) => _graphics[animation];
    internal bool CanTarget(int enemyId) => enemyId >= 0 && enemyId < _targets.Length && _targets[enemyId];
    internal Vector2I FrontOffset(int angle) => _offsets["front"][(angle & 0x18) >> 3];
    internal Vector2I PlacementOffset(int direction, bool sidescrollGroup) =>
        _offsets[sidescrollGroup ? "sidescroll" : "normal"][direction & 3];
    internal int Constant(string name) => _mechanics.TryGetValue(name, out int value) ? value :
        throw new InvalidOperationException($"ITEM$0d has no imported source constant '{name}'.");
}

internal readonly record struct BombchuGraphic(string Sprite, int TileBase, int OamFlags,
    int Collision, Vector2I Radius, int RawDamage, int Health, string Animation, string Source);
