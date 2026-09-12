using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PatchDatabase
{
    internal IReadOnlyList<CutsceneCommand> Commands { get; } = CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/patch_commands.tsv");
    private readonly Dictionary<string, int> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _animations = new();
    private readonly Dictionary<int, TimedSparkleVisual> _visuals = new();
    private readonly Dictionary<int, string> _itemNames = new();
    private readonly Dictionary<int, int> _textFlags = new();
    private readonly Dictionary<string, (int Mode, bool RoomFlag)> _rewards = new();
    internal int ResetGroup { get; }
    internal int ResetRoom { get; }
    internal InteractionExplosionVisual ExplosionVisual { get; }
    internal PatchDatabase()
    {
        var reset = Table("reset", ["group", "room"]);
        if (reset.Rows.Count != 1) throw new InvalidOperationException("miscPuzzles.s:$90:$10 missing Patch reset placement.");
        ResetGroup = reset.Rows[0].Decimal(0, 0, 7); ResetRoom = reset.Rows[0].HexByte(1);
        var explosion = Table("explosion", ["sprite", "tile-base", "palette", "animation"]);
        if (explosion.Rows.Count != 1) throw new InvalidOperationException("patch.s: missing $56:$00 visual.");
        var visual = explosion.Rows[0];
        ExplosionVisual = new(visual.RequiredString(0), visual.Decimal(1, 0, 255), visual.Decimal(2, 0, 7), visual.RequiredString(3));
        foreach (var row in Table("scripts", ["name", "entry"]).Rows) _entries.Add(row.RequiredString(0), row.Decimal(1, 0, Commands.Count - 1));
        foreach (var row in Table("constants", ["key", "value"]).Rows) _constants.Add(row.RequiredString(0), row.Decimal(1, 0, 65535));
        foreach (var row in Table("animations", ["index", "animation"]).Rows) _animations.Add(row.Decimal(0, 0, 12), row.RequiredString(1));
        foreach (var row in Table("visuals", ["subid", "sprite", "tile-base", "palette", "animation"]).Rows)
            _visuals.Add(row.Decimal(0, 2, 7), new(row.RequiredString(1), 0, row.Decimal(2, 0, 255), row.Decimal(3, 0, 7), row.RequiredString(4)));
        foreach (var row in Table("item_names", ["id", "text-base64"]).Rows)
            _itemNames.Add(row.HexWord(0), row.Base64Utf8(1));
        foreach (var row in Table("text_flags", ["id", "flags"]).Rows) _textFlags.Add(row.HexWord(0), row.Decimal(1, 0, 2));
        foreach (var row in Table("rewards", ["name", "mode", "room-flag"]).Rows)
            _rewards.Add(row.RequiredString(0), (row.Decimal(1, 1, 3), row.Decimal(2, 0, 1) != 0));
        if (_entries.Count != 10 || _animations.Count != 13 || _visuals.Count != 5 || _itemNames.Count != 2 || _rewards.Count != 5)
            throw new InvalidOperationException("patch.s: incomplete script/visual records.");
    }
    private static GeneratedTable Table(string name, string[] columns) => GeneratedTable.Load(
        $"res://assets/oracle/objects/patch_{name}.tsv",
        new GeneratedTableSchema($"patch.s {name}", GeneratedTableKeySemantics.Unique, columns, [columns[0]], headerRequired: true));
    internal int Entry(string name) => _entries.TryGetValue("patch_" + name, out int value) ? value : throw new InvalidOperationException($"patch.s: missing script {name}.");
    internal int Constant(string name) => _constants.TryGetValue(name, out int value) ? value : throw new InvalidOperationException($"patch.s: missing constant {name}.");
    internal string Animation(int index) => _animations[index];
    internal TimedSparkleVisual Visual(int subid) => _visuals[subid];
    internal string ItemName(int id) => _itemNames[id];
    internal int TextFlags(int id) => _textFlags[id];
    internal (int Mode, bool RoomFlag) Reward(string name) => _rewards[name];
}
