using Godot;
using System;
using System.Text;

namespace oracleofages;

/// <summary>
/// Imported parameters for INTERAC_MALE_VILLAGER ($3a) subid $0d, the
/// one-shot cutscene that runs when Link first enters past room $39.
/// </summary>
public sealed class EnterPastEventDatabase
{
    public EnterPastEventRecord Record { get; }

    public EnterPastEventDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/cutscenes/enter_past_event.tsv");
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
            throw new InvalidOperationException("Enter-past event data is empty.");

        string[] columns = row.Split('\t');
        if (columns.Length != 24)
        {
            throw new InvalidOperationException(
                $"Enter-past event row should contain 24 columns, got {columns.Length}.");
        }

        Record = new EnterPastEventRecord(
            int.Parse(columns[0]),
            Convert.ToInt32(columns[1], 16),
            Convert.ToInt32(columns[2], 16),
            Convert.ToInt32(columns[3], 16),
            int.Parse(columns[4]),
            int.Parse(columns[5]),
            int.Parse(columns[6]),
            int.Parse(columns[7]),
            int.Parse(columns[8]),
            int.Parse(columns[9]),
            Convert.ToInt32(columns[10], 16),
            Convert.ToInt32(columns[11], 16),
            int.Parse(columns[12]),
            int.Parse(columns[13]),
            int.Parse(columns[14]),
            int.Parse(columns[15]),
            int.Parse(columns[16]),
            Convert.ToInt32(columns[17], 16),
            Convert.ToInt32(columns[18], 16),
            columns[19],
            columns[20],
            Encoding.UTF8.GetString(Convert.FromBase64String(columns[21])),
            Convert.ToInt32(columns[22], 16),
            int.Parse(columns[23]));
    }

    public readonly record struct EnterPastEventRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        int IntroWaitFrames,
        int PreJumpWaitFrames,
        int PostJumpWaitFrames,
        int PostTextWaitFrames,
        int JumpSpeedZ,
        int JumpGravity,
        int FastSpeed,
        int SlowSpeed,
        int FirstDownCounter,
        int RightCounter,
        int SecondDownCounter,
        int SlowDownCounter,
        int FinalDownCounter,
        int GlobalFlag,
        int TextId,
        string RightAnimation,
        string DownAnimation,
        string Text,
        int JumpSound,
        int ExpectedArrivalCounter);
}
