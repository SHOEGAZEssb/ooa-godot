using System.Collections.Generic;

namespace oracleofages;

/// <summary>Placement and native bindings; script operands belong to Commands.</summary>
internal sealed class RalphPortalEventDatabase
{
    public RalphPortalEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public RalphPortalEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/ralph_portal_event.tsv",
            new GeneratedTableSchema(
                "Ralph portal event",
                GeneratedTableKeySemantics.Ordered,
                ["group", "room", "id", "subid", "entry-direction", "global-flag"],
                headerRequired: true)).SingleRow();
        Record = new RalphPortalEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5));
        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/ralph_portal_commands.tsv");
    }
}

internal readonly record struct RalphPortalEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int EntryDirection,
    int GlobalFlag);
