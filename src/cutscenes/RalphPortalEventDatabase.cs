using Godot;
using System;
using System.Text;

namespace oracleofages;

/// <summary>
/// Imported parameters for Ralph's first trip through a time portal. The
/// source interaction is INTERAC_RALPH ($37) subid $0d in present room $39.
/// </summary>
public sealed class RalphPortalEventDatabase
{
    public RalphPortalEventRecord Record { get; }

    public RalphPortalEventDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/ralph_portal_event.tsv");
        string? row = null;
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (!line.StartsWith('#'))
            {
                row = line;
                break;
            }
        }
        if (row is null)
            throw new InvalidOperationException("Ralph portal event data is empty.");

        string[] columns = row.Split('\t');
        if (columns.Length != 16)
            throw new InvalidOperationException(
                $"Ralph portal event row should contain 16 columns, got {columns.Length}.");

        Record = new RalphPortalEventRecord(
            int.Parse(columns[0]),
            Convert.ToInt32(columns[1], 16),
            Convert.ToInt32(columns[2], 16),
            Convert.ToInt32(columns[3], 16),
            Convert.ToInt32(columns[4], 16),
            int.Parse(columns[5]),
            int.Parse(columns[6]),
            int.Parse(columns[7]),
            int.Parse(columns[8]),
            Convert.ToInt32(columns[9], 16),
            Convert.ToInt32(columns[10], 16),
            Convert.ToInt32(columns[11], 16),
            Convert.ToInt32(columns[12], 16),
            columns[13],
            columns[14],
            Encoding.UTF8.GetString(Convert.FromBase64String(columns[15])));
    }

    public readonly record struct RalphPortalEventRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        int EntryDirection,
        int IntroDelayFrames,
        int PostTextFrames,
        int MovementCounter,
        int FlickerFrames,
        int Speed,
        int Angle,
        int GlobalFlag,
        int TextId,
        string MovementAnimation,
        string PortalAnimation,
        string Text);
}
