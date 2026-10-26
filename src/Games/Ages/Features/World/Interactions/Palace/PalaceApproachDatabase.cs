using System.Collections.Generic;

namespace Oracles;

internal sealed class PalaceApproachDatabase
{
    internal IReadOnlyList<CutsceneCommand> TriggerCommands { get; } = CutsceneCommandCatalog.Load(AgesGameContent.AssetRoot + "cutscenes/palace_approach_commands.tsv");
    internal IReadOnlyList<CutsceneCommand> RalphCommands { get; } = CutsceneCommandCatalog.Load(AgesGameContent.AssetRoot + "cutscenes/palace_approach_ralph_commands.tsv");
    internal NpcRecord Ralph { get; }
    internal PalaceApproachDatabase()
    {
        var row = GeneratedTable.Load(AgesGameContent.AssetRoot + "cutscenes/palace_approach_ralph.tsv",
            new GeneratedTableSchema("palace approach Ralph", GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "animation", "source"], headerRequired: true)).Rows[0];
        string animation = row.RequiredString(3);
        Ralph = new(1, 0x46, 0x37, 1, 0x50, 0xb0, 0, 0, row.RequiredString(0),
            row.Decimal(1), row.Decimal(2), 0, false, animation, animation, animation, animation, "",
            NpcImplementationClassification.EventOwned);
        _ = row.RequiredString(4);
    }
}
