using Godot;
using System;
using System.Text;

namespace oracleofages;

/// <summary>
/// Imported script-selected visuals, text, sound, and dirt-chip physics for
/// INTERAC_PICKAXE_WORKER $57:$00 in past room $48.
/// </summary>
internal sealed class Room148PickaxeDatabase
{
    internal readonly record struct PickaxeRecord(
        string SpriteName,
        int WorkerTileBase,
        int WorkerPalette,
        string WorkAnimation,
        string TalkAnimation,
        string DebrisSpriteName,
        int DebrisTileBase,
        string DebrisAnimation,
        int TextId,
        string Message,
        int Sound,
        int DebrisCount,
        int OffsetY,
        int OffsetX,
        int Speed,
        int InitialSpeedZ,
        int Gravity,
        int Angle0,
        int Angle1);

    public PickaxeRecord Record { get; }

    public Room148PickaxeDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/room148_pickaxe.tsv");
        string? row = null;
        foreach (string rawLine in source.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            if (row is not null)
                throw new InvalidOperationException(
                    "Room 1:48 pickaxe data contains more than one record.");
            row = line;
        }

        if (row is null)
            throw new InvalidOperationException(
                "Room 1:48 pickaxe data contains no record.");
        string[] columns = row.Split('\t');
        if (columns.Length != 19)
            throw new InvalidOperationException(
                $"Malformed room 1:48 pickaxe row: {row}");

        Record = new PickaxeRecord(
            columns[0],
            int.Parse(columns[1]),
            int.Parse(columns[2]),
            columns[3],
            columns[4],
            columns[5],
            int.Parse(columns[6]),
            columns[7],
            Convert.ToInt32(columns[8], 16),
            Encoding.UTF8.GetString(Convert.FromBase64String(columns[9])),
            int.Parse(columns[10]),
            int.Parse(columns[11]),
            int.Parse(columns[12]),
            int.Parse(columns[13]),
            int.Parse(columns[14]),
            int.Parse(columns[15]),
            int.Parse(columns[16]),
            int.Parse(columns[17]),
            int.Parse(columns[18]));

        OracleGraphicsCache.AnimationDefinition work =
            OracleGraphicsCache.GetAnimationDefinition(Record.WorkAnimation);
        OracleGraphicsCache.AnimationDefinition talk =
            OracleGraphicsCache.GetAnimationDefinition(Record.TalkAnimation);
        OracleGraphicsCache.AnimationDefinition debris =
            OracleGraphicsCache.GetAnimationDefinition(Record.DebrisAnimation);
        bool hasLeftStrike = Array.Exists(
            work.Frames, frame => frame.Parameter == 1);
        bool hasRightStrike = Array.Exists(
            work.Frames, frame => frame.Parameter == 2);
        if (Record.DebrisSpriteName != "spr_common_sprites" ||
            Record.DebrisTileBase != 0x02 ||
            Record.TextId != 0x1b00 || string.IsNullOrEmpty(Record.Message) ||
            Record.DebrisCount != 2 || Record.OffsetY != 4 ||
            Record.OffsetX != 14 || Record.Speed <= 0 ||
            Record.InitialSpeedZ >= 0 || Record.Gravity <= 0 ||
            Record.Angle0 != 0x08 || Record.Angle1 != 0x18 ||
            work.Frames.Length == 0 || !hasLeftStrike || !hasRightStrike ||
            talk.Frames.Length == 0 || debris.Frames.Length == 0)
        {
            throw new InvalidOperationException(
                "Room 1:48 pickaxe data does not match the imported $57:$00/$92:$06 contract.");
        }
    }
}
