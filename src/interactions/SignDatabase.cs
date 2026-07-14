using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

public sealed class SignDatabase
{
    private readonly Dictionary<int, string> _messages = new();

    public SignDatabase()
    {
        string source = FileAccess.GetFileAsString("res://assets/oracle/objects/signs.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 5)
                throw new InvalidOperationException($"Malformed sign data row: {line}");

            int group = int.Parse(columns[0]);
            int room = Convert.ToInt32(columns[1], 16);
            int position = Convert.ToInt32(columns[2], 16);
            int key = MakeKey(group, room, position);
            _messages[key] = Encoding.UTF8.GetString(Convert.FromBase64String(columns[4]));
        }

        if (_messages.Count != 42)
            throw new InvalidOperationException($"Expected 42 sign records, loaded {_messages.Count}.");
    }

    public bool TryGetMessage(int group, int room, int position, out string message)
    {
        return _messages.TryGetValue(MakeKey(group, room, position), out message!);
    }

    private static int MakeKey(int group, int room, int position)
    {
        return (group << 16) | (room << 8) | position;
    }
}
