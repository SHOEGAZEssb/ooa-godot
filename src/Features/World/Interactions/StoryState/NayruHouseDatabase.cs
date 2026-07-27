using System;

namespace oracleofages;

/// <summary>
/// Native room contract installed by INTERAC_IMPA_NPC $4f:$00 in room 3:9e.
/// </summary>
internal sealed class NayruHouseDatabase
{
    public NayruHouseRecord Record { get; }

    public NayruHouseDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/nayru_house.tsv",
            new GeneratedTableSchema(
                "Nayru house",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "interaction-id", "subid",
                    "stair-position", "stair-tile", "preserve-rendered",
                    "source"
                ],
                ["group", "room", "interaction-id", "subid"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one Nayru-house record, got {table.Rows.Count}.");
        }

        GeneratedTableRow row = table.Rows[0];
        Record = new NayruHouseRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.Boolean01(6),
            row.RequiredString(7));
        if (Record is not
            {
                Group: 3,
                Room: 0x9e,
                InteractionId: 0x4f,
                SubId: 0x00,
                StairPosition: 0x22,
                StairTile: 0x45,
                PreserveRendered: true
            })
        {
            throw new InvalidOperationException(
                $"Invalid Nayru-house native contract at " +
                $"{row.Path}:{row.LineNumber}.");
        }
    }

    public bool Matches(NpcRecord npc) =>
        npc.Group == Record.Group &&
        npc.Room == Record.Room &&
        (npc.Id == Record.InteractionId && npc.SubId == Record.SubId ||
         npc.Id == 0x36 && npc.SubId == 0x0b ||
         npc.Id == 0xad && npc.SubId == 0x07);
}

internal readonly record struct NayruHouseRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int StairPosition,
    int StairTile,
    bool PreserveRendered,
    string Source);
