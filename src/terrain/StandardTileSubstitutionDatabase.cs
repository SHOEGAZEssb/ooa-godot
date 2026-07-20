using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// applyStandardTileSubstitutions: room-flag bits 0-3 and 7 select an imported
/// replacement list by wActiveCollisions whenever a room is loaded.
/// </summary>
public sealed class StandardTileSubstitutionDatabase
{
    private readonly Dictionary<(int Flag, int Collisions), Dictionary<byte, byte>>
        _substitutions = new();

    internal int RecordCount { get; }

    public StandardTileSubstitutionDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/metadata/standard_tile_substitutions.tsv");
        int count = 0;
        foreach (string rawLine in source.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 5 || string.IsNullOrWhiteSpace(columns[4]))
                throw new InvalidOperationException(
                    $"Malformed standard tile-substitution row: {line}");

            int flag = Convert.ToInt32(columns[0], 16);
            int collisions = int.Parse(columns[1]);
            byte replacement = Convert.ToByte(columns[2], 16);
            byte original = Convert.ToByte(columns[3], 16);
            var key = (flag, collisions);
            if (!_substitutions.TryGetValue(key, out Dictionary<byte, byte>? records))
            {
                records = new Dictionary<byte, byte>();
                _substitutions.Add(key, records);
            }
            if (!records.TryAdd(original, replacement))
            {
                throw new InvalidOperationException(
                    $"Duplicate standard tile substitution ${flag:x2}/" +
                    $"${collisions:x2}:${original:x2}.");
            }
            count++;
        }
        RecordCount = count;
    }

    internal void Apply(OracleRoomData room, byte roomFlags, long animationTick)
    {
        foreach (int flag in new[] { 0x01, 0x02, 0x04, 0x08, 0x80 })
        {
            if ((roomFlags & flag) == 0 ||
                !_substitutions.TryGetValue(
                    (flag, room.ActiveCollisions),
                    out Dictionary<byte, byte>? records))
            {
                continue;
            }
            room.ApplyMetatileSubstitutions(records, animationTick);
        }
    }
}
