using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class OceanInteractionDatabase
{
    internal IReadOnlyList<CutsceneCommand> KingPresent { get; } = Load("king_zora_present");
    internal IReadOnlyList<CutsceneCommand> KingPast { get; } = Load("king_zora_past");
    internal IReadOnlyList<CutsceneCommand> Scale { get; } = Load("zora_scale");
    internal IReadOnlyList<CutsceneCommand> StormGuard { get; } = Load("zora_storm_guard");
    internal NpcRecord Potion { get; }

    internal OceanInteractionDatabase()
    {
        var table = GeneratedTable.Load(AgesGameContent.AssetRoot + "objects/ocean_visuals.tsv",
            new GeneratedTableSchema("ocean native visuals", GeneratedTableKeySemantics.Unique,
                ["role", "id", "subid", "sprite", "tile-base", "palette", "animation"], ["role"], headerRequired: true));
        if (table.Rows.Count != 1) throw new InvalidOperationException("kingZora.s: expected one potion visual.");
        var row = table.Rows[0];
        if (row.RequiredString(0) != "potion" || row.HexByte(1) != AgesInteractionId.KingZora || row.HexByte(2) != 2)
            throw new InvalidOperationException("kingZora.s: invalid $9c:$02 potion visual.");
        string animation = row.RequiredString(6);
        Potion = new(5, 0xad, row.HexByte(1), row.HexByte(2), 0x34, 0x78, 0, 0,
            row.RequiredString(3), row.UnsignedDecimal(4), row.UnsignedDecimal(5), 0, false,
            animation, animation, animation, animation, "", NpcImplementationClassification.SpecializedNative);
    }

    internal static int Entry(IReadOnlyList<CutsceneCommand> commands, string label)
    {
        for (int i = 0; i < commands.Count; i++)
            if (commands[i].Source.Label == label) return i;
        throw new InvalidOperationException($"Ocean script entry {label} is missing.");
    }
    private static IReadOnlyList<CutsceneCommand> Load(string name) =>
        CutsceneCommandCatalog.Load(AgesGameContent.AssetRoot + $"cutscenes/{name}_commands.tsv");
}
