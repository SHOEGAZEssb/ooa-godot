using System.Collections.Generic;

namespace Oracles;

internal sealed class SharedNpcDatabase
{
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<(int, int), string> _animations = new();
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _scripts = new();
    private readonly Dictionary<int, string> _extraGraphics = new();
    internal IReadOnlyList<NpcRecord> WaitingAnimals { get; }
    internal SharedNpcDatabase()
    {
        var animals = new List<NpcRecord>();
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/animals_waiting_for_nayru.tsv",
            new GeneratedTableSchema("animalsWaitingForNayru", GeneratedTableKeySemantics.Ordered,
                ["group", "room", "id", "subid", "y", "x", "var03", "text-id", "sprite", "tile-base", "palette", "default-animation", "can-face",
                    "up-animation", "right-animation", "down-animation", "left-animation", "utf8-base64", "implementation"], headerRequired: true)).Rows)
            animals.Add(NpcDatabase.ParseNpcRecord(row, 0));
        WaitingAnimals = animals;
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/shared_npc_texts.tsv",
            new GeneratedTableSchema("shared NPC texts", GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"], ["text-id"], headerRequired: true)).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/shared_npc_animations.tsv",
            new GeneratedTableSchema("shared NPC animations", GeneratedTableKeySemantics.Unique,
                ["id", "index", "animation"], ["id", "index"], headerRequired: true)).Rows)
            _animations.Add((row.HexByte(0), row.HexByte(1)), row.RequiredString(2));
        foreach (string name in new[] { "nayru", "ralph", "boy", "monkey", "bear", "old_man_gift", "adlar" })
            _scripts.Add(name, CutsceneCommandCatalog.Load(AgesGameContent.AssetRoot + $"cutscenes/shared_npc_{name}_commands.tsv"));
        foreach (var row in GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/shared_npc_extra_graphics.tsv",
            new GeneratedTableSchema("shared NPC extra graphics", GeneratedTableKeySemantics.Unique,
                ["id", "sprite"], ["id"], headerRequired: true)).Rows)
            _extraGraphics.Add(row.HexByte(0), row.RequiredString(1));
    }
    internal string Text(int id) => _texts[id];
    internal string Animation(int id, int index) => _animations[(id, index)];
    internal string ExtraGraphics(int id) => _extraGraphics[id];
    internal IReadOnlyList<CutsceneCommand> Script(NpcRecord npc) => _scripts[npc.Id switch
    {
        0x36 => "nayru", 0x37 => "ralph", 0x3c => "boy", 0x39 => "monkey",
        0x5d => "bear", 0x2e => "old_man_gift", 0x29 => "adlar",
        _ => throw new System.NotSupportedException($"Shared NPC ${npc.Id:x2}:${npc.SubId:x2}.")
    }];
}
