using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Imported native presentation state for the three petrified rabbits in
/// past room 1:84.
/// </summary>
internal sealed class StoneRabbitDatabase
{
    public StoneRabbitRecord Record { get; }
    public Color[] StonePalette { get; }

    public StoneRabbitDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/stone_rabbit.tsv",
            new GeneratedTableSchema(
                "room 1:84 stone rabbit",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "id", "subid", "palette",
                    "animation-index", "collision-radius", "animation",
                    "source"
                ],
                ["group", "room", "id", "subid"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one room 1:84 stone-rabbit state, got " +
                $"{table.Rows.Count}.");
        }

        GeneratedTableRow row = table.Rows[0];
        Record = new StoneRabbitRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.RequiredString(7),
            row.RequiredString(8));
        if (Record is not
            {
                Group: 1,
                Room: 0x84,
                Id: 0x4b,
                SubId: 0x06,
                Palette: 0x06,
                AnimationIndex: 0x06,
                CollisionRadius: 0x06
            } ||
            string.IsNullOrEmpty(Record.Animation))
        {
            throw new InvalidOperationException(
                $"Invalid room 1:84 stone-rabbit state at " +
                $"{row.Path}:{row.LineNumber}.");
        }

        byte[] palette = FileAccess.GetFileAsBytes(
            "res://assets/oracle/cutscenes/nayru_stone_sprite_palette.bin");
        if (palette.Length != 12)
        {
            throw new InvalidOperationException(
                $"PALH_a2 stone palette should contain 12 bytes, got " +
                $"{palette.Length}.");
        }
        StonePalette = new Color[4];
        for (int color = 0; color < StonePalette.Length; color++)
        {
            int offset = color * 3;
            StonePalette[color] = new Color(
                palette[offset] / 31.0f,
                palette[offset + 1] / 31.0f,
                palette[offset + 2] / 31.0f,
                color == 0 ? 0.0f : 1.0f);
        }
    }

    public bool Matches(NpcRecord npc) =>
        npc.Group == Record.Group &&
        npc.Room == Record.Room &&
        npc.Id == Record.Id &&
        npc.SubId == Record.SubId;
}

internal readonly record struct StoneRabbitRecord(
    int Group,
    int Room,
    int Id,
    int SubId,
    int Palette,
    int AnimationIndex,
    int CollisionRadius,
    string Animation,
    string Source);
