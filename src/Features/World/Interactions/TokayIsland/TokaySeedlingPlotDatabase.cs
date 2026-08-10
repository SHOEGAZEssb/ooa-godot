using System;

namespace oracleofages;

/// <summary>
/// Source-derived movement and INTERAC_DECORATION $80:$04 data for
/// tokayAtSeedlingPlotScript in room 1:ac.
/// </summary>
internal sealed class TokaySeedlingPlotDatabase
{
    internal TokaySeedlingPlotRecord Record { get; }

    internal bool MatchesRoom(int group, int room) =>
        group == Record.Group && room == Record.Room;

    internal bool MatchesNpc(NpcRecord npc) =>
        MatchesRoom(npc.Group, npc.Room) &&
        npc.Id == Record.NpcId && npc.SubId == Record.NpcSubId;

    internal TokaySeedlingPlotDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_seedling_plot.tsv",
            new GeneratedTableSchema(
                "Tokay scent-seedling plot",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "npc-id", "npc-subid",
                    "decoration-id", "decoration-subid", "y", "x",
                    "room-flag", "speed", "move-counter", "planted-x-offset",
                    "intro-wait", "done-wait", "sprite", "tile-base", "palette",
                    "animation", "source"
                ],
                ["group", "room", "npc-id", "npc-subid"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Tokay scent-seedling plot should have one row, got " +
                $"{table.Rows.Count}.");
        }

        GeneratedTableRow row = table.Rows[0];
        Record = new TokaySeedlingPlotRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2),
            row.HexByte(3), row.HexByte(4), row.HexByte(5),
            row.HexByte(6), row.HexByte(7), row.HexByte(8),
            row.HexByte(9), row.HexByte(10), row.HexByte(11),
            row.UnsignedDecimal(12), row.UnsignedDecimal(13),
            row.RequiredString(14), row.HexByte(15), row.HexByte(16),
            row.RequiredString(17), row.RequiredString(18));

        if (Record is not
            {
                Group: 1,
                Room: 0xac,
                NpcId: 0x48,
                NpcSubId: 0x11,
                DecorationId: 0x80,
                DecorationSubId: 0x04,
                Y: 0x38,
                X: 0x48,
                RoomFlag: 0x80,
                Speed: 0x28,
                MoveCounter: 0x10,
                PlantedXOffset: 0x10,
                IntroWait: 30,
                DoneWait: 120
            })
        {
            throw new InvalidOperationException(
                "Imported room 1:ac Tokay scent-seedling contract is incomplete.");
        }
    }
}

internal sealed record TokaySeedlingPlotRecord(
    int Group,
    int Room,
    int NpcId,
    int NpcSubId,
    int DecorationId,
    int DecorationSubId,
    int Y,
    int X,
    int RoomFlag,
    int Speed,
    int MoveCounter,
    int PlantedXOffset,
    int IntroWait,
    int DoneWait,
    string Sprite,
    int TileBase,
    int Palette,
    string Animation,
    string Source);
