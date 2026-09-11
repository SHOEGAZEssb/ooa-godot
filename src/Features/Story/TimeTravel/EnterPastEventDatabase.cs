using System.Collections.Generic;

namespace oracleofages;

/// <summary>Placement and native bindings; script operands belong to Commands.</summary>
internal sealed class EnterPastEventDatabase
{
    public EnterPastEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public EnterPastEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/enter_past_event.tsv",
            new GeneratedTableSchema(
                "enter-past event",
                GeneratedTableKeySemantics.Ordered,
                ["group", "room", "id", "subid", "animation-double-speed", "global-flag"],
                headerRequired: true)).SingleRow();
        Record = new EnterPastEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5));
        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/enter_past_commands.tsv");
    }
}

internal readonly record struct EnterPastEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int AnimationDoubleSpeed,
    int GlobalFlag);
